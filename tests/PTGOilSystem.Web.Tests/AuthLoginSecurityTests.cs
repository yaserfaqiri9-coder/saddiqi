using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Infrastructure.RateLimiting;
using PTGOilSystem.Web.Models.Auth;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Security;
using PTGOilSystem.Web.Services;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class AuthLoginSecurityTests
{
    [Fact]
    public void Login_Post_Preserves_AntiForgery_And_Uses_Login_Rate_Limit_Policy()
    {
        var method = typeof(AuthController).GetMethod(
            nameof(AuthController.Login),
            [typeof(LoginViewModel), typeof(CancellationToken)]);

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        var rateLimit = method.GetCustomAttribute<EnableRateLimitingAttribute>();
        Assert.NotNull(rateLimit);
        Assert.Equal(RateLimitPolicies.Login, rateLimit!.PolicyName);
    }

    [Fact]
    public async Task Locked_Login_Returns_Generic_429_Without_Verifying_Password_And_Is_Audited()
    {
        await using var db = CreateDatabase();
        var users = new UserService(db);
        var audit = new AuditService(db);
        var controller = CreateController(
            users,
            audit,
            new AlwaysLockedGuard(),
            isLocalUrl: true);

        var result = await controller.Login(new LoginViewModel
        {
            Username = "unknown-user",
            Password = "not-logged"
        });

        Assert.IsType<ViewResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, controller.Response.StatusCode);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage.Contains("موقتاً محدود", StringComparison.Ordinal));
        var entry = Assert.Single(db.AuditLogs);
        Assert.Equal(LoginAuditActions.Locked, entry.Action);
        Assert.DoesNotContain("not-logged", entry.Description ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fifth_Failed_Login_Is_Audited_Locked_And_Does_Not_Log_The_Password()
    {
        await using var db = CreateDatabase();
        db.Roles.Add(new Role { Id = 1, Name = AuthRoles.Viewer });
        await db.SaveChangesAsync();
        var users = new UserService(db);
        await users.CreateUserAsync("operator", "Operator", "StrongPass123!", 1);

        for (var index = 4; index > 0; index--)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Category = AuditLogCategories.Authentication,
                EntityName = nameof(User),
                Action = LoginAuditActions.Failed,
                ActorUsername = "operator",
                IpAddress = "10.10.0.8",
                ActionAtUtc = DateTime.UtcNow.AddMinutes(-index),
                IsSuccess = false
            });
        }
        await db.SaveChangesAsync();

        var audit = new AuditService(db);
        var guard = new AuditLoginAttemptGuard(db, TimeProvider.System);
        var controller = CreateController(users, audit, guard, isLocalUrl: true);

        await controller.Login(new LoginViewModel
        {
            Username = "operator",
            Password = "never-log-this-password"
        });

        Assert.Equal(StatusCodes.Status429TooManyRequests, controller.Response.StatusCode);
        Assert.Equal(5, db.AuditLogs.Count(entry => entry.Action == LoginAuditActions.Failed));
        Assert.Contains(db.AuditLogs, entry => entry.Action == LoginAuditActions.Locked);
        Assert.DoesNotContain(db.AuditLogs, entry =>
            (entry.Description ?? string.Empty).Contains("never-log-this-password", StringComparison.Ordinal)
            || (entry.Diff ?? string.Empty).Contains("never-log-this-password", StringComparison.Ordinal)
            || (entry.MetadataJson ?? string.Empty).Contains("never-log-this-password", StringComparison.Ordinal));
    }

    [Fact]
    public void Authenticated_Login_Get_Allows_Local_ReturnUrl()
    {
        using var db = CreateDatabase();
        var controller = CreateController(
            new UserService(db),
            audit: null,
            AllowGuard.Instance,
            isLocalUrl: true,
            authenticated: true);

        var result = controller.Login("/Reports/Index");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Reports/Index", redirect.Url);
    }

    [Fact]
    public void Authenticated_Login_Get_Rejects_External_ReturnUrl()
    {
        using var db = CreateDatabase();
        var controller = CreateController(
            new UserService(db),
            audit: null,
            AllowGuard.Instance,
            isLocalUrl: false,
            authenticated: true);

        var result = controller.Login("https://example.test/steal");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }

    [Fact]
    public async Task Missing_And_Inactive_Users_Return_The_Same_Generic_Error()
    {
        await using var db = CreateDatabase();
        db.Roles.Add(new Role { Id = 1, Name = AuthRoles.Viewer });
        await db.SaveChangesAsync();
        var users = new UserService(db);
        var inactive = await users.CreateUserAsync("inactive", "Inactive User", "StrongPass123!", 1);
        inactive.IsActive = false;
        await db.SaveChangesAsync();

        var inactiveController = CreateController(users, null, AllowGuard.Instance, true);
        await inactiveController.Login(new LoginViewModel
        {
            Username = "inactive",
            Password = "StrongPass123!"
        });

        var missingController = CreateController(users, null, AllowGuard.Instance, true);
        await missingController.Login(new LoginViewModel
        {
            Username = "missing",
            Password = "StrongPass123!"
        });

        Assert.Equal(
            inactiveController.ModelState[string.Empty]!.Errors.Single().ErrorMessage,
            missingController.ModelState[string.Empty]!.Errors.Single().ErrorMessage);
    }

    private static ApplicationDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static AuthController CreateController(
        IUserService users,
        IAuditService? audit,
        ILoginAttemptGuard guard,
        bool isLocalUrl,
        bool authenticated = false)
    {
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new NoopAuthenticationService())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.10.0.8");
        if (authenticated)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.Name, "operator")], "Test"));
        }

        var controller = new AuthController(users, audit, guard, NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new InMemoryTempDataProvider()),
            Url = new StubUrlHelper(isLocalUrl)
        };
        return controller;
    }

    private sealed class AlwaysLockedGuard : ILoginAttemptGuard
    {
        public Task<LoginAttemptStatus> GetStatusAsync(
            string username,
            string ipAddress,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new LoginAttemptStatus(true, 5, DateTimeOffset.UtcNow.AddMinutes(15)));
    }

    private sealed class AllowGuard : ILoginAttemptGuard
    {
        public static AllowGuard Instance { get; } = new();

        public Task<LoginAttemptStatus> GetStatusAsync(
            string username,
            string ipAddress,
            CancellationToken cancellationToken = default)
            => Task.FromResult(LoginAttemptStatus.Allowed());
    }

    private sealed class NoopAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
            => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
            => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private sealed class StubUrlHelper(bool isLocalUrl) : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();
        public string? Action(UrlActionContext actionContext) => "/";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => isLocalUrl;
        public string? Link(string? routeName, object? values) => "/";
        public string? RouteUrl(UrlRouteContext routeContext) => "/";
    }
}
