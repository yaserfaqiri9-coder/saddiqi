namespace PTGOilSystem.Web.Models.Audit;

public sealed class ActivityLogFilterViewModel
{
    public string? Query { get; init; }
    public string? User { get; init; }
    public string? Category { get; init; }
    public string? Module { get; init; }
    public string? Action { get; init; }
    public string? Severity { get; init; }
    public string? Success { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public int Limit { get; init; } = 250;
    public int Page { get; init; } = 1;
}

public sealed class ActivityLogListItemViewModel
{
    public int Id { get; init; }
    public DateTime ActionAtUtc { get; init; }
    public string Category { get; init; } = "";
    public string Action { get; init; } = "";
    public string EntityName { get; init; } = "";
    public int EntityId { get; init; }
    public string? Module { get; init; }
    public string? Description { get; init; }
    public string? ActorUsername { get; init; }
    public int? ActorUserId { get; init; }
    public string? HttpMethod { get; init; }
    public string? RequestPath { get; init; }
    public int? StatusCode { get; init; }
    public bool IsSuccess { get; init; }
    public long? DurationMs { get; init; }
    public string ActorDisplay { get; init; } = "";
    public string ModuleDisplay { get; init; } = "";
    public string ActionDisplay { get; init; } = "";
    public string RelatedRecord { get; init; } = "";
    public string HumanSummary { get; init; } = "";
    public string Severity { get; init; } = "";
    public string SeverityLabel { get; init; } = "";
    public string SeverityCssClass { get; init; } = "";
    public string ResultLabel { get; init; } = "";
    public string ResultCssClass { get; init; } = "";
    public string? RelatedController { get; init; }
    public string RelatedAction { get; init; } = "Details";
    public int? RelatedId { get; init; }
}

public sealed class ActivityLogIndexViewModel
{
    public ActivityLogFilterViewModel Filter { get; init; } = new();
    public IReadOnlyList<ActivityLogListItemViewModel> Items { get; init; } = Array.Empty<ActivityLogListItemViewModel>();
    public int TotalCount { get; init; }
    public int SensitiveCount { get; init; }
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public int ActiveUserCount { get; init; }
    public DateTime? LastActivityAtUtc { get; init; }
    public int CurrentPage { get; init; } = 1;
    public int PageCount { get; init; } = 1;
    public IReadOnlyList<string> Users { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Modules { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Actions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ActivityLogSeverityOption> SeverityOptions { get; init; } = Array.Empty<ActivityLogSeverityOption>();
}

public sealed class ActivityLogDetailsViewModel
{
    public int Id { get; init; }
    public DateTime ActionAtUtc { get; init; }
    public string Category { get; init; } = "";
    public string Action { get; init; } = "";
    public string EntityName { get; init; } = "";
    public int EntityId { get; init; }
    public string? Module { get; init; }
    public string? Description { get; init; }
    public string? Diff { get; init; }
    public string? ActorUsername { get; init; }
    public int? ActorUserId { get; init; }
    public string? ActorRoleName { get; init; }
    public string? HttpMethod { get; init; }
    public string? RequestPath { get; init; }
    public string? ControllerName { get; init; }
    public string? ActionName { get; init; }
    public int? StatusCode { get; init; }
    public bool IsSuccess { get; init; }
    public string? CorrelationId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public long? DurationMs { get; init; }
    public string? MetadataJson { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string ActorDisplay { get; init; } = "";
    public string ModuleDisplay { get; init; } = "";
    public string ActionDisplay { get; init; } = "";
    public string RelatedRecord { get; init; } = "";
    public string HumanSummary { get; init; } = "";
    public string Severity { get; init; } = "";
    public string SeverityLabel { get; init; } = "";
    public string SeverityCssClass { get; init; } = "";
    public string ResultLabel { get; init; } = "";
    public string ResultCssClass { get; init; } = "";
    public string? RelatedController { get; init; }
    public string RelatedAction { get; init; } = "Details";
    public int? RelatedId { get; init; }
    public IReadOnlyList<ActivityLogChangeViewModel> Changes { get; init; } = Array.Empty<ActivityLogChangeViewModel>();
}

public sealed class ActivityLogSeverityOption
{
    public string Value { get; init; } = "";
    public string Label { get; init; } = "";
}

public sealed class ActivityLogChangeViewModel
{
    public string Field { get; init; } = "";
    public string Before { get; init; } = "";
    public string After { get; init; } = "";
}
