using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public interface IReopenFiscalYearService
{
    Task<ReopenPrecheck?> PrecheckAsync(int companyId, int fiscalYearId, CancellationToken cancellationToken = default);

    Task<ReopenResult> ReopenAsync(
        int companyId, int fiscalYearId, int? userId, string? reason, string confirmation,
        bool hasPermission, CancellationToken cancellationToken = default);
}

/// <summary>
/// مرحله ۱۵ — بازگشاییِ کنترل‌شده.
///
/// بازگشایی Final Close را حذف یا تاریخچه را پاک نمی‌کند: همهٔ آثارِ بستن فقط با **Reversal رسمی**
/// برمی‌گردند و هیچ سندی Delete یا ویرایش نمی‌شود. فقط آخرین سالِ بسته قابلِ بازگشایی است.
///
/// **دامنهٔ Reverse:** فقط سندهای Final Close (P&L و Retained Earnings). جفتِ Revaluation +
/// Auto-Reversal متعلق به Trial Close است و خودبه‌خود نت می‌شود؛ دست‌نخورده می‌ماند تا اجرای
/// دوبارهٔ Trial/Final با منطقِ Revisionِ مرحله ۱۳ آن را دوباره ارزیابی کند.
/// </summary>
public sealed class ReopenFiscalYearService(
    ApplicationDbContext db,
    IAccountingPostingService posting,
    IAuditService? audit = null) : IReopenFiscalYearService
{
    private const string SourceModule = TrialCloseService.SourceModule;

    public async Task<ReopenPrecheck?> PrecheckAsync(
        int companyId, int fiscalYearId, CancellationToken cancellationToken = default)
    {
        var year = await db.FiscalYears.AsNoTracking()
            .SingleOrDefaultAsync(y => y.Id == fiscalYearId && y.CompanyId == companyId, cancellationToken);
        if (year is null)
            return null;

        var blockers = new List<string>();

        if (year.Status != FiscalYearStatus.Closed)
            blockers.Add(ReopenReasons.NotClosed);

        // فقط آخرین سالِ بسته: هیچ سالِ بسته‌ای با EndDate بزرگ‌تر وجود نداشته باشد.
        var laterClosed = await db.FiscalYears.AsNoTracking()
            .AnyAsync(y => y.CompanyId == companyId && y.Id != fiscalYearId
                && y.Status == FiscalYearStatus.Closed && y.EndDate > year.EndDate, cancellationToken);
        if (laterClosed)
            blockers.Add(ReopenReasons.LaterClosedYearExists);

        var (run, closingIds, runBlockers) = await LoadActiveFinalCloseAsync(companyId, fiscalYearId, cancellationToken);
        blockers.AddRange(runBlockers);

        // سالِ بعد نباید Postingِ عملیاتی داشته باشد (سندهای سیستمیِ FiscalYearClose مانع نیستند).
        var nextStart = year.EndDate.Date.AddDays(1);
        var nextYearId = await db.FiscalYears.AsNoTracking()
            .Where(y => y.CompanyId == companyId && y.StartDate == nextStart)
            .Select(y => (int?)y.Id).FirstOrDefaultAsync(cancellationToken);
        if (nextYearId is int nid)
        {
            var hasOperational = await db.JournalEntries.AsNoTracking()
                .AnyAsync(j => j.CompanyId == companyId && j.FiscalYearId == nid
                    && j.Status == JournalEntryStatus.Posted
                    && j.SourceModule != SourceModule, cancellationToken);
            if (hasOperational)
                blockers.Add(ReopenReasons.NextYearHasOperationalPostings);
        }

        return new ReopenPrecheck(blockers.Count == 0, blockers, run?.Id, closingIds);
    }

    private async Task<(FiscalYearCloseRun? Run, IReadOnlyList<int> ClosingIds, IReadOnlyList<string> Blockers)>
        LoadActiveFinalCloseAsync(int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var blockers = new List<string>();
        var run = await db.FiscalYearCloseRuns.AsNoTracking()
            .Where(r => r.CompanyId == companyId && r.FiscalYearId == fiscalYearId
                && r.RunType == FiscalYearCloseRunType.Final && r.FailureCode == null)
            .OrderByDescending(r => r.Revision).ThenByDescending(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (run is null)
        {
            blockers.Add(ReopenReasons.FinalCloseRunNotFound);
            return (null, Array.Empty<int>(), blockers);
        }

        var ids = new List<int>();
        if (!string.IsNullOrEmpty(run.RevaluationJournalIdsJson))
            ids.AddRange(JsonSerializer.Deserialize<List<int>>(run.RevaluationJournalIdsJson) ?? new());
        if (run.ClosingJournalEntryId is int cj)
            ids.Add(cj);
        ids = ids.Distinct().ToList();

        if (ids.Count == 0)
            blockers.Add(ReopenReasons.FinalCloseJournalMissing);

        return (run, ids, blockers);
    }

    public async Task<ReopenResult> ReopenAsync(
        int companyId, int fiscalYearId, int? userId, string? reason, string confirmation,
        bool hasPermission, CancellationToken cancellationToken = default)
    {
        if (!hasPermission)
            return ReopenResult.Fail(ReopenReasons.PermissionRequired, "اجازهٔ ReopenFiscalYear لازم است.");
        if (string.IsNullOrWhiteSpace(reason))
            return ReopenResult.Fail(ReopenReasons.ReasonRequired, "دلیل بازگشایی اجباری است.");

        var year = await db.FiscalYears
            .SingleOrDefaultAsync(y => y.Id == fiscalYearId && y.CompanyId == companyId, cancellationToken);
        if (year is null)
            return ReopenResult.Fail(ReopenReasons.FiscalYearNotFound, "سال مالی یافت نشد.");

        if (year.Status == FiscalYearStatus.Reopened)
            return new ReopenResult(ReopenResultStatus.AlreadyReopened, ReopenReasons.AlreadyCompleted,
                "سال از قبل بازگشایی شده است.", Array.Empty<int>(), Array.Empty<string>());

        if (!string.Equals(confirmation?.Trim(), year.Name?.Trim(), StringComparison.Ordinal))
            return ReopenResult.Fail(ReopenReasons.ConfirmationInvalid, "عبارت تأیید باید برابر کد سال مالی باشد.");

        var precheck = await PrecheckAsync(companyId, fiscalYearId, cancellationToken);
        if (precheck is null)
            return ReopenResult.Fail(ReopenReasons.FiscalYearNotFound, "سال مالی یافت نشد.");
        if (!precheck.CanReopen)
            return ReopenResult.Fail(precheck.Blockers.FirstOrDefault() ?? ReopenReasons.NotClosed,
                "پیش‌شرط‌های بازگشایی برقرار نیست.", precheck.Blockers);

        var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            // ابتدا سال را Reopened و فقط آخرین دوره را Open می‌کنیم تا Reversalِ سندهای بستن
            // (به تاریخ EndDate) از PeriodGuard عبور کند. دوره‌های قبلی HardLocked می‌مانند.
            year.Status = FiscalYearStatus.Reopened;
            year.IsCurrent = true;
            year.ReopenedAt = DateTime.UtcNow;
            year.ReopenedByUserId = userId;
            year.ReopenReason = reason!.Trim();

            var periods = await db.FiscalPeriods
                .Where(p => p.FiscalYearId == fiscalYearId).ToListAsync(cancellationToken);
            var lastPeriod = periods.OrderByDescending(p => p.EndDate).ThenByDescending(p => p.PeriodNumber).First();
            lastPeriod.Status = FiscalPeriodStatus.Open;
            lastPeriod.LockedAt = null;
            lastPeriod.LockedByUserId = null;

            await db.SaveChangesAsync(cancellationToken);

            var reversalIds = new List<int>();
            foreach (var journalId in precheck.ClosingJournalIds)
            {
                var reversal = await ReverseClosingJournalAsync(journalId, year.EndDate.Date, userId, cancellationToken);
                if (reversal is int rid)
                    reversalIds.Add(rid);
            }

            // FinalCloseRun را Superseded/Reopened علامت می‌زنیم — بدون پاک‌کردنِ تاریخچه.
            if (precheck.FinalCloseRunId is int runId)
            {
                var run = await db.FiscalYearCloseRuns.SingleAsync(r => r.Id == runId, cancellationToken);
                run.FailureCode = "REOPENED";
                run.FailureMessage = $"Reopened: {reason!.Trim()}";
            }

            // سالِ بعد فقط وقتی Posting عملیاتی ندارد به Draft/غیرجاری برمی‌گردد.
            var nextStart = year.EndDate.Date.AddDays(1);
            var nextYear = await db.FiscalYears
                .Where(y => y.CompanyId == companyId && y.StartDate == nextStart)
                .OrderBy(y => y.Id).FirstOrDefaultAsync(cancellationToken);
            if (nextYear is not null)
            {
                var hasOperational = await db.JournalEntries.AsNoTracking()
                    .AnyAsync(j => j.CompanyId == companyId && j.FiscalYearId == nextYear.Id
                        && j.Status == JournalEntryStatus.Posted
                        && j.SourceModule != SourceModule, cancellationToken);
                if (!hasOperational)
                {
                    nextYear.Status = FiscalYearStatus.Draft;
                    nextYear.IsCurrent = false;
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            if (audit is not null)
            {
                await audit.LogAsync(nameof(FiscalYear), fiscalYearId, AuditAction.Approve, userId,
                    JsonSerializer.Serialize(new
                    {
                        Action = "ReopenFiscalYear",
                        companyId, fiscalYearId, Reason = reason!.Trim(),
                        ReversedJournals = reversalIds, precheck.FinalCloseRunId
                    }), cancellationToken);
            }

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            return new ReopenResult(ReopenResultStatus.Succeeded, null, null, reversalIds, Array.Empty<string>());
        }
        catch (AccountingValidationException ex)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            return ReopenResult.Fail(ReopenReasons.ReversalFailed, $"برگشت سندهای بستن ناموفق بود: {ex.Code}.");
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

    private async Task<int?> ReverseClosingJournalAsync(
        int journalId, DateTime accountingDate, int? userId, CancellationToken cancellationToken)
    {
        var original = await db.JournalEntries.AsNoTracking()
            .SingleOrDefaultAsync(j => j.Id == journalId, cancellationToken);
        if (original is null || original.Status != JournalEntryStatus.Posted)
            return null;

        try
        {
            var reversal = await posting.ReverseAsync(new AccountingReversalRequest(
                journalId,
                $"REOPEN-REV-{journalId}",
                accountingDate,
                SourceModule,
                $"{original.SourceEventId}:Reopened",
                Description: "Reverse final close on reopen",
                PostedByUserId: userId), cancellationToken);
            return reversal.Id;
        }
        catch (AccountingValidationException ex) when (ex.Code is "JOURNAL_ALREADY_REVERSED" or "DUPLICATE_SOURCE_EVENT")
        {
            // قبلاً برگشت خورده — Idempotent.
            return null;
        }
    }
}
