using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public enum ContractBalanceTransferPostingStatus
{
    Skipped = 0,
    Posted = 1,
    Duplicate = 2
}

public sealed record ContractBalanceTransferAccountingResult(
    ContractBalanceTransferPostingStatus Status,
    JournalEntry? Journal,
    string? Reason);

public interface IContractBalanceTransferAccountingAdapter
{
    Task<ContractBalanceTransferAccountingResult> TryPostAsync(
        ContractBalanceTransfer transfer,
        Contract sourceContract,
        Contract destinationContract,
        CancellationToken cancellationToken = default);
}

public sealed class ContractBalanceTransferAccountingAdapter(
    ApplicationDbContext db,
    IAccountingPostingService postingService,
    IAccountingJournalNumberGenerator journalNumberGenerator,
    IOptions<AccountingOptions> options,
    ILogger<ContractBalanceTransferAccountingAdapter> logger)
    : IContractBalanceTransferAccountingAdapter
{
    public const string SourceModule = "ContractBalanceTransfer";
    public const string SourceEntityType = "ContractBalanceTransfer";

    private readonly AccountingOptions _options = options.Value;

    public async Task<ContractBalanceTransferAccountingResult> TryPostAsync(
        ContractBalanceTransfer transfer,
        Contract sourceContract,
        Contract destinationContract,
        CancellationToken cancellationToken = default)
    {
        var skipReason = await GetSkipReasonAsync(
            transfer,
            sourceContract,
            destinationContract,
            cancellationToken);
        if (skipReason is not null)
        {
            LogComparison(
                transfer,
                sourceContract,
                destinationContract,
                companyId: sourceContract.CompanyId,
                supplierId: sourceContract.SupplierId,
                journalDebit: 0m,
                journalCredit: 0m,
                status: ContractBalanceTransferPostingStatus.Skipped.ToString(),
                reason: skipReason);
            return new ContractBalanceTransferAccountingResult(
                ContractBalanceTransferPostingStatus.Skipped,
                null,
                skipReason);
        }

        var sourceEventId = BuildSourceEventId(transfer.Id);
        var existing = await db.JournalEntries
            .AsNoTracking()
            .Include(x => x.Lines)
            .SingleOrDefaultAsync(
                x => x.CompanyId == sourceContract.CompanyId
                    && x.SourceModule == SourceModule
                    && x.SourceEventId == sourceEventId,
                cancellationToken);
        if (existing is not null)
        {
            LogComparison(
                transfer,
                sourceContract,
                destinationContract,
                sourceContract.CompanyId,
                sourceContract.SupplierId,
                existing.Lines.Sum(x => x.Debit),
                existing.Lines.Sum(x => x.Credit),
                ContractBalanceTransferPostingStatus.Duplicate.ToString(),
                "DUPLICATE_SOURCE_EVENT");
            return new ContractBalanceTransferAccountingResult(
                ContractBalanceTransferPostingStatus.Duplicate,
                existing,
                "DUPLICATE_SOURCE_EVENT");
        }

        var settings = await db.AccountingSettings
            .AsNoTracking()
            .SingleAsync(x => x.CompanyId == sourceContract.CompanyId, cancellationToken);
        var supplierId = sourceContract.SupplierId!.Value;

        var request = new AccountingPostRequest(
            sourceContract.CompanyId,
            journalNumberGenerator.ForContractBalanceTransfer(sourceContract.CompanyId, transfer.Id),
            transfer.TransferDate.Date,
            transfer.TransferDate.Date,
            transfer.TransferDate.Date,
            SourceModule,
            [
                new AccountingPostLine(
                    settings.AccountsPayableAccountId,
                    Debit: transfer.AmountUsd,
                    Credit: 0m,
                    transfer.CurrencyCode,
                    transfer.AmountOriginal,
                    transfer.FxRateToUsd,
                    AccountingPartyType.Supplier,
                    supplierId,
                    ContractId: sourceContract.Id,
                    Description: $"Balance transfer from contract {sourceContract.ContractNumber}"),
                new AccountingPostLine(
                    settings.AccountsPayableAccountId,
                    Debit: 0m,
                    Credit: transfer.AmountUsd,
                    transfer.CurrencyCode,
                    transfer.AmountOriginal,
                    transfer.FxRateToUsd,
                    AccountingPartyType.Supplier,
                    supplierId,
                    ContractId: destinationContract.Id,
                    Description: $"Balance transfer to contract {destinationContract.ContractNumber}")
            ],
            SourceEventId: sourceEventId,
            SourceEntityType: SourceEntityType,
            SourceEntityId: transfer.Id,
            Description: $"Contract balance transfer {sourceContract.ContractNumber} -> {destinationContract.ContractNumber}");

        try
        {
            var journal = await postingService.PostAsync(request, cancellationToken);
            LogComparison(
                transfer,
                sourceContract,
                destinationContract,
                sourceContract.CompanyId,
                supplierId,
                journal.Lines.Sum(x => x.Debit),
                journal.Lines.Sum(x => x.Credit),
                ContractBalanceTransferPostingStatus.Posted.ToString(),
                null);
            return new ContractBalanceTransferAccountingResult(
                ContractBalanceTransferPostingStatus.Posted,
                journal,
                null);
        }
        catch (Exception exception)
        {
            var failureReason = exception is AccountingValidationException validation
                ? validation.Code
                : exception.GetType().Name;
            LogComparison(
                transfer,
                sourceContract,
                destinationContract,
                sourceContract.CompanyId,
                supplierId,
                journalDebit: 0m,
                journalCredit: 0m,
                status: "Failure",
                reason: failureReason);
            logger.LogError(
                exception,
                "Contract balance transfer accounting pilot posting failed for TransferId {TransferId} with FailureReason {FailureReason}",
                transfer.Id,
                failureReason);
            throw;
        }
    }

    public static string BuildSourceEventId(int transferId)
        => $"ContractBalanceTransfer:{transferId}:Created";

    private async Task<string?> GetSkipReasonAsync(
        ContractBalanceTransfer transfer,
        Contract sourceContract,
        Contract destinationContract,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return "ACCOUNTING_DISABLED";
        if (!_options.Pilots.ContractBalanceTransfer)
            return "PILOT_DISABLED";
        if (sourceContract.ContractType != ContractType.Purchase
            || destinationContract.ContractType != ContractType.Purchase)
            return "CONTRACT_TYPE_NOT_PURCHASE";
        if (sourceContract.CompanyId != destinationContract.CompanyId)
            return "COMPANY_MISMATCH";
        if (!sourceContract.SupplierId.HasValue
            || sourceContract.SupplierId != destinationContract.SupplierId)
            return "SUPPLIER_MISMATCH";
        if (!CodesEqual(sourceContract.Currency, destinationContract.Currency))
            return "CONTRACT_CURRENCY_MISMATCH";
        if (!CodesEqual(sourceContract.SettlementCurrencyCode, destinationContract.SettlementCurrencyCode))
            return "SETTLEMENT_CURRENCY_MISMATCH";
        if (transfer.AmountOriginal <= 0m || transfer.AmountUsd <= 0m)
            return "INVALID_TRANSFER_AMOUNT";

        var settings = await db.AccountingSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.CompanyId == sourceContract.CompanyId, cancellationToken);
        if (settings is null)
            return "ACCOUNTING_SETTINGS_MISSING";
        if (!string.Equals(settings.FunctionalCurrencyCode?.Trim(), "USD", StringComparison.OrdinalIgnoreCase))
            return "UNSUPPORTED_FUNCTIONAL_CURRENCY";

        var configuredAccountIds = GetConfiguredAccountIds(settings);
        if (configuredAccountIds.Any(x => x <= 0)
            || configuredAccountIds.Distinct().Count() != configuredAccountIds.Length)
            return "ACCOUNTING_SETTINGS_INCOMPLETE";

        var validAccountCount = await db.Accounts.AsNoTracking().CountAsync(
            x => configuredAccountIds.Contains(x.Id)
                && x.CompanyId == sourceContract.CompanyId
                && x.IsActive,
            cancellationToken);
        if (validAccountCount != configuredAccountIds.Length)
            return "ACCOUNTING_SETTINGS_INVALID_ACCOUNTS";

        var payableIsValid = await db.Accounts.AsNoTracking().AnyAsync(
            x => x.Id == settings.AccountsPayableAccountId
                && x.CompanyId == sourceContract.CompanyId
                && x.IsActive,
            cancellationToken);
        return payableIsValid ? null : "ACCOUNTS_PAYABLE_INVALID";
    }

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

    private static bool CodesEqual(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private void LogComparison(
        ContractBalanceTransfer transfer,
        Contract sourceContract,
        Contract destinationContract,
        int companyId,
        int? supplierId,
        decimal journalDebit,
        decimal journalCredit,
        string status,
        string? reason)
    {
        var legacyDebit = transfer.AmountUsd;
        var legacyCredit = transfer.AmountUsd;
        var difference = journalDebit - legacyDebit;
        logger.LogInformation(
            "Contract balance transfer pilot comparison: TransferId {TransferId}, CompanyId {CompanyId}, SourceContractId {SourceContractId}, DestinationContractId {DestinationContractId}, SupplierId {SupplierId}, LegacyDebitTotal {LegacyDebitTotal}, LegacyCreditTotal {LegacyCreditTotal}, JournalDebitTotal {JournalDebitTotal}, JournalCreditTotal {JournalCreditTotal}, Difference {Difference}, PostingStatus {PostingStatus}, SkipOrFailureReason {SkipOrFailureReason}",
            transfer.Id,
            companyId,
            sourceContract.Id,
            destinationContract.Id,
            supplierId,
            legacyDebit,
            legacyCredit,
            journalDebit,
            journalCredit,
            difference,
            status,
            reason);
    }
}
