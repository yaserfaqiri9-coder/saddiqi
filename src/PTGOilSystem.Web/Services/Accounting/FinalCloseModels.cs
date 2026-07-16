namespace PTGOilSystem.Web.Services.Accounting;

/// <summary>
/// نتیجهٔ Precheckِ Final Close — همهٔ پیش‌شرط‌ها به‌صورت فهرستِ Blocker، به‌علاوهٔ ارقامِ سود/زیانی
/// که سندِ بستن روی آن‌ها ساخته می‌شود.
/// </summary>
public sealed record FinalClosePrecheck(
    bool CanClose,
    IReadOnlyList<string> Blockers,
    decimal RevenueUsd,
    decimal ExpenseUsd,
    decimal NetProfitUsd,
    int? NextFiscalYearId,
    string? NextFiscalYearName);

public enum FinalCloseResultStatus
{
    Succeeded = 0,
    Blocked = 1,
    AlreadyClosed = 2
}

public sealed record FinalCloseResult(
    FinalCloseResultStatus Status,
    int? CloseRunId,
    string? FailureCode,
    string? FailureMessage,
    IReadOnlyList<int> ClosingJournalIds,
    IReadOnlyList<string> Blockers)
{
    public static FinalCloseResult Fail(string code, string message, IReadOnlyList<string>? blockers = null)
        => new(FinalCloseResultStatus.Blocked, null, code, message,
            Array.Empty<int>(), blockers ?? Array.Empty<string>());
}

/// <summary>Reason Codeهای Final Close.</summary>
public static class FinalCloseReasons
{
    public const string FiscalYearNotFound = "FISCAL_YEAR_NOT_FOUND";
    public const string InvalidState = "FISCAL_YEAR_NOT_OPEN_OR_REOPENED";
    public const string ConfirmationInvalid = "CLOSE_CONFIRMATION_INVALID";
    public const string ChecklistBlocked = "CHECKLIST_BLOCKED";
    public const string TrialCloseMissing = "TRIAL_CLOSE_MISSING";
    public const string TrialCloseStale = "TRIAL_CLOSE_STALE";
    public const string RevaluationIncomplete = "REVALUATION_INCOMPLETE";
    public const string RetainedEarningsAccountMissing = "RETAINED_EARNINGS_ACCOUNT_MISSING";
    public const string CurrentYearEarningsAccountMissing = "CURRENT_YEAR_EARNINGS_ACCOUNT_MISSING";
    public const string NextYearMissing = "NEXT_FISCAL_YEAR_MISSING";
    public const string NextYearInvalidDates = "NEXT_FISCAL_YEAR_INVALID_DATES";
    public const string NextYearNoPeriods = "NEXT_FISCAL_YEAR_NO_PERIODS";
}
