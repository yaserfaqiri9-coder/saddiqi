using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Security;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class LoginAttemptGuardTests
{
    [Fact]
    public async Task Fifth_Failure_In_Window_Activates_Fifteen_Minute_Lockout()
    {
        var now = new DateTimeOffset(2026, 7, 18, 10, 0, 0, TimeSpan.Zero);
        await using var db = CreateDatabase();
        AddFailures(db, "operator", "10.10.0.8", now, 5);
        await db.SaveChangesAsync();

        var guard = new AuditLoginAttemptGuard(db, new FixedTimeProvider(now));

        var status = await guard.GetStatusAsync("OPERATOR", "10.10.0.8");

        Assert.True(status.IsLocked);
        Assert.Equal(5, status.FailedAttemptCount);
        Assert.Equal(now.AddMinutes(14), status.LockedUntilUtc);
    }

    [Fact]
    public async Task Successful_Login_Clears_Earlier_Failures_For_Same_User_And_Ip()
    {
        var now = new DateTimeOffset(2026, 7, 18, 10, 0, 0, TimeSpan.Zero);
        await using var db = CreateDatabase();
        AddFailures(db, "operator", "10.10.0.8", now, 5);
        db.AuditLogs.Add(AuthenticationEvent(
            LoginAuditActions.Succeeded,
            "operator",
            "10.10.0.8",
            now.UtcDateTime));
        await db.SaveChangesAsync();

        var guard = new AuditLoginAttemptGuard(db, new FixedTimeProvider(now));

        var status = await guard.GetStatusAsync("operator", "10.10.0.8");

        Assert.False(status.IsLocked);
        Assert.Equal(0, status.FailedAttemptCount);
    }

    [Fact]
    public async Task Expired_Failures_Do_Not_Keep_Account_Locked()
    {
        var now = new DateTimeOffset(2026, 7, 18, 10, 30, 0, TimeSpan.Zero);
        await using var db = CreateDatabase();
        AddFailures(db, "operator", "10.10.0.8", now.AddMinutes(-15), 5);
        await db.SaveChangesAsync();

        var guard = new AuditLoginAttemptGuard(db, new FixedTimeProvider(now));

        var status = await guard.GetStatusAsync("operator", "10.10.0.8");

        Assert.False(status.IsLocked);
    }

    [Fact]
    public async Task Failures_Are_Partitioned_By_Username_And_Ip()
    {
        var now = new DateTimeOffset(2026, 7, 18, 10, 0, 0, TimeSpan.Zero);
        await using var db = CreateDatabase();
        AddFailures(db, "operator", "10.10.0.8", now, 5);
        await db.SaveChangesAsync();

        var guard = new AuditLoginAttemptGuard(db, new FixedTimeProvider(now));

        var differentUser = await guard.GetStatusAsync("manager", "10.10.0.8");
        var differentIp = await guard.GetStatusAsync("operator", "10.10.0.9");

        Assert.False(differentUser.IsLocked);
        Assert.False(differentIp.IsLocked);
    }

    private static ApplicationDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void AddFailures(
        ApplicationDbContext db,
        string username,
        string ipAddress,
        DateTimeOffset referenceTime,
        int count)
    {
        for (var index = count; index > 0; index--)
        {
            db.AuditLogs.Add(AuthenticationEvent(
                LoginAuditActions.Failed,
                username,
                ipAddress,
                referenceTime.AddMinutes(-index).UtcDateTime));
        }
    }

    private static AuditLog AuthenticationEvent(
        string action,
        string username,
        string ipAddress,
        DateTime actionAtUtc)
        => new()
        {
            Category = AuditLogCategories.Authentication,
            EntityName = nameof(User),
            Action = action,
            ActorUsername = username,
            IpAddress = ipAddress,
            ActionAtUtc = actionAtUtc,
            IsSuccess = action == LoginAuditActions.Succeeded
        };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
