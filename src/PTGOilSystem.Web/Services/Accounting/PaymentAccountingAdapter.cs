using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public enum PaymentPostingStatus
{
    Skipped = 0,
    Posted = 1,
    Duplicate = 2
}

/// <summary>
/// The accounting nature of a cash payment. Derived from what the payment actually is
/// (kind + advance markers + party), never from the legacy LedgerSide alone: the legacy side
/// only records which way the party balance moved, which is not enough to choose between a
/// receivable settlement and an advance.
/// </summary>
public enum PaymentAccountingEventKind
{
    CustomerReceipt = 1,
    CustomerAdvance = 2,
    SupplierPayment = 3,
    SupplierPrepayment = 4,
    SarrafCashPayment = 5
}

public sealed record PaymentAccountingResult(
    PaymentPostingStatus Status,
    JournalEntry? Journal,
    string? Reason,
    PaymentAccountingEventKind? EventKind = null);

public interface IPaymentAccountingAdapter
{
    Task<PaymentAccountingResult> TryPostPaymentAsync(
        PaymentTransaction payment,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Stage 4 dual-write pilot: mirrors cash receipts and payments into the double-entry journal
/// while the legacy ledger remains the operational source.
///
/// Mapping (functional currency is USD; the payment currency pair rides on every line):
///   CustomerReceipt    Dr Cash/Bank            Cr Accounts Receivable   (party = customer)
///   CustomerAdvance    Dr Cash/Bank            Cr Customer Advance      (party = customer)
///   SupplierPayment    Dr Accounts Payable     Cr Cash/Bank             (party = supplier)
///   SupplierPrepayment Dr Supplier Prepayment  Cr Cash/Bank             (party = supplier)
///   SarrafCashPayment  Dr Accounts Payable     Cr Cash/Bank             (party = sarraf)
///
/// The cash line always carries CashAccountId; the party line always carries PartyType/PartyId
/// plus the contract/shipment dimensions when the legacy payment supplies them.
///
/// Ambiguous kinds (ManualPayment, ManualReceipt, expense/truck/employee/commission flows) are
/// deliberately not mapped here: they are skipped with UNSUPPORTED_PAYMENT_KIND and stay
/// legacy-only until their own stage defines a proven mapping.
///
/// Company ownership comes from <see cref="IPaymentCompanyResolver"/>, which only returns a
/// company it can prove. Unprovable payments skip; legacy behaviour never changes.
/// </summary>
public sealed class PaymentAccountingAdapter(
    ApplicationDbContext db,
    IAccountingPostingService postingService,
    IAccountingJournalNumberGenerator journalNumberGenerator,
    IPaymentCompanyResolver companyResolver,
    IOptions<AccountingOptions> options,
    ILogger<PaymentAccountingAdapter> logger)
    : IPaymentAccountingAdapter
{
    public const string SourceModule = "Payment";
    public const string SourceEntityType = nameof(PaymentTransaction);

    private readonly AccountingOptions _options = options.Value;

    public async Task<PaymentAccountingResult> TryPostPaymentAsync(
        PaymentTransaction payment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);

        var eventKind = ResolveEventKind(payment);
        if (eventKind is null)
        {
            LogOutcome(payment, null, 0, 0m, PaymentPostingStatus.Skipped, "UNSUPPORTED_PAYMENT_KIND");
            return new PaymentAccountingResult(
                PaymentPostingStatus.Skipped, null, "UNSUPPORTED_PAYMENT_KIND");
        }

        var (companyId, skipReason) = await ResolveCompanyAndSkipReasonAsync(
            payment,
            eventKind.Value,
            cancellationToken);
        if (skipReason is not null)
        {
            LogOutcome(payment, eventKind, companyId, 0m, PaymentPostingStatus.Skipped, skipReason);
            return new PaymentAccountingResult(
                PaymentPostingStatus.Skipped, null, skipReason, eventKind);
        }

        var sourceEventId = BuildCreatedSourceEventId(payment.Id);
        var existing = await FindJournalAsync(companyId, sourceEventId, cancellationToken);
        if (existing is not null)
        {
            LogOutcome(payment, eventKind, companyId, existing.Lines.Sum(x => x.Debit),
                PaymentPostingStatus.Duplicate, "DUPLICATE_SOURCE_EVENT");
            return new PaymentAccountingResult(
                PaymentPostingStatus.Duplicate, existing, "DUPLICATE_SOURCE_EVENT", eventKind);
        }

        var settings = await db.AccountingSettings
            .AsNoTracking()
            .SingleAsync(x => x.CompanyId == companyId, cancellationToken);

        var request = new AccountingPostRequest(
            companyId,
            journalNumberGenerator.ForPayment(companyId, payment.Id),
            payment.PaymentDate.Date,
            payment.PaymentDate.Date,
            payment.PaymentDate.Date,
            SourceModule,
            BuildLines(payment, eventKind.Value, settings),
            SourceEventId: sourceEventId,
            SourceEntityType: SourceEntityType,
            SourceEntityId: payment.Id,
            Description: BuildJournalDescription(payment, eventKind.Value));

        try
        {
            var journal = await postingService.PostAsync(request, cancellationToken);
            LogOutcome(payment, eventKind, companyId, journal.Lines.Sum(x => x.Debit),
                PaymentPostingStatus.Posted, null);
            return new PaymentAccountingResult(PaymentPostingStatus.Posted, journal, null, eventKind);
        }
        catch (Exception exception)
        {
            LogFailure(payment, exception);
            throw;
        }
    }

    public static string BuildCreatedSourceEventId(int paymentId)
        => $"Payment:{paymentId}:Created";

    /// <summary>
    /// Maps a legacy payment onto its accounting nature. Returns null when the payment kind has
    /// no proven Stage 4 mapping, which keeps that flow legacy-only instead of guessing.
    /// </summary>
    public static PaymentAccountingEventKind? ResolveEventKind(PaymentTransaction payment)
        => payment.PaymentKind switch
        {
            PaymentKind.CustomerReceipt => payment.IsCustomerAdvance == true
                ? PaymentAccountingEventKind.CustomerAdvance
                : PaymentAccountingEventKind.CustomerReceipt,
            PaymentKind.SupplierPayment => payment.IsAdvancePayment == true
                ? PaymentAccountingEventKind.SupplierPrepayment
                : PaymentAccountingEventKind.SupplierPayment,
            PaymentKind.SarrafSettlement => PaymentAccountingEventKind.SarrafCashPayment,
            _ => null
        };

    private bool IsPilotEnabled(PaymentAccountingEventKind eventKind)
        => eventKind switch
        {
            PaymentAccountingEventKind.CustomerReceipt => _options.Pilots.CustomerReceipt,
            PaymentAccountingEventKind.CustomerAdvance => _options.Pilots.CustomerAdvance,
            PaymentAccountingEventKind.SupplierPayment => _options.Pilots.SupplierPayment,
            PaymentAccountingEventKind.SupplierPrepayment => _options.Pilots.SupplierPrepayment,
            PaymentAccountingEventKind.SarrafCashPayment => _options.Pilots.SarrafPayment,
            _ => false
        };

    private AccountingPostLine[] BuildLines(
        PaymentTransaction payment,
        PaymentAccountingEventKind eventKind,
        AccountingSettings settings)
    {
        var (partyAccountId, partyType, partyId) = eventKind switch
        {
            PaymentAccountingEventKind.CustomerReceipt =>
                (settings.AccountsReceivableAccountId, AccountingPartyType.Customer, payment.CustomerId!.Value),
            PaymentAccountingEventKind.CustomerAdvance =>
                (settings.CustomerAdvanceAccountId, AccountingPartyType.Customer, payment.CustomerId!.Value),
            PaymentAccountingEventKind.SupplierPayment =>
                (settings.AccountsPayableAccountId, AccountingPartyType.Supplier, payment.SupplierId!.Value),
            PaymentAccountingEventKind.SupplierPrepayment =>
                (settings.SupplierPrepaymentAccountId, AccountingPartyType.Supplier, payment.SupplierId!.Value),
            PaymentAccountingEventKind.SarrafCashPayment =>
                (settings.AccountsPayableAccountId, AccountingPartyType.Sarraf, payment.SarrafId!.Value),
            _ => throw new InvalidOperationException($"Unmapped accounting event kind {eventKind}.")
        };

        var rate = payment.AppliedFxRateToUsd!.Value;
        var cashIsDebit = IsCashInflow(eventKind);

        var cashLine = new AccountingPostLine(
            settings.CashBankControlAccountId,
            Debit: cashIsDebit ? payment.AmountUsd : 0m,
            Credit: cashIsDebit ? 0m : payment.AmountUsd,
            payment.Currency,
            payment.Amount,
            rate,
            CashAccountId: payment.CashAccountId,
            Description: BuildCashLineDescription(eventKind));

        var partyLine = new AccountingPostLine(
            partyAccountId,
            Debit: cashIsDebit ? 0m : payment.AmountUsd,
            Credit: cashIsDebit ? payment.AmountUsd : 0m,
            payment.Currency,
            payment.Amount,
            rate,
            partyType,
            partyId,
            ContractId: payment.ContractId,
            ShipmentId: payment.ShipmentId,
            Description: BuildPartyLineDescription(eventKind));

        // Debit first keeps the stored line order readable in the journal view.
        return cashIsDebit ? [cashLine, partyLine] : [partyLine, cashLine];
    }

    private static bool IsCashInflow(PaymentAccountingEventKind eventKind)
        => eventKind is PaymentAccountingEventKind.CustomerReceipt
            or PaymentAccountingEventKind.CustomerAdvance;

    private static PaymentDirection ExpectedDirection(PaymentAccountingEventKind eventKind)
        => IsCashInflow(eventKind) ? PaymentDirection.In : PaymentDirection.Out;

    private async Task<(int CompanyId, string? SkipReason)> ResolveCompanyAndSkipReasonAsync(
        PaymentTransaction payment,
        PaymentAccountingEventKind eventKind,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return (0, "ACCOUNTING_DISABLED");
        if (!IsPilotEnabled(eventKind))
            return (0, "PILOT_DISABLED");

        if (payment.Direction != ExpectedDirection(eventKind))
            return (0, "DIRECTION_MISMATCH");

        var partyIsPresent = eventKind switch
        {
            PaymentAccountingEventKind.CustomerReceipt or PaymentAccountingEventKind.CustomerAdvance
                => payment.CustomerId.HasValue,
            PaymentAccountingEventKind.SupplierPayment or PaymentAccountingEventKind.SupplierPrepayment
                => payment.SupplierId.HasValue,
            PaymentAccountingEventKind.SarrafCashPayment => payment.SarrafId.HasValue,
            _ => false
        };
        if (!partyIsPresent)
            return (0, "PARTY_MISSING");

        if (payment.CashAccountId <= 0)
            return (0, "CASH_ACCOUNT_MISSING");
        if (payment.Amount <= 0m || payment.AmountUsd <= 0m)
            return (0, "INVALID_PAYMENT_AMOUNT");

        var rate = payment.AppliedFxRateToUsd;
        if (!rate.HasValue || rate.Value <= 0m)
            return (0, "INVALID_PAYMENT_FX");
        if (SystemCurrency.IsBaseCurrency(payment.Currency) && rate.Value != 1m)
            return (0, "INVALID_PAYMENT_FX");

        // The posting service re-derives the functional amount from the currency pair, so a
        // legacy row whose AmountUsd drifted from Amount x rate must stay legacy-only rather
        // than fail the whole payment.
        var expectedUsd = decimal.Round(payment.Amount * rate.Value, 4, MidpointRounding.AwayFromZero);
        if (payment.AmountUsd != expectedUsd)
            return (0, "INVALID_PAYMENT_CONVERSION");

        var companyId = await companyResolver.ResolveAsync(payment, cancellationToken);
        if (companyId is null)
            return (0, "PAYMENT_COMPANY_UNKNOWN");

        var settings = await db.AccountingSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.CompanyId == companyId.Value, cancellationToken);
        if (settings is null)
            return (companyId.Value, "ACCOUNTING_SETTINGS_MISSING");
        if (!string.Equals(settings.FunctionalCurrencyCode?.Trim(), "USD", StringComparison.OrdinalIgnoreCase))
            return (companyId.Value, "UNSUPPORTED_FUNCTIONAL_CURRENCY");

        var configuredAccountIds = GetConfiguredAccountIds(settings);
        if (configuredAccountIds.Any(x => x <= 0)
            || configuredAccountIds.Distinct().Count() != configuredAccountIds.Length)
            return (companyId.Value, "ACCOUNTING_SETTINGS_INCOMPLETE");

        var validAccountCount = await db.Accounts.AsNoTracking().CountAsync(
            x => configuredAccountIds.Contains(x.Id)
                && x.CompanyId == companyId.Value
                && x.IsActive,
            cancellationToken);
        if (validAccountCount != configuredAccountIds.Length)
            return (companyId.Value, "ACCOUNTING_SETTINGS_INVALID_ACCOUNTS");

        // The cash account must belong to the journal company when it declares an owner;
        // an unowned legacy cash account stays usable so the pilot does not block operations.
        var cashAccountCompanyId = await db.CashAccounts
            .AsNoTracking()
            .Where(x => x.Id == payment.CashAccountId)
            .Select(x => x.CompanyId)
            .SingleOrDefaultAsync(cancellationToken);
        if (cashAccountCompanyId.HasValue && cashAccountCompanyId.Value != companyId.Value)
            return (companyId.Value, "CASH_ACCOUNT_COMPANY_MISMATCH");

        if (payment.ContractId.HasValue)
        {
            var contractCompanyId = await db.Contracts
                .AsNoTracking()
                .Where(x => x.Id == payment.ContractId.Value)
                .Select(x => (int?)x.CompanyId)
                .SingleOrDefaultAsync(cancellationToken);
            if (contractCompanyId.HasValue && contractCompanyId.Value != companyId.Value)
                return (companyId.Value, "COMPANY_MISMATCH");
        }

        return (companyId.Value, null);
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

    private static string BuildJournalDescription(
        PaymentTransaction payment,
        PaymentAccountingEventKind eventKind)
        => $"{eventKind} #{payment.Id} on {payment.PaymentDate:yyyy-MM-dd}";

    private static string BuildCashLineDescription(PaymentAccountingEventKind eventKind)
        => IsCashInflow(eventKind) ? "Cash received" : "Cash paid";

    private static string BuildPartyLineDescription(PaymentAccountingEventKind eventKind)
        => eventKind switch
        {
            PaymentAccountingEventKind.CustomerReceipt => "Customer receivable settled",
            PaymentAccountingEventKind.CustomerAdvance => "Customer advance received",
            PaymentAccountingEventKind.SupplierPayment => "Supplier payable settled",
            PaymentAccountingEventKind.SupplierPrepayment => "Supplier prepayment made",
            PaymentAccountingEventKind.SarrafCashPayment => "Sarraf payable settled",
            _ => "Payment party movement"
        };

    private static int[] GetConfiguredAccountIds(AccountingSettings settings)
        =>
        [
            settings.CashBankControlAccountId,
            settings.AccountsReceivableAccountId,
            settings.AccountsPayableAccountId,
            settings.InventoryAccountId,
            settings.InventoryInTransitAccountId,
            settings.SupplierPrepaymentAccountId,
            settings.CustomerAdvanceAccountId,
            settings.FreightPayableAccountId,
            settings.CommissionPayableAccountId,
            settings.EmployeeAdvanceAccountId,
            settings.EmployeePayableAccountId,
            settings.AccruedExpenseAccountId,
            settings.SalesRevenueAccountId,
            settings.CostOfGoodsSoldAccountId,
            settings.GeneralExpenseAccountId,
            settings.ExchangeGainAccountId,
            settings.ExchangeLossAccountId,
            settings.InventoryLossAccountId,
            settings.CurrentYearProfitLossAccountId,
            settings.RetainedEarningsAccountId
        ];

    private void LogOutcome(
        PaymentTransaction payment,
        PaymentAccountingEventKind? eventKind,
        int companyId,
        decimal journalDebitTotal,
        PaymentPostingStatus status,
        string? reason)
    {
        // Legacy writes a single ledger row of AmountUsd; the journal must debit the same total.
        logger.LogInformation(
            "Payment accounting pilot comparison: PaymentId {PaymentId}, PaymentKind {PaymentKind}, EventKind {EventKind}, CompanyId {CompanyId}, CashAccountId {CashAccountId}, LegacyAmountUsd {LegacyAmountUsd}, JournalDebitTotal {JournalDebitTotal}, Difference {Difference}, PostingStatus {PostingStatus}, SkipOrFailureReason {SkipOrFailureReason}",
            payment.Id,
            payment.PaymentKind,
            eventKind,
            companyId,
            payment.CashAccountId,
            payment.AmountUsd,
            journalDebitTotal,
            journalDebitTotal - payment.AmountUsd,
            status,
            reason);
    }

    private void LogFailure(PaymentTransaction payment, Exception exception)
    {
        var failureReason = exception is AccountingValidationException validation
            ? validation.Code
            : exception.GetType().Name;
        logger.LogError(
            exception,
            "Payment accounting pilot posting failed for PaymentId {PaymentId} with FailureReason {FailureReason}",
            payment.Id,
            failureReason);
    }
}
