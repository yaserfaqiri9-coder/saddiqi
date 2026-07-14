using System.Threading;
using System.Threading.Tasks;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services;

/// <summary>
/// Audit logging actions covered by <see cref="IAuditService"/>.
/// Kept narrow on purpose — additional verbs go through <see cref="LogAsync"/>
/// with a free-form action string.
/// </summary>
public enum AuditAction
{
    Insert = 1,
    Update = 2,
    Delete = 3,
    Approve = 4,
    Reverse = 5,
}

public sealed class AuditLogEntryInput
{
    public string Category { get; init; } = AuditLogCategories.Entity;
    public string EntityName { get; init; } = "";
    public int EntityId { get; init; }
    public string Action { get; init; } = "";
    public int? ActorUserId { get; init; }
    public string? ActorUsername { get; init; }
    public string? Module { get; init; }
    public string? Description { get; init; }
    public string? Diff { get; init; }
    public string? HttpMethod { get; init; }
    public string? RequestPath { get; init; }
    public string? ControllerName { get; init; }
    public string? ActionName { get; init; }
    public int? StatusCode { get; init; }
    public bool IsSuccess { get; init; } = true;
    public string? CorrelationId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public long? DurationMs { get; init; }
    public string? MetadataJson { get; init; }
}

/// <summary>
/// Lightweight audit logging service (system rule #11).
///
/// Designed to be called explicitly from operations that mutate sensitive
/// data: contracts, sales, expenses, daily prices, FX rates, and contract
/// amendments. Callers pass entity name, id, and an optional structured diff.
/// If <paramref name="actorUserId"/> is null, the implementation may fall back
/// to the current authenticated user context.
/// </summary>
public interface IAuditService
{
    /// <summary>Persists a single audit entry. Does not call SaveChanges.</summary>
    Task LogAsync(
        string entityName,
        int entityId,
        AuditAction action,
        int? actorUserId = null,
        string? diff = null,
        CancellationToken ct = default);

    /// <summary>Persists a single audit entry and commits via SaveChanges.</summary>
    Task LogAndSaveAsync(
        string entityName,
        int entityId,
        AuditAction action,
        int? actorUserId = null,
        string? diff = null,
        CancellationToken ct = default);

    Task LogActivityAsync(
        AuditLogEntryInput entry,
        CancellationToken ct = default);

    Task LogActivityAndSaveAsync(
        AuditLogEntryInput entry,
        CancellationToken ct = default);
}
