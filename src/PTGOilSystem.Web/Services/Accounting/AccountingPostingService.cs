using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public interface IAccountingPostingService
{
    Task<JournalEntry> PostAsync(
        AccountingPostRequest request,
        CancellationToken cancellationToken = default);

    Task<JournalEntry> ReverseAsync(
        AccountingReversalRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class AccountingPostingService(
    ApplicationDbContext db,
    IPeriodGuard periodGuard,
    IOptions<AccountingOptions> options,
    ISystemCompanyProvider systemCompany) : IAccountingPostingService
{
    private readonly AccountingOptions _options = options.Value;

    public Task<JournalEntry> PostAsync(
        AccountingPostRequest request,
        CancellationToken cancellationToken = default)
        => PostInternalAsync(request, reversalOfJournalEntryId: null, cancellationToken);

    public async Task<JournalEntry> ReverseAsync(
        AccountingReversalRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        var original = await db.JournalEntries
            .AsNoTracking()
            .Include(x => x.Lines.OrderBy(line => line.LineNumber))
            .SingleOrDefaultAsync(x => x.Id == request.JournalEntryId, cancellationToken)
            ?? throw new AccountingValidationException("JOURNAL_NOT_FOUND", "The journal was not found.");

        if (original.Status != JournalEntryStatus.Posted)
        {
            throw new AccountingValidationException(
                "JOURNAL_NOT_POSTED",
                "Only a posted journal can be reversed.");
        }

        if (await db.JournalEntries.AsNoTracking().AnyAsync(
                x => x.ReversalOfJournalEntryId == original.Id
                    && x.Status == JournalEntryStatus.Posted,
                cancellationToken))
        {
            throw new AccountingValidationException(
                "JOURNAL_ALREADY_REVERSED",
                "The journal already has an official reversal.");
        }

        var lines = original.Lines.Select(line => new AccountingPostLine(
            line.AccountId,
            Debit: line.Credit,
            Credit: line.Debit,
            line.TransactionCurrencyCode,
            line.TransactionAmount,
            line.ExchangeRate,
            line.PartyType,
            line.PartyId,
            line.ContractId,
            line.ShipmentId,
            line.TankId,
            line.ProductId,
            line.CashAccountId,
            line.Description)).ToArray();

        var postRequest = new AccountingPostRequest(
            original.CompanyId,
            request.JournalNumber,
            request.AccountingDate,
            request.AccountingDate,
            request.AccountingDate,
            request.SourceModule,
            lines,
            request.SourceEventId,
            nameof(JournalEntry),
            original.Id,
            request.Description ?? $"Reversal of {original.JournalNumber}",
            PostedByUserId: request.PostedByUserId);

        return await PostInternalAsync(postRequest, original.Id, cancellationToken);
    }

    private async Task<JournalEntry> PostInternalAsync(
        AccountingPostRequest request,
        int? reversalOfJournalEntryId,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();
        ValidateRequestShape(request);
        await EnsureOwnerCompanyAsync(request.CompanyId, cancellationToken);

        var selection = await periodGuard.EnsurePostingAllowedAsync(
            request.CompanyId,
            request.AccountingDate,
            cancellationToken);

        var settings = await db.AccountingSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CompanyId == request.CompanyId, cancellationToken)
            ?? throw new AccountingValidationException(
                "ACCOUNTING_NOT_CONFIGURED",
                "Accounting settings have not been created for this company.");

        if (!string.IsNullOrWhiteSpace(request.SourceEventId)
            && await db.JournalEntries.AsNoTracking().AnyAsync(
                x => x.CompanyId == request.CompanyId
                    && x.SourceModule == request.SourceModule.Trim()
                    && x.SourceEventId == request.SourceEventId.Trim(),
                cancellationToken))
        {
            throw new AccountingValidationException(
                "DUPLICATE_SOURCE_EVENT",
                "This source event has already been posted.");
        }

        await ValidateAccountsAsync(request, cancellationToken);
        await ValidateCurrenciesAsync(request, settings.FunctionalCurrencyCode, cancellationToken);

        IDbContextTransaction? transaction = null;
        if (db.Database.IsRelational() && db.Database.CurrentTransaction is null)
            transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var journal = new JournalEntry
            {
                CompanyId = request.CompanyId,
                FiscalYearId = selection.FiscalYear.Id,
                FiscalPeriodId = selection.FiscalPeriod.Id,
                JournalNumber = request.JournalNumber.Trim(),
                Status = JournalEntryStatus.Draft,
                AccountingDate = request.AccountingDate.Date,
                DocumentDate = request.DocumentDate.Date,
                OperationDate = request.OperationDate.Date,
                Description = NormalizeOptional(request.Description),
                SourceModule = request.SourceModule.Trim(),
                SourceEntityType = NormalizeOptional(request.SourceEntityType),
                SourceEntityId = request.SourceEntityId,
                SourceEventId = NormalizeOptional(request.SourceEventId),
                IsOpening = request.IsOpening,
                IsClosing = request.IsClosing,
                IsAdjustment = request.IsAdjustment,
                IsReversal = reversalOfJournalEntryId.HasValue,
                ReversalOfJournalEntryId = reversalOfJournalEntryId,
                PostedByUserId = request.PostedByUserId,
                Lines = request.Lines.Select((line, index) => new JournalEntryLine
                {
                    LineNumber = index + 1,
                    AccountId = line.AccountId,
                    PartyType = line.PartyType,
                    PartyId = line.PartyId,
                    ContractId = line.ContractId,
                    ShipmentId = line.ShipmentId,
                    TankId = line.TankId,
                    ProductId = line.ProductId,
                    CashAccountId = line.CashAccountId,
                    Debit = line.Debit,
                    Credit = line.Credit,
                    TransactionCurrencyCode = line.TransactionCurrencyCode.Trim().ToUpperInvariant(),
                    TransactionAmount = line.TransactionAmount,
                    ExchangeRate = line.ExchangeRate,
                    Description = NormalizeOptional(line.Description)
                }).ToList()
            };

            db.JournalEntries.Add(journal);
            await db.SaveChangesAsync(cancellationToken);

            journal.Status = JournalEntryStatus.Posted;
            journal.PostedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            return journal;
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    /// <summary>
    /// گاردِ مرکزیِ مالک (Fail-Closed): هیچ سندی نباید به دفترِ شرکتی جز شرکتِ مالک بنشیند. چون همهٔ
    /// حساب‌ها، تنظیمات، سال و دورهٔ همین سند در ادامه با <c>request.CompanyId</c> اعتبارسنجی می‌شوند،
    /// تطبیقِ همین یک شناسه با مالک، کلِ سند را به شرکتِ مالک مقید می‌کند. اگر مالک صفر یا بیش از یک
    /// باشد، <see cref="ISystemCompanyProvider.GetOwnerCompanyIdAsync"/> با خطای پیکربندیِ واضح کلِ
    /// عملیات را متوقف می‌کند — گارد هرگز ساکت نمی‌ماند.
    /// </summary>
    private async Task EnsureOwnerCompanyAsync(int companyId, CancellationToken cancellationToken)
    {
        var ownerCompanyId = await systemCompany.GetOwnerCompanyIdAsync(cancellationToken);
        if (companyId != ownerCompanyId)
        {
            throw new AccountingValidationException(
                "COMPANY_NOT_OWNER",
                "The journal company is not the system owner company.");
        }
    }

    private void EnsureEnabled()
    {
        if (!_options.Enabled)
        {
            throw new AccountingValidationException(
                "ACCOUNTING_DISABLED",
                "The independent accounting core is disabled by configuration.");
        }
    }

    private static void ValidateRequestShape(AccountingPostRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.JournalNumber))
            throw new AccountingValidationException("INVALID_JOURNAL_NUMBER", "Journal number is required.");
        if (string.IsNullOrWhiteSpace(request.SourceModule))
            throw new AccountingValidationException("INVALID_SOURCE_MODULE", "Source module is required.");
        if (request.Lines.Count < 2)
            throw new AccountingValidationException("INVALID_LINES", "A journal requires at least two lines.");

        foreach (var line in request.Lines)
        {
            var hasDebit = line.Debit > 0m && line.Credit == 0m;
            var hasCredit = line.Credit > 0m && line.Debit == 0m;
            if (line.Debit < 0m || line.Credit < 0m || (!hasDebit && !hasCredit))
            {
                throw new AccountingValidationException(
                    "INVALID_DEBIT_CREDIT",
                    "Each line must contain exactly one positive debit or credit and no negative amount.");
            }

            if ((line.PartyType is null) != (line.PartyId is null))
                throw new AccountingValidationException("INVALID_PARTY", "Party type and party id must be supplied together.");
        }

        var debit = request.Lines.Sum(x => x.Debit);
        var credit = request.Lines.Sum(x => x.Credit);
        if (debit <= 0m || debit != credit)
        {
            throw new AccountingValidationException(
                "UNBALANCED_JOURNAL",
                "Total debit and total credit must be equal and positive.");
        }
    }

    private async Task ValidateAccountsAsync(
        AccountingPostRequest request,
        CancellationToken cancellationToken)
    {
        var accountIds = request.Lines.Select(x => x.AccountId).Distinct().ToArray();
        var validCount = await db.Accounts.AsNoTracking().CountAsync(
            x => accountIds.Contains(x.Id)
                && x.CompanyId == request.CompanyId
                && x.IsActive,
            cancellationToken);

        if (validCount != accountIds.Length)
        {
            throw new AccountingValidationException(
                "INVALID_ACCOUNT_OWNERSHIP",
                "All accounts must be active and belong to the journal company.");
        }
    }

    private async Task ValidateCurrenciesAsync(
        AccountingPostRequest request,
        string functionalCurrencyCode,
        CancellationToken cancellationToken)
    {
        var functionalCode = functionalCurrencyCode.Trim().ToUpperInvariant();
        if (!IsValidCurrencyCode(functionalCode))
        {
            throw new AccountingValidationException(
                "INVALID_FUNCTIONAL_CURRENCY",
                "The functional currency in accounting settings is invalid.");
        }

        var requestedCodes = request.Lines
            .Select(x => x.TransactionCurrencyCode.Trim().ToUpperInvariant())
            .Distinct()
            .ToArray();

        if (requestedCodes.Any(code => !IsValidCurrencyCode(code)))
            throw new AccountingValidationException("INVALID_CURRENCY", "A transaction currency code is invalid.");

        var configuredCurrencyCount = await db.Currencies.AsNoTracking().CountAsync(cancellationToken);
        if (configuredCurrencyCount > 0)
        {
            var activeCodes = await db.Currencies.AsNoTracking()
                .Where(x => x.IsActive && requestedCodes.Contains(x.Code.ToUpper()))
                .Select(x => x.Code.ToUpper())
                .Distinct()
                .ToListAsync(cancellationToken);

            if (activeCodes.Count != requestedCodes.Length)
                throw new AccountingValidationException("INVALID_CURRENCY", "A transaction currency is missing or inactive.");
        }

        foreach (var line in request.Lines)
        {
            if (line.TransactionAmount < 0m || line.ExchangeRate <= 0m)
                throw new AccountingValidationException("INVALID_CURRENCY_AMOUNT", "Transaction amount and exchange rate are invalid.");

            var functionalAmount = line.Debit + line.Credit;
            var convertedAmount = decimal.Round(
                line.TransactionAmount * line.ExchangeRate,
                4,
                MidpointRounding.AwayFromZero);

            if (functionalAmount != convertedAmount)
            {
                throw new AccountingValidationException(
                    "INVALID_CURRENCY_CONVERSION",
                    "Transaction amount multiplied by exchange rate must equal the functional debit or credit.");
            }

            if (string.Equals(line.TransactionCurrencyCode.Trim(), functionalCode, StringComparison.OrdinalIgnoreCase)
                && line.ExchangeRate != 1m)
            {
                throw new AccountingValidationException(
                    "INVALID_FUNCTIONAL_RATE",
                    "Exchange rate must be one when transaction and functional currencies are the same.");
            }
        }
    }

    private static bool IsValidCurrencyCode(string code)
        => code.Length is >= 2 and <= 10 && code.All(char.IsLetterOrDigit);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
