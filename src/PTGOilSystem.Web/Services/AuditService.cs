using System;
using System.Threading;
using System.Threading.Tasks;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Security;

namespace PTGOilSystem.Web.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserContext? _currentUserContext;

    public AuditService(ApplicationDbContext db) => _db = db;

    public AuditService(ApplicationDbContext db, ICurrentUserContext currentUserContext)
    {
        _db = db;
        _currentUserContext = currentUserContext;
    }

    public Task LogAsync(
        string entityName,
        int entityId,
        AuditAction action,
        int? actorUserId = null,
        string? diff = null,
        CancellationToken ct = default)
        => LogActivityAsync(
            new AuditLogEntryInput
            {
                Category = AuditLogCategories.Entity,
                EntityName = entityName,
                EntityId = entityId,
                Action = action.ToString(),
                ActorUserId = actorUserId,
                ActorUsername = _currentUserContext?.Username,
                Module = entityName,
                Diff = diff,
                IsSuccess = true,
            },
            ct);

    public Task LogActivityAsync(
        AuditLogEntryInput entry,
        CancellationToken ct = default)
    {
        if (entry is null)
            throw new ArgumentNullException(nameof(entry));

        if (string.IsNullOrWhiteSpace(entry.EntityName))
            throw new ArgumentException("entityName is required.", nameof(entry));

        if (string.IsNullOrWhiteSpace(entry.Action))
            throw new ArgumentException("action is required.", nameof(entry));

        if (string.IsNullOrWhiteSpace(entry.Category))
            throw new ArgumentException("category is required.", nameof(entry));

        var actorUserId = entry.ActorUserId ?? _currentUserContext?.UserId;
        var actorUsername = string.IsNullOrWhiteSpace(entry.ActorUsername)
            ? _currentUserContext?.Username
            : entry.ActorUsername.Trim();

        var auditLog = new AuditLog
        {
            Category = entry.Category.Trim(),
            EntityName = entry.EntityName.Trim(),
            EntityId = entry.EntityId,
            Action = entry.Action.Trim(),
            ActorUserId = actorUserId,
            ActorUsername = actorUsername,
            Module = string.IsNullOrWhiteSpace(entry.Module) ? null : entry.Module.Trim(),
            Description = string.IsNullOrWhiteSpace(entry.Description) ? null : entry.Description.Trim(),
            Diff = entry.Diff,
            ActionAtUtc = DateTime.UtcNow,
            HttpMethod = string.IsNullOrWhiteSpace(entry.HttpMethod) ? null : entry.HttpMethod.Trim(),
            RequestPath = string.IsNullOrWhiteSpace(entry.RequestPath) ? null : entry.RequestPath.Trim(),
            ControllerName = string.IsNullOrWhiteSpace(entry.ControllerName) ? null : entry.ControllerName.Trim(),
            ActionName = string.IsNullOrWhiteSpace(entry.ActionName) ? null : entry.ActionName.Trim(),
            StatusCode = entry.StatusCode,
            IsSuccess = entry.IsSuccess,
            CorrelationId = string.IsNullOrWhiteSpace(entry.CorrelationId) ? null : entry.CorrelationId.Trim(),
            IpAddress = string.IsNullOrWhiteSpace(entry.IpAddress) ? null : entry.IpAddress.Trim(),
            UserAgent = string.IsNullOrWhiteSpace(entry.UserAgent) ? null : entry.UserAgent.Trim(),
            DurationMs = entry.DurationMs,
            MetadataJson = entry.MetadataJson,
        };

        _db.AuditLogs.Add(auditLog);
        return Task.CompletedTask;
    }

    public async Task LogAndSaveAsync(
        string entityName,
        int entityId,
        AuditAction action,
        int? actorUserId = null,
        string? diff = null,
        CancellationToken ct = default)
    {
        await LogAsync(entityName, entityId, action, actorUserId, diff, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task LogActivityAndSaveAsync(
        AuditLogEntryInput entry,
        CancellationToken ct = default)
    {
        await LogActivityAsync(entry, ct);
        await _db.SaveChangesAsync(ct);
    }
}
