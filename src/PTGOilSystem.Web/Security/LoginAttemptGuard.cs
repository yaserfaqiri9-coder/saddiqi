using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Security;

public static class LoginProtectionDefaults
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
}

public static class LoginAuditActions
{
    public const string Succeeded = "Login";
    public const string Failed = "LoginFailed";
    public const string Locked = "LoginLocked";
    public const string RateLimited = "LoginRateLimited";
}

public readonly record struct LoginAttemptStatus(
    bool IsLocked,
    int FailedAttemptCount,
    DateTimeOffset? LockedUntilUtc)
{
    public static LoginAttemptStatus Allowed(int failedAttemptCount = 0)
        => new(false, failedAttemptCount, null);
}

public interface ILoginAttemptGuard
{
    Task<LoginAttemptStatus> GetStatusAsync(
        string username,
        string ipAddress,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads authentication audit events from the shared application database so
/// lockout state remains consistent across application instances. No new table
/// or migration is required.
/// </summary>
public sealed class AuditLoginAttemptGuard : ILoginAttemptGuard
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;

    public AuditLoginAttemptGuard(ApplicationDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<LoginAttemptStatus> GetStatusAsync(
        string username,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(ipAddress))
            return LoginAttemptStatus.Allowed();

        var now = _timeProvider.GetUtcNow();
        var windowStartUtc = now.Subtract(LoginProtectionDefaults.AttemptWindow).UtcDateTime;
        var normalizedUsername = username.Trim().ToUpperInvariant();
        var normalizedIpAddress = ipAddress.Trim();

        var events = await _db.AuditLogs
            .AsNoTracking()
            .Where(entry =>
                entry.Category == AuditLogCategories.Authentication
                && entry.ActionAtUtc >= windowStartUtc
                && entry.ActorUsername != null
                && entry.ActorUsername.ToUpper() == normalizedUsername
                && entry.IpAddress == normalizedIpAddress
                && (entry.Action == LoginAuditActions.Failed
                    || entry.Action == LoginAuditActions.Succeeded))
            .OrderBy(entry => entry.ActionAtUtc)
            .Select(entry => new { entry.Action, entry.ActionAtUtc })
            .ToListAsync(cancellationToken);

        var lastSuccessfulLoginUtc = events
            .Where(entry => entry.Action == LoginAuditActions.Succeeded)
            .Select(entry => (DateTime?)entry.ActionAtUtc)
            .LastOrDefault();

        var failedAttempts = events
            .Where(entry =>
                entry.Action == LoginAuditActions.Failed
                && (!lastSuccessfulLoginUtc.HasValue || entry.ActionAtUtc > lastSuccessfulLoginUtc.Value))
            .Select(entry => entry.ActionAtUtc)
            .ToList();

        if (failedAttempts.Count < LoginProtectionDefaults.MaxFailedAttempts)
            return LoginAttemptStatus.Allowed(failedAttempts.Count);

        var lockoutStartedUtc = failedAttempts[LoginProtectionDefaults.MaxFailedAttempts - 1];
        var lockedUntilUtc = new DateTimeOffset(
            DateTime.SpecifyKind(lockoutStartedUtc, DateTimeKind.Utc))
            .Add(LoginProtectionDefaults.LockoutDuration);

        return lockedUntilUtc > now
            ? new LoginAttemptStatus(true, failedAttempts.Count, lockedUntilUtc)
            : LoginAttemptStatus.Allowed(failedAttempts.Count);
    }
}

internal sealed class AllowAllLoginAttemptGuard : ILoginAttemptGuard
{
    public static AllowAllLoginAttemptGuard Instance { get; } = new();

    private AllowAllLoginAttemptGuard()
    {
    }

    public Task<LoginAttemptStatus> GetStatusAsync(
        string username,
        string ipAddress,
        CancellationToken cancellationToken = default)
        => Task.FromResult(LoginAttemptStatus.Allowed());
}
