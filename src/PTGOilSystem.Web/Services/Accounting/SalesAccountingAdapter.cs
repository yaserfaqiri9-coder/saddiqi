using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public sealed record SalesAccountingResult(
    PaymentPostingStatus Status,
    JournalEntry? Journal,
    string? Reason);

public interface ISalesAccountingAdapter
{
    /// <summary>
    /// Posts the revenue a sale earned and, separately, what the goods it moved cost.
    /// </summary>
    Task<SalesAccountingResult> TryPostSaleAsync(
        SalesTransaction sale,
        CancellationToken cancellationToken = default);

    Task<SalesAccountingResult> TryPostCogsAsync(
        SalesTransaction sale,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Stage 7 dual-write pilot for sales and cost of goods sold.
///
///   Sale  Dr Accounts Receivable  Cr Sales Revenue        (party = customer)
///   Cogs  Dr Cost of Goods Sold   Cr Inventory
///
/// The two are separate journals behind separate flags on purpose. Revenue is known the moment
/// the sale is written; cost is only known once the goods that left have been valued, and that
/// depends on the purchase side having posted first. Splitting them means a sale is never held
/// hostage to its cost — and the log shows exactly which sales are carrying revenue without
/// cost yet.
///
/// Cost comes from <see cref="IInventoryValuationService"/>, the single valuation authority,
/// which holds a moving weighted average per (company, product, terminal). The quantity and
/// terminal come from the legacy outbound InventoryMovement rows the sale already wrote, so the
/// journal values exactly what the operational system says left the tank — never a re-derived
/// quantity. A sale spanning several terminals consumes from each pool in turn.
///
/// When a pool cannot cover what left it, COGS is skipped with INVENTORY_NOT_VALUED and the
/// revenue still posts. Profit reads high until the matching purchase is posted, which is
/// visible and recoverable; guessing a cost would not be.
/// </summary>
public sealed class SalesAccountingAdapter(
    ApplicationDbContext db,
    IAccountingPostingService postingService,
    IAccountingJournalNumberGenerator journalNumberGenerator,
    IInventoryValuationService valuation,
    IOptions<AccountingOptions> options,
    ILogger<SalesAccountingAdapter> logger)
    : ISalesAccountingAdapter
{
    public const string SourceModule = "Sale";
    public const string SourceEntityType = nameof(SalesTransaction);

    private readonly AccountingOptions _options = options.Value;

    public async Task<SalesAccountingResult> TryPostSaleAsync(
        SalesTransaction sale,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sale);

        if (!_options.Enabled)
            return Skipped(sale, "Sale", 0, "ACCOUNTING_DISABLED");
        if (!_options.Pilots.Sale)
            return Skipped(sale, "Sale", 0, "PILOT_DISABLED");

        var (companyId, skipReason) = await ResolveCompanyAndSkipReasonAsync(sale, cancellationToken);
        if (skipReason is not null)
            return Skipped(sale, "Sale", companyId, skipReason);

        var sourceEventId = BuildCreatedSourceEventId(sale.Id);
        var existing = await FindJournalAsync(companyId, sourceEventId, cancellationToken);
        if (existing is not null)
        {
            LogOutcome(sale, "Sale", companyId, sale.TotalUsd,
                existing.Lines.Sum(x => x.Debit), PaymentPostingStatus.Duplicate, "DUPLICATE_SOURCE_EVENT");
            return new SalesAccountingResult(
                PaymentPostingStatus.Duplicate, existing, "DUPLICATE_SOURCE_EVENT");
        }

        var settings = await db.AccountingSettings
            .AsNoTracking()
            .SingleAsync(x => x.CompanyId == companyId, cancellationToken);
        var rate = sale.AppliedFxRateToUsd!.Value;

        var request = new AccountingPostRequest(
            companyId,
            journalNumberGenerator.ForSale(companyId, sale.Id),
            sale.SaleDate.Date,
            sale.SaleDate.Date,
            sale.SaleDate.Date,
            SourceModule,
            [
                new AccountingPostLine(
                    settings.AccountsReceivableAccountId,
                    Debit: sale.TotalUsd,
                    Credit: 0m,
                    sale.Currency,
                    sale.TotalInCurrency,
                    rate,
                    AccountingPartyType.Customer,
                    sale.CustomerId,
                    ContractId: sale.ContractId,
                    ShipmentId: sale.ShipmentId,
                    ProductId: sale.ProductId,
                    Description: $"Sale invoice {sale.InvoiceNumber}"),
                new AccountingPostLine(
                    settings.SalesRevenueAccountId,
                    Debit: 0m,
                    Credit: sale.TotalUsd,
                    sale.Currency,
                    sale.TotalInCurrency,
                    rate,
                    ContractId: sale.ContractId,
                    ShipmentId: sale.ShipmentId,
                    ProductId: sale.ProductId,
                    Description: "Sales revenue")
            ],
            SourceEventId: sourceEventId,
            SourceEntityType: SourceEntityType,
            SourceEntityId: sale.Id,
            Description: $"Sale #{sale.Id} invoice {sale.InvoiceNumber} on {sale.SaleDate:yyyy-MM-dd}");

        try
        {
            var journal = await postingService.PostAsync(request, cancellationToken);
            LogOutcome(sale, "Sale", companyId, sale.TotalUsd,
                journal.Lines.Sum(x => x.Debit), PaymentPostingStatus.Posted, null);
            return new SalesAccountingResult(PaymentPostingStatus.Posted, journal, null);
        }
        catch (Exception exception)
        {
            LogFailure(sale, "Sale", exception);
            throw;
        }
    }

    public async Task<SalesAccountingResult> TryPostCogsAsync(
        SalesTransaction sale,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sale);

        if (!_options.Enabled)
            return Skipped(sale, "Cogs", 0, "ACCOUNTING_DISABLED");
        if (!_options.Pilots.Cogs)
            return Skipped(sale, "Cogs", 0, "PILOT_DISABLED");

        var (companyId, skipReason) = await ResolveCompanyAndSkipReasonAsync(sale, cancellationToken);
        if (skipReason is not null)
            return Skipped(sale, "Cogs", companyId, skipReason);

        var sourceEventId = BuildCogsSourceEventId(sale.Id);
        var existing = await FindJournalAsync(companyId, sourceEventId, cancellationToken);
        if (existing is not null)
        {
            LogOutcome(sale, "Cogs", companyId, 0m,
                existing.Lines.Sum(x => x.Debit), PaymentPostingStatus.Duplicate, "DUPLICATE_SOURCE_EVENT");
            return new SalesAccountingResult(
                PaymentPostingStatus.Duplicate, existing, "DUPLICATE_SOURCE_EVENT");
        }

        // What actually left the tanks, as the operational system recorded it.
        var outMovements = await db.InventoryMovements
            .AsNoTracking()
            .Where(x => x.SalesTransactionId == sale.Id && x.Direction == MovementDirection.Out)
            .Select(x => new { x.TerminalId, x.ProductId, x.QuantityMt })
            .ToListAsync(cancellationToken);
        if (outMovements.Count == 0)
            return Skipped(sale, "Cogs", companyId, "NO_OUTBOUND_MOVEMENT");
        if (outMovements.Any(x => x.QuantityMt <= 0m))
            return Skipped(sale, "Cogs", companyId, "INVALID_MOVEMENT_QUANTITY");

        // Value every pool first. If any of them cannot cover its share, nothing is consumed:
        // a half-valued sale would be worse than an unvalued one.
        var pools = outMovements
            .GroupBy(x => new { x.TerminalId, x.ProductId })
            .Select(g => new { g.Key.TerminalId, g.Key.ProductId, QuantityMt = g.Sum(x => x.QuantityMt) })
            .ToList();

        foreach (var pool in pools)
        {
            var available = await db.InventoryAverageCosts
                .AsNoTracking()
                .Where(x => x.CompanyId == companyId
                    && x.ProductId == pool.ProductId
                    && x.TerminalId == pool.TerminalId)
                .Select(x => (decimal?)x.QuantityMt)
                .SingleOrDefaultAsync(cancellationToken);
            if (available is null || available.Value < pool.QuantityMt)
                return Skipped(sale, "Cogs", companyId, "INVENTORY_NOT_VALUED");
        }

        var consumed = new List<(int TerminalId, int ProductId, decimal CostUsd)>();
        foreach (var pool in pools)
        {
            var consumption = await valuation.TryConsumeAsync(
                companyId,
                pool.ProductId,
                pool.TerminalId,
                pool.QuantityMt,
                cancellationToken);
            if (!consumption.Succeeded)
            {
                // Put back whatever the earlier pools already gave up, so a failure here leaves
                // the valuation exactly as it was.
                foreach (var done in consumed)
                {
                    var quantity = pools.Single(p =>
                        p.TerminalId == done.TerminalId && p.ProductId == done.ProductId).QuantityMt;
                    await valuation.ReturnAsync(
                        companyId, done.ProductId, done.TerminalId, quantity, done.CostUsd, cancellationToken);
                }

                return Skipped(sale, "Cogs", companyId, consumption.Reason ?? "INVENTORY_NOT_VALUED");
            }

            consumed.Add((pool.TerminalId, pool.ProductId, consumption.CostUsd));
        }

        var totalCostUsd = consumed.Sum(x => x.CostUsd);
        if (totalCostUsd <= 0m)
            return Skipped(sale, "Cogs", companyId, "INVENTORY_NOT_VALUED");

        var settings = await db.AccountingSettings
            .AsNoTracking()
            .SingleAsync(x => x.CompanyId == companyId, cancellationToken);

        var request = new AccountingPostRequest(
            companyId,
            journalNumberGenerator.ForCogs(companyId, sale.Id),
            sale.SaleDate.Date,
            sale.SaleDate.Date,
            sale.SaleDate.Date,
            SourceModule,
            [
                new AccountingPostLine(
                    settings.CostOfGoodsSoldAccountId,
                    Debit: totalCostUsd,
                    Credit: 0m,
                    SystemCurrency.BaseCurrencyCode,
                    totalCostUsd,
                    1m,
                    ContractId: sale.ContractId,
                    ShipmentId: sale.ShipmentId,
                    ProductId: sale.ProductId,
                    Description: $"Cost of goods sold for invoice {sale.InvoiceNumber}"),
                new AccountingPostLine(
                    settings.InventoryAccountId,
                    Debit: 0m,
                    Credit: totalCostUsd,
                    SystemCurrency.BaseCurrencyCode,
                    totalCostUsd,
                    1m,
                    ContractId: sale.ContractId,
                    ShipmentId: sale.ShipmentId,
                    ProductId: sale.ProductId,
                    Description: "Goods left inventory")
            ],
            SourceEventId: sourceEventId,
            SourceEntityType: SourceEntityType,
            SourceEntityId: sale.Id,
            Description: $"COGS for sale #{sale.Id} invoice {sale.InvoiceNumber}");

        try
        {
            var journal = await postingService.PostAsync(request, cancellationToken);
            LogOutcome(sale, "Cogs", companyId, totalCostUsd,
                journal.Lines.Sum(x => x.Debit), PaymentPostingStatus.Posted, null);
            return new SalesAccountingResult(PaymentPostingStatus.Posted, journal, null);
        }
        catch (Exception exception)
        {
            LogFailure(sale, "Cogs", exception);
            throw;
        }
    }

    public static string BuildCreatedSourceEventId(int salesTransactionId)
        => $"Sale:{salesTransactionId}:Created";

    public static string BuildCogsSourceEventId(int salesTransactionId)
        => $"Sale:{salesTransactionId}:Cogs";

    private async Task<(int CompanyId, string? SkipReason)> ResolveCompanyAndSkipReasonAsync(
        SalesTransaction sale,
        CancellationToken cancellationToken)
    {
        if (sale.IsCancelled)
            return (0, "SALE_CANCELLED");
        if (sale.QuantityMt <= 0m)
            return (0, "INVALID_SALE_QUANTITY");
        if (sale.TotalUsd <= 0m || sale.TotalInCurrency <= 0m)
            return (0, "INVALID_SALE_AMOUNT");

        var rate = sale.AppliedFxRateToUsd;
        if (!rate.HasValue || rate.Value <= 0m)
            return (0, "INVALID_SALE_FX");
        if (SystemCurrency.IsBaseCurrency(sale.Currency) && rate.Value != 1m)
            return (0, "INVALID_SALE_FX");

        var expectedUsd = decimal.Round(sale.TotalInCurrency * rate.Value, 4, MidpointRounding.AwayFromZero);
        if (sale.TotalUsd != expectedUsd)
            return (0, "INVALID_SALE_CONVERSION");

        var companyId = await ResolveCompanyAsync(sale, cancellationToken);
        if (companyId is null)
            return (0, "SALE_COMPANY_UNKNOWN");

        var settings = await db.AccountingSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.CompanyId == companyId.Value, cancellationToken);
        if (settings is null)
            return (companyId.Value, "ACCOUNTING_SETTINGS_MISSING");
        if (!string.Equals(settings.FunctionalCurrencyCode?.Trim(), "USD", StringComparison.OrdinalIgnoreCase))
            return (companyId.Value, "UNSUPPORTED_FUNCTIONAL_CURRENCY");

        var accountIds = new[]
        {
            settings.AccountsReceivableAccountId,
            settings.SalesRevenueAccountId,
            settings.CostOfGoodsSoldAccountId,
            settings.InventoryAccountId
        };
        var validAccountCount = await db.Accounts.AsNoTracking().CountAsync(
            x => accountIds.Contains(x.Id) && x.CompanyId == companyId.Value && x.IsActive,
            cancellationToken);
        if (validAccountCount != accountIds.Distinct().Count())
            return (companyId.Value, "ACCOUNTING_SETTINGS_INVALID_ACCOUNTS");

        return (companyId.Value, null);
    }

    /// <summary>
    /// A sale's company, when provable. SalesTransaction.CompanyId is nullable by design — a
    /// bulk sale of a shipment whose contracts belong to different companies has no single owner
    /// — so the source purchase contract and then the sale's own contract are the fallbacks.
    /// Anything else stays unresolved rather than guessed.
    /// </summary>
    private async Task<int?> ResolveCompanyAsync(SalesTransaction sale, CancellationToken cancellationToken)
    {
        if (sale.CompanyId.HasValue)
            return sale.CompanyId;

        foreach (var contractId in new[] { sale.SourcePurchaseContractId, sale.ContractId })
        {
            if (!contractId.HasValue)
                continue;

            var companyId = await db.Contracts
                .AsNoTracking()
                .Where(x => x.Id == contractId.Value)
                .Select(x => (int?)x.CompanyId)
                .SingleOrDefaultAsync(cancellationToken);
            if (companyId.HasValue)
                return companyId;
        }

        return null;
    }

    private async Task<JournalEntry?> FindJournalAsync(
        int companyId,
        string sourceEventId,
        CancellationToken cancellationToken)
        => await db.JournalEntries
            .AsNoTracking()
            .Include(x => x.Lines)
            .SingleOrDefaultAsync(
                x => x.CompanyId == companyId
                    && x.SourceModule == SourceModule
                    && x.SourceEventId == sourceEventId,
                cancellationToken);

    private SalesAccountingResult Skipped(
        SalesTransaction sale,
        string eventKind,
        int companyId,
        string reason)
    {
        LogOutcome(sale, eventKind, companyId, 0m, 0m, PaymentPostingStatus.Skipped, reason);
        return new SalesAccountingResult(PaymentPostingStatus.Skipped, null, reason);
    }

    private void LogOutcome(
        SalesTransaction sale,
        string eventKind,
        int companyId,
        decimal expectedAmountUsd,
        decimal journalDebitTotal,
        PaymentPostingStatus status,
        string? reason)
    {
        // Legacy writes one sale ledger row of TotalUsd and nothing at all for cost, so revenue
        // reconciles against it and COGS has no legacy counterpart to compare with.
        logger.LogInformation(
            "Sales accounting pilot comparison: SaleId {SaleId}, EventKind {EventKind}, CompanyId {CompanyId}, CustomerId {CustomerId}, LegacyTotalUsd {LegacyTotalUsd}, ExpectedAmountUsd {ExpectedAmountUsd}, JournalDebitTotal {JournalDebitTotal}, Difference {Difference}, PostingStatus {PostingStatus}, SkipOrFailureReason {SkipOrFailureReason}",
            sale.Id,
            eventKind,
            companyId,
            sale.CustomerId,
            sale.TotalUsd,
            expectedAmountUsd,
            journalDebitTotal,
            journalDebitTotal - expectedAmountUsd,
            status,
            reason);
    }

    private void LogFailure(SalesTransaction sale, string eventKind, Exception exception)
    {
        var failureReason = exception is AccountingValidationException validation
            ? validation.Code
            : exception.GetType().Name;
        logger.LogError(
            exception,
            "Sales accounting pilot posting failed for SaleId {SaleId} ({EventKind}) with FailureReason {FailureReason}",
            sale.Id,
            eventKind,
            failureReason);
    }
}
