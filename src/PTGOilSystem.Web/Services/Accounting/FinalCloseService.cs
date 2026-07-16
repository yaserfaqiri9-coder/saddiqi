using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public interface IFinalCloseService
{
    Task<FinalClosePrecheck?> PrecheckAsync(int companyId, int fiscalYearId, CancellationToken cancellationToken = default);

    Task<FinalCloseResult> CloseAsync(
        int companyId, int fiscalYearId, int? userId, string confirmation,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// مرحله ۱۴ — Final Close: عملیاتِ صریح، اتمیک و غیرقابل‌تکرارِ ناخواسته.
///
/// ترتیب اتمیک داخل یک Transaction: بستنِ حساب‌های درآمد/هزینه به Current Year Earnings، سپس
/// انتقال به Retained Earnings، سپس HardLockِ همهٔ دوره‌ها، سپس بستنِ سال و جاری‌کردنِ سالِ بعد.
/// هر شکستی کل عملیات را Rollback می‌کند.
///
/// **تصمیمِ Opening Balance (از روی کد گزارش‌گیری):** دفتر کل جدید *پیوسته/تجمعی* است — هیچ
/// گزارشی مانده را per-fiscal-year بازنشانی نمی‌کند و مانده از جمعِ تجمعیِ سطرهای Posted به‌دست
/// می‌آید. بنابراین ساختِ Opening Journal، حساب‌های ترازنامه‌ای را **دوبار** می‌شمرد. پس Opening
/// Journal ساخته نمی‌شود؛ فقط P&L به Equity بسته می‌شود و حساب‌های ترازنامه‌ای خودبه‌خود به سال
/// بعد منتقل می‌شوند.
/// </summary>
public sealed class FinalCloseService(
    ApplicationDbContext db,
    IClosingChecklistService checklist,
    ITrialCloseService trialClose,
    IAccountingPostingService posting,
    IAuditService? audit = null) : IFinalCloseService
{
    public const string SourceModule = TrialCloseService.SourceModule;

    public async Task<FinalClosePrecheck?> PrecheckAsync(
        int companyId, int fiscalYearId, CancellationToken cancellationToken = default)
    {
        var year = await db.FiscalYears.AsNoTracking()
            .SingleOrDefaultAsync(y => y.Id == fiscalYearId && y.CompanyId == companyId, cancellationToken);
        if (year is null)
            return null;

        var blockers = new List<string>();

        if (year.Status is not (FiscalYearStatus.Open or FiscalYearStatus.Reopened))
            blockers.Add(FinalCloseReasons.InvalidState);

        var report = await checklist.BuildAsync(companyId, fiscalYearId, cancellationToken);
        if (report?.HasBlockers == true)
            blockers.Add(FinalCloseReasons.ChecklistBlocked);

        // Trial Close موفق و تازه.
        var trialRun = await db.FiscalYearCloseRuns.AsNoTracking()
            .Where(r => r.CompanyId == companyId && r.FiscalYearId == fiscalYearId
                && r.RunType == FiscalYearCloseRunType.Trial
                && r.Status == FiscalYearCloseRunStatus.Completed)
            .OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (trialRun is null)
            blockers.Add(FinalCloseReasons.TrialCloseMissing);
        else
        {
            var cutoff = trialRun.LastJournalEntryId ?? 0;
            var newerOperational = await db.JournalEntries.AsNoTracking()
                .AnyAsync(j => j.CompanyId == companyId && j.FiscalYearId == fiscalYearId
                    && j.Status == JournalEntryStatus.Posted
                    && j.SourceModule != SourceModule
                    && j.Id > cutoff, cancellationToken);
            if (newerOperational)
                blockers.Add(FinalCloseReasons.TrialCloseStale);
        }

        // تسعیر Complete یا NotApplicable.
        var preview = await trialClose.PreviewAsync(companyId, fiscalYearId, cancellationToken);
        if (preview is not null)
        {
            if (preview.MissingRates.Count > 0)
                blockers.Add(FinalCloseReasons.RevaluationIncomplete);
            foreach (var group in preview.Revaluations)
            {
                var applied = await db.JournalEntries.AsNoTracking().AnyAsync(j =>
                    j.CompanyId == companyId && j.SourceModule == SourceModule
                    && j.SourceEventId != null
                    && j.SourceEventId.StartsWith($"FiscalYearRevaluation:{fiscalYearId}:{group.Currency}:")
                    && j.Status == JournalEntryStatus.Posted, cancellationToken);
                if (!applied)
                {
                    blockers.Add(FinalCloseReasons.RevaluationIncomplete);
                    break;
                }
            }
        }

        var settings = await db.AccountingSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);
        if (settings is null || settings.CurrentYearProfitLossAccountId <= 0)
            blockers.Add(FinalCloseReasons.CurrentYearEarningsAccountMissing);
        if (settings is null || settings.RetainedEarningsAccountId <= 0)
            blockers.Add(FinalCloseReasons.RetainedEarningsAccountMissing);

        var (nextYear, nextBlockers) = await ValidateNextYearAsync(companyId, year, cancellationToken);
        blockers.AddRange(nextBlockers);

        var (revenue, expense, net) = await ComputeProfitAsync(companyId, fiscalYearId, year.EndDate.Date, cancellationToken);

        return new FinalClosePrecheck(
            blockers.Count == 0, blockers, revenue, expense, net,
            nextYear?.Id, nextYear?.Name);
    }

    private async Task<(FiscalYear? Next, IReadOnlyList<string> Blockers)> ValidateNextYearAsync(
        int companyId, FiscalYear year, CancellationToken cancellationToken)
    {
        var blockers = new List<string>();
        var nextStart = year.EndDate.Date.AddDays(1);
        var next = await db.FiscalYears.AsNoTracking()
            .Where(y => y.CompanyId == companyId && y.StartDate == nextStart)
            .OrderBy(y => y.Id).FirstOrDefaultAsync(cancellationToken);

        if (next is null)
        {
            blockers.Add(FinalCloseReasons.NextYearMissing);
            return (null, blockers);
        }

        if (next.EndDate.Date <= next.StartDate.Date)
            blockers.Add(FinalCloseReasons.NextYearInvalidDates);

        var hasPeriods = await db.FiscalPeriods.AsNoTracking()
            .AnyAsync(p => p.FiscalYearId == next.Id, cancellationToken);
        if (!hasPeriods)
            blockers.Add(FinalCloseReasons.NextYearNoPeriods);

        return (next, blockers);
    }

    // درآمد (Cr−Dr) و هزینه (Dr−Cr) از سطرهای Posted تا EndDate، بر اساس نوعِ صریحِ حساب.
    private async Task<(decimal Revenue, decimal Expense, decimal Net)> ComputeProfitAsync(
        int companyId, int fiscalYearId, DateTime endDate, CancellationToken cancellationToken)
    {
        var lines = await db.JournalEntryLines.AsNoTracking()
            .Where(l => l.JournalEntry!.CompanyId == companyId
                && l.JournalEntry.FiscalYearId == fiscalYearId
                && l.JournalEntry.Status == JournalEntryStatus.Posted
                && l.JournalEntry.AccountingDate <= endDate
                && (l.Account!.AccountType == AccountType.Revenue || l.Account.AccountType == AccountType.Expense))
            .Select(l => new { l.Account!.AccountType, l.Debit, l.Credit })
            .ToListAsync(cancellationToken);

        var revenue = lines.Where(l => l.AccountType == AccountType.Revenue).Sum(l => l.Credit - l.Debit);
        var expense = lines.Where(l => l.AccountType == AccountType.Expense).Sum(l => l.Debit - l.Credit);
        return (revenue, expense, revenue - expense);
    }

    public async Task<FinalCloseResult> CloseAsync(
        int companyId, int fiscalYearId, int? userId, string confirmation,
        CancellationToken cancellationToken = default)
    {
        var year = await db.FiscalYears
            .SingleOrDefaultAsync(y => y.Id == fiscalYearId && y.CompanyId == companyId, cancellationToken);
        if (year is null)
            return FinalCloseResult.Fail(FinalCloseReasons.FiscalYearNotFound, "سال مالی یافت نشد.");

        if (year.Status == FiscalYearStatus.Closed)
            return new FinalCloseResult(FinalCloseResultStatus.AlreadyClosed, null, null,
                "سال از قبل بسته است.", Array.Empty<int>(), Array.Empty<string>());

        if (!string.Equals(confirmation?.Trim(), year.Name?.Trim(), StringComparison.Ordinal))
            return FinalCloseResult.Fail(FinalCloseReasons.ConfirmationInvalid,
                "عبارت تأیید باید دقیقاً برابر کد سال مالی باشد.");

        var precheck = await PrecheckAsync(companyId, fiscalYearId, cancellationToken);
        if (precheck is null)
            return FinalCloseResult.Fail(FinalCloseReasons.FiscalYearNotFound, "سال مالی یافت نشد.");
        if (!precheck.CanClose)
            return FinalCloseResult.Fail(FinalCloseReasons.ChecklistBlocked,
                "پیش‌شرط‌های Final Close برقرار نیست.", precheck.Blockers);

        var settings = await db.AccountingSettings.AsNoTracking()
            .SingleAsync(s => s.CompanyId == companyId, cancellationToken);
        var endDate = year.EndDate.Date;
        var revision = await db.FiscalYearCloseRuns.AsNoTracking()
            .CountAsync(r => r.CompanyId == companyId && r.FiscalYearId == fiscalYearId
                && r.RunType == FiscalYearCloseRunType.Final, cancellationToken);

        var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            var closingJournalIds = new List<int>();

            var pnlJournal = await PostProfitAndLossClosingAsync(
                companyId, fiscalYearId, endDate, revision, settings, userId, cancellationToken);
            if (pnlJournal is not null)
                closingJournalIds.Add(pnlJournal.Id);

            var reJournal = await PostRetainedEarningsTransferAsync(
                companyId, fiscalYearId, endDate, revision, settings, precheck.NetProfitUsd, userId, cancellationToken);
            if (reJournal is not null)
                closingJournalIds.Add(reJournal.Id);

            // HardLock همهٔ دوره‌ها — بعد از پست‌شدنِ سندهای بستن.
            var periods = await db.FiscalPeriods
                .Where(p => p.FiscalYearId == fiscalYearId).ToListAsync(cancellationToken);
            foreach (var period in periods)
            {
                period.Status = FiscalPeriodStatus.HardLocked;
                period.LockedAt = DateTime.UtcNow;
                period.LockedByUserId = userId;
            }

            year.Status = FiscalYearStatus.Closed;
            year.ClosedAt = DateTime.UtcNow;
            year.ClosedByUserId = userId;
            year.IsCurrent = false;
            if (pnlJournal is not null)
                year.ClosingJournalEntryId = pnlJournal.Id;

            var nextYear = await db.FiscalYears
                .SingleAsync(y => y.Id == precheck.NextFiscalYearId, cancellationToken);
            nextYear.IsCurrent = true;
            if (nextYear.Status == FiscalYearStatus.Draft)
                nextYear.Status = FiscalYearStatus.Open;

            var report = await checklist.BuildAsync(companyId, fiscalYearId, cancellationToken);
            var run = new FiscalYearCloseRun
            {
                CompanyId = companyId,
                FiscalYearId = fiscalYearId,
                RunType = FiscalYearCloseRunType.Final,
                Revision = revision,
                Status = FiscalYearCloseRunStatus.Completed,
                StartedAt = DateTime.UtcNow,
                StartedByUserId = userId,
                CompletedAt = DateTime.UtcNow,
                CompletedByUserId = userId,
                ClosingJournalEntryId = pnlJournal?.Id,
                ChecklistSnapshotJson = JsonSerializer.Serialize(report),
                DebitTotal = precheck.RevenueUsd,
                CreditTotal = precheck.ExpenseUsd,
                SourceDataCutoff = endDate,
                RevaluationJournalIdsJson = JsonSerializer.Serialize(closingJournalIds)
            };
            db.FiscalYearCloseRuns.Add(run);

            await db.SaveChangesAsync(cancellationToken);

            if (audit is not null)
            {
                await audit.LogAsync(nameof(FiscalYear), fiscalYearId, AuditAction.Approve, userId,
                    JsonSerializer.Serialize(new
                    {
                        Action = "FinalClose",
                        companyId, fiscalYearId, revision,
                        precheck.NetProfitUsd, ClosingJournalIds = closingJournalIds,
                        NextFiscalYearId = nextYear.Id
                    }), cancellationToken);
            }

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            return new FinalCloseResult(FinalCloseResultStatus.Succeeded, run.Id, null, null,
                closingJournalIds, Array.Empty<string>());
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

    // سندِ بستنِ P&L: هر حساب درآمد/هزینه به صفر می‌رسد و خالص به Current Year Earnings می‌رود.
    private async Task<JournalEntry?> PostProfitAndLossClosingAsync(
        int companyId, int fiscalYearId, DateTime endDate, int revision,
        AccountingSettings settings, int? userId, CancellationToken cancellationToken)
    {
        var perAccount = await db.JournalEntryLines.AsNoTracking()
            .Where(l => l.JournalEntry!.CompanyId == companyId
                && l.JournalEntry.FiscalYearId == fiscalYearId
                && l.JournalEntry.Status == JournalEntryStatus.Posted
                && l.JournalEntry.AccountingDate <= endDate
                && (l.Account!.AccountType == AccountType.Revenue || l.Account.AccountType == AccountType.Expense))
            .GroupBy(l => l.AccountId)
            .Select(g => new { AccountId = g.Key, Net = g.Sum(x => x.Debit - x.Credit) })
            .ToListAsync(cancellationToken);

        var offsets = perAccount
            .Select(a => new { a.AccountId, Net = decimal.Round(a.Net, 4, MidpointRounding.AwayFromZero) })
            .Where(a => a.Net != 0m)
            .ToList();

        if (offsets.Count == 0)
            return null;

        var lines = new List<AccountingPostLine>();
        foreach (var a in offsets)
        {
            // خالصِ حساب را با سطرِ معکوس صفر می‌کنیم: خالصِ بدهکار → بستانکار، و برعکس.
            var amount = Math.Abs(a.Net);
            var credit = a.Net > 0m;
            lines.Add(new AccountingPostLine(a.AccountId,
                credit ? 0m : amount, credit ? amount : 0m,
                "USD", amount, 1m, Description: "P&L closing"));
        }

        // net = Σoffset(Dr−Cr) = سود؛ خطِ Current Year Earnings آن را متوازن می‌کند.
        var net = offsets.Sum(a => -a.Net); // = R − E = سود
        if (net != 0m)
        {
            var amount = Math.Abs(net);
            var creditCye = net > 0m; // سود → بستانکارِ CYE
            lines.Add(new AccountingPostLine(settings.CurrentYearProfitLossAccountId,
                creditCye ? 0m : amount, creditCye ? amount : 0m,
                "USD", amount, 1m, Description: "Current Year Earnings"));
        }

        return await posting.PostAsync(new AccountingPostRequest(
            companyId,
            $"CLOSE-PNL-{fiscalYearId}-{revision}",
            endDate, endDate, endDate,
            SourceModule,
            lines,
            SourceEventId: $"FiscalYearClose:{fiscalYearId}:ProfitAndLoss:{revision}",
            SourceEntityType: nameof(FiscalYear),
            SourceEntityId: fiscalYearId,
            Description: $"بستن درآمد و هزینه سال {fiscalYearId} (Revision {revision})",
            IsClosing: true,
            PostedByUserId: userId), cancellationToken);
    }

    // انتقالِ Current Year Earnings به Retained Earnings.
    private async Task<JournalEntry?> PostRetainedEarningsTransferAsync(
        int companyId, int fiscalYearId, DateTime endDate, int revision,
        AccountingSettings settings, decimal netProfit, int? userId, CancellationToken cancellationToken)
    {
        var net = decimal.Round(netProfit, 4, MidpointRounding.AwayFromZero);
        if (net == 0m)
            return null;

        var amount = Math.Abs(net);
        // سود: CYE بستانکار شده بود → حالا بدهکارش می‌کنیم و Retained Earnings را بستانکار.
        var profit = net > 0m;
        var lines = new List<AccountingPostLine>
        {
            new(settings.CurrentYearProfitLossAccountId,
                profit ? amount : 0m, profit ? 0m : amount,
                "USD", amount, 1m, Description: "Current Year Earnings transfer"),
            new(settings.RetainedEarningsAccountId,
                profit ? 0m : amount, profit ? amount : 0m,
                "USD", amount, 1m, Description: "Retained Earnings")
        };

        return await posting.PostAsync(new AccountingPostRequest(
            companyId,
            $"CLOSE-RE-{fiscalYearId}-{revision}",
            endDate, endDate, endDate,
            SourceModule,
            lines,
            SourceEventId: $"FiscalYearClose:{fiscalYearId}:RetainedEarnings:{revision}",
            SourceEntityType: nameof(FiscalYear),
            SourceEntityId: fiscalYearId,
            Description: $"انتقال سود/زیان به سود انباشته سال {fiscalYearId} (Revision {revision})",
            IsClosing: true,
            PostedByUserId: userId), cancellationToken);
    }
}
