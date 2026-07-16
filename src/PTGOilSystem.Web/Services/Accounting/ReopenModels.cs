namespace PTGOilSystem.Web.Services.Accounting;

public sealed record ReopenPrecheck(
    bool CanReopen,
    IReadOnlyList<string> Blockers,
    int? FinalCloseRunId,
    IReadOnlyList<int> ClosingJournalIds);

public enum ReopenResultStatus
{
    Succeeded = 0,
    Blocked = 1,
    AlreadyReopened = 2
}

public sealed record ReopenResult(
    ReopenResultStatus Status,
    string? FailureCode,
    string? FailureMessage,
    IReadOnlyList<int> ReversalJournalIds,
    IReadOnlyList<string> Blockers)
{
    public static ReopenResult Fail(string code, string message, IReadOnlyList<string>? blockers = null)
        => new(ReopenResultStatus.Blocked, code, message, Array.Empty<int>(), blockers ?? Array.Empty<string>());
}

/// <summary>Reason Codeهای بازگشایی (مرحله ۱۵).</summary>
public static class ReopenReasons
{
    public const string FiscalYearNotFound = "FISCAL_YEAR_NOT_FOUND";
    public const string NotClosed = "FISCAL_YEAR_NOT_CLOSED";
    public const string NotLatestClosed = "FISCAL_YEAR_NOT_LATEST_CLOSED";
    public const string LaterClosedYearExists = "LATER_CLOSED_YEAR_EXISTS";
    public const string NextYearHasOperationalPostings = "NEXT_YEAR_HAS_OPERATIONAL_POSTINGS";
    public const string FinalCloseRunNotFound = "FINAL_CLOSE_RUN_NOT_FOUND";
    public const string FinalCloseJournalMissing = "FINAL_CLOSE_JOURNAL_MISSING";
    public const string ReasonRequired = "REOPEN_REASON_REQUIRED";
    public const string PermissionRequired = "REOPEN_PERMISSION_REQUIRED";
    public const string ConfirmationInvalid = "REOPEN_CONFIRMATION_INVALID";
    public const string AlreadyCompleted = "REOPEN_ALREADY_COMPLETED";
    public const string ReversalFailed = "REOPEN_REVERSAL_FAILED";
}
