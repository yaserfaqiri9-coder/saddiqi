using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

/// <summary>
/// The facts of one via-sarraf supplier payment, taken from the legacy rows the operational
/// flow just wrote. <paramref name="SupplierLedgerEntryId"/> is the event identity because the
/// via-sarraf flow writes no PaymentTransaction — the supplier ledger row is the only stable,
/// database-generated id for the event.
/// </summary>
public sealed record ViaSarrafSupplierPaymentEvent(
    int SupplierLedgerEntryId,
    int SupplierId,
    int SarrafId,
    int? ContractId,
    DateTime PaymentDate,
    string Currency,
    decimal Amount,
    decimal AmountUsd,
    decimal FxRateToUsd);

public sealed record ViaSarrafAccountingResult(
    PaymentPostingStatus Status,
    JournalEntry? Journal,
    string? Reason);

public interface IViaSarrafAccountingAdapter
{
    Task<ViaSarrafAccountingResult> TryPostSupplierPaymentAsync(
        ViaSarrafSupplierPaymentEvent paymentEvent,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Stage 4 dual-write pilot for the non-cash "payment via sarraf" flow, where the sarraf pays
/// the supplier on the company's behalf and cash never moves:
///
///   Debit  Accounts Payable — PartyType=Supplier  (the supplier claim goes down)
///   Credit Accounts Payable — PartyType=Sarraf    (the company now owes the sarraf)
///
/// Both lines hit the same payable control account and are told apart by party, mirroring the
/// two legacy ledger rows exactly. No cash line is produced, because no cash moved.
///
/// Company ownership: this flow carries no CompanyId of its own and no PaymentTransaction, so
/// the contract is the only provable source. A via-sarraf payment without a contract stays
/// legacy-only rather than being assigned a guessed company.
///
/// Known limitation — non-USD stays legacy-only: the legacy flow derives AmountUsd by dividing
/// by the document rate while storing a separately rounded FxRateToUsd, so for RUB the identity
/// AmountUsd == round(Amount x FxRateToUsd, 4) does not hold. The posting service re-derives the
/// functional amount from that pair, so such events are skipped with INVALID_PAYMENT_CONVERSION
/// instead of being posted with a fabricated rate. USD via-sarraf payments post normally.
/// </summary>
public sealed class ViaSarrafAccountingAdapter(
    ApplicationDbContext db,
    IAccountingPostingService postingService,
    IAccountingJournalNumberGenerator journalNumberGenerator,
    IOptions<AccountingOptions> options,
    ILogger<ViaSarrafAccountingAdapter> logger)
    : IViaSarrafAccountingAdapter
{
    public const string SourceModule = "ViaSarrafSupplierPayment";
    public const string SourceEntityType = nameof(LedgerEntry);

    private readonly AccountingOptions _options = options.Value;

    public async Task<ViaSarrafAccountingResult> TryPostSupplierPaymentAsync(
        ViaSarrafSupplierPaymentEvent paymentEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paymentEvent);

        var (companyId, skipReason) = await ResolveCompanyAndSkipReasonAsync(paymentEvent, cancellationToken);
        if (skipReason is not null)
        {
            LogOutcome(paymentEvent, companyId, 0m, PaymentPostingStatus.Skipped, skipReason);
            return new ViaSarrafAccountingResult(PaymentPostingStatus.Skipped, null, skipReason);
        }

        var sourceEventId = BuildCreatedSourceEventId(paymentEvent.SupplierLedgerEntryId);
        var existing = await FindJournalAsync(companyId, sourceEventId, cancellationToken);
        if (existing is not null)
        {
            LogOutcome(paymentEvent, companyId, existing.Lines.Sum(x => x.Debit),
                PaymentPostingStatus.Duplicate, "DUPLICATE_SOURCE_EVENT");
            return new ViaSarrafAccountingResult(
                PaymentPostingStatus.Duplicate, existing, "DUPLICATE_SOURCE_EVENT");
        }

        var settings = await db.AccountingSettings
            .AsNoTracking()
            .SingleAsync(x => x.CompanyId == companyId, cancellationToken);

        var request = new AccountingPostRequest(
            companyId,
            journalNumberGenerator.ForViaSarrafSupplierPayment(companyId, paymentEvent.SupplierLedgerEntryId),
            paymentEvent.PaymentDate.Date,
            paymentEvent.PaymentDate.Date,
            paymentEvent.PaymentDate.Date,
            SourceModule,
            [
                new AccountingPostLine(
                    settings.AccountsPayableAccountId,
                    Debit: paymentEvent.AmountUsd,
                    Credit: 0m,
                    paymentEvent.Currency,
                    paymentEvent.Amount,
                    paymentEvent.FxRateToUsd,
                    AccountingPartyType.Supplier,
                    paymentEvent.SupplierId,
                    ContractId: paymentEvent.ContractId,
                    Description: "Supplier payable settled by sarraf"),
                new AccountingPostLine(
                    settings.AccountsPayableAccountId,
                    Debit: 0m,
                    Credit: paymentEvent.AmountUsd,
                    paymentEvent.Currency,
                    paymentEvent.Amount,
                    paymentEvent.FxRateToUsd,
                    AccountingPartyType.Sarraf,
                    paymentEvent.SarrafId,
                    ContractId: paymentEvent.ContractId,
                    Description: "Payable to sarraf for supplier payment")
            ],
            SourceEventId: sourceEventId,
            SourceEntityType: SourceEntityType,
            SourceEntityId: paymentEvent.SupplierLedgerEntryId,
            Description: $"Via-sarraf supplier payment on {paymentEvent.PaymentDate:yyyy-MM-dd}");

        try
        {
            var journal = await postingService.PostAsync(request, cancellationToken);
            LogOutcome(paymentEvent, companyId, journal.Lines.Sum(x => x.Debit),
                PaymentPostingStatus.Posted, null);
            return new ViaSarrafAccountingResult(PaymentPostingStatus.Posted, journal, null);
        }
        catch (Exception exception)
        {
            LogFailure(paymentEvent, exception);
            throw;
        }
    }

    public static string BuildCreatedSourceEventId(int supplierLedgerEntryId)
        => $"ViaSarrafSupplierPayment:{supplierLedgerEntryId}:Created";

    private async Task<(int CompanyId, string? SkipReason)> ResolveCompanyAndSkipReasonAsync(
        ViaSarrafSupplierPaymentEvent paymentEvent,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return (0, "ACCOUNTING_DISABLED");
        if (!_options.Pilots.SarrafPayment)
            return (0, "PILOT_DISABLED");
        if (paymentEvent.SupplierId <= 0 || paymentEvent.SarrafId <= 0)
            return (0, "PARTY_MISSING");
        if (paymentEvent.Amount <= 0m || paymentEvent.AmountUsd <= 0m)
            return (0, "INVALID_PAYMENT_AMOUNT");
        if (paymentEvent.FxRateToUsd <= 0m)
            return (0, "INVALID_PAYMENT_FX");
        if (SystemCurrency.IsBaseCurrency(paymentEvent.Currency) && paymentEvent.FxRateToUsd != 1m)
            return (0, "INVALID_PAYMENT_FX");

        var expectedUsd = decimal.Round(
            paymentEvent.Amount * paymentEvent.FxRateToUsd,
            4,
            MidpointRounding.AwayFromZero);
        if (paymentEvent.AmountUsd != expectedUsd)
            return (0, "INVALID_PAYMENT_CONVERSION");

        // The contract is the only provable company source for this flow.
        if (!paymentEvent.ContractId.HasValue)
            return (0, "PAYMENT_COMPANY_UNKNOWN");
        var companyId = await db.Contracts
            .AsNoTracking()
            .Where(x => x.Id == paymentEvent.ContractId.Value)
            .Select(x => (int?)x.CompanyId)
            .SingleOrDefaultAsync(cancellationToken);
        if (companyId is null)
            return (0, "PAYMENT_CONTRACT_NOT_FOUND");

        var settings = await db.AccountingSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.CompanyId == companyId.Value, cancellationToken);
        if (settings is null)
            return (companyId.Value, "ACCOUNTING_SETTINGS_MISSING");
        if (!string.Equals(settings.FunctionalCurrencyCode?.Trim(), "USD", StringComparison.OrdinalIgnoreCase))
            return (companyId.Value, "UNSUPPORTED_FUNCTIONAL_CURRENCY");
        if (settings.AccountsPayableAccountId <= 0)
            return (companyId.Value, "ACCOUNTING_SETTINGS_INCOMPLETE");

        var payableIsValid = await db.Accounts.AsNoTracking().AnyAsync(
            x => x.Id == settings.AccountsPayableAccountId
                && x.CompanyId == companyId.Value
                && x.IsActive,
            cancellationToken);
        return payableIsValid ? (companyId.Value, null) : (companyId.Value, "ACCOUNTS_PAYABLE_INVALID");
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

    private void LogOutcome(
        ViaSarrafSupplierPaymentEvent paymentEvent,
        int companyId,
        decimal journalDebitTotal,
        PaymentPostingStatus status,
        string? reason)
    {
        // Legacy writes two balanced rows of AmountUsd each; the journal must debit the same total.
        logger.LogInformation(
            "Via-sarraf supplier payment pilot comparison: SupplierLedgerEntryId {SupplierLedgerEntryId}, CompanyId {CompanyId}, ContractId {ContractId}, SupplierId {SupplierId}, SarrafId {SarrafId}, LegacyAmountUsd {LegacyAmountUsd}, JournalDebitTotal {JournalDebitTotal}, Difference {Difference}, PostingStatus {PostingStatus}, SkipOrFailureReason {SkipOrFailureReason}",
            paymentEvent.SupplierLedgerEntryId,
            companyId,
            paymentEvent.ContractId,
            paymentEvent.SupplierId,
            paymentEvent.SarrafId,
            paymentEvent.AmountUsd,
            journalDebitTotal,
            journalDebitTotal - paymentEvent.AmountUsd,
            status,
            reason);
    }

    private void LogFailure(ViaSarrafSupplierPaymentEvent paymentEvent, Exception exception)
    {
        var failureReason = exception is AccountingValidationException validation
            ? validation.Code
            : exception.GetType().Name;
        logger.LogError(
            exception,
            "Via-sarraf supplier payment accounting pilot posting failed for SupplierLedgerEntryId {SupplierLedgerEntryId} with FailureReason {FailureReason}",
            paymentEvent.SupplierLedgerEntryId,
            failureReason);
    }
}
