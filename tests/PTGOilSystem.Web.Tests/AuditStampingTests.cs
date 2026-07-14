using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Security;
using PTGOilSystem.Web.Services;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class AuditStampingTests
{
    [Fact]
    public async Task SaveChangesAsync_Sets_CreatedBy_And_UpdatedBy_From_CurrentUserContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var userContext = new FakeCurrentUserContext(userId: 42, username: "operator", roleName: AuthRoles.Operator);

        await using var db = new ApplicationDbContext(options, userContext);
        var product = new Product { Code = "GO", Name = "Gas Oil" };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        Assert.Equal(42, product.CreatedByUserId);
        Assert.Equal(42, product.UpdatedByUserId);
        Assert.Equal(DateTimeKind.Utc, product.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, product.UpdatedAtUtc!.Value.Kind);

        product.Name = "Updated Gas Oil";
        await db.SaveChangesAsync();

        Assert.Equal(42, product.CreatedByUserId);
        Assert.Equal(42, product.UpdatedByUserId);
        Assert.NotNull(product.UpdatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, product.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, product.UpdatedAtUtc.Value.Kind);
    }

    [Fact]
    public async Task AuditService_Uses_CurrentUserContext_When_ActorUserId_Is_Not_Passed()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var userContext = new FakeCurrentUserContext(userId: 99, username: "admin", roleName: AuthRoles.Admin);

        await using var db = new ApplicationDbContext(options, userContext);
        var service = new AuditService(db, userContext);

        await service.LogAndSaveAsync(nameof(Product), 123, AuditAction.Update, diff: "test-diff");

        var log = await db.AuditLogs.SingleAsync();
        Assert.Equal(99, log.ActorUserId);
        Assert.Equal(nameof(Product), log.EntityName);
        Assert.Equal("Update", log.Action);
        Assert.Equal("Entity", log.Category);
    }

    [Fact]
    public async Task AuditService_Can_Persist_Request_Level_Activity_Metadata()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var userContext = new FakeCurrentUserContext(userId: 7, username: "manager", roleName: AuthRoles.Manager);

        await using var db = new ApplicationDbContext(options, userContext);
        var service = new AuditService(db, userContext);

        await service.LogActivityAndSaveAsync(new AuditLogEntryInput
        {
            Category = AuditLogCategories.Request,
            EntityName = "Contracts",
            EntityId = 0,
            Action = "GET",
            Module = "Contracts",
            Description = "Visited contracts index.",
            HttpMethod = "GET",
            RequestPath = "/Contracts",
            ControllerName = "Contracts",
            ActionName = "Index",
            StatusCode = 200,
            IsSuccess = true,
            CorrelationId = "trace-123",
            DurationMs = 18,
            MetadataJson = "{\"Query\":{}}"
        });

        var log = await db.AuditLogs.SingleAsync();
        Assert.Equal(AuditLogCategories.Request, log.Category);
        Assert.Equal("Contracts", log.Module);
        Assert.Equal("GET", log.Action);
        Assert.Equal("/Contracts", log.RequestPath);
        Assert.Equal(200, log.StatusCode);
        Assert.True(log.IsSuccess);
        Assert.Equal("manager", log.ActorUsername);
        Assert.Equal(18, log.DurationMs);
    }

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public FakeCurrentUserContext(int? userId, string? username, string? roleName)
        {
            UserId = userId;
            Username = username;
            RoleName = roleName;
            Principal = userId.HasValue
                ? new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
                        new Claim(AppClaimTypes.Username, username ?? ""),
                        new Claim(ClaimTypes.Role, roleName ?? ""),
                    ],
                    "TestAuth"))
                : new ClaimsPrincipal(new ClaimsIdentity());
        }

        public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;
        public int? UserId { get; }
        public string? Username { get; }
        public string? FullName => Username;
        public string? RoleName { get; }
        public ClaimsPrincipal Principal { get; }
    }
}
