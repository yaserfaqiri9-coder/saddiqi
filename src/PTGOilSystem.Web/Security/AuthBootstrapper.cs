using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services;

namespace PTGOilSystem.Web.Security;

public sealed class BootstrapAdminOptions
{
    public string Username { get; set; } = "admin";
    public string FullName { get; set; } = "System Administrator";
    public string? Password { get; set; }
}

public sealed class AuthBootstrapper
{
    private readonly ApplicationDbContext _db;
    private readonly IUserService _users;
    private readonly ILogger<AuthBootstrapper> _logger;

    public AuthBootstrapper(
        ApplicationDbContext db,
        IUserService users,
        ILogger<AuthBootstrapper> logger)
    {
        _db = db;
        _users = users;
        _logger = logger;
    }

    public async Task EnsureSeedDataAsync(
        BootstrapAdminOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new BootstrapAdminOptions();

        try
        {
            await EnsureDefaultRolesAsync(ct);

            if (await _db.Users.AnyAsync(ct))
                return;

            var adminRole = await _db.Roles
                .AsNoTracking()
                .SingleAsync(r => r.Name == AuthRoles.Admin, ct);

            var password = options.Password;
            var generatedPassword = false;
            if (string.IsNullOrWhiteSpace(password))
            {
                password = GenerateOneTimePassword();
                generatedPassword = true;
            }

            var user = await _users.CreateUserAsync(
                username: string.IsNullOrWhiteSpace(options.Username) ? "admin" : options.Username.Trim(),
                fullName: string.IsNullOrWhiteSpace(options.FullName) ? "System Administrator" : options.FullName.Trim(),
                password: password,
                roleId: adminRole.Id,
                ct: ct);

            if (generatedPassword)
            {
                _logger.LogCritical(
                    "No users existed. Bootstrap admin '{Username}' was created with one-time password '{Password}'. Change or replace this user immediately.",
                    user.Username,
                    password);
            }
            else
            {
                _logger.LogWarning(
                    "No users existed. Bootstrap admin '{Username}' was created from bootstrap configuration.",
                    user.Username);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Authentication bootstrap skipped. Database may be unavailable or not migrated yet.");
        }
    }

    public async Task EnsureDefaultRolesAsync(CancellationToken ct = default)
    {
        var existingRoles = await _db.Roles
            .Where(r => AuthRoles.AllRoles.Contains(r.Name))
            .ToListAsync(ct);

        foreach (var role in existingRoles)
        {
            ApplyDefaultAccess(role);
        }

        var existingNames = existingRoles.Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var roleName in AuthRoles.AllRoles.Except(existingNames))
        {
            var role = new Role
            {
                Name = roleName,
                Description = GetRoleDescription(roleName),
                CreatedAtUtc = DateTime.UtcNow
            };
            ApplyDefaultAccess(role);
            _db.Roles.Add(role);
        }

        await _db.SaveChangesAsync(ct);
    }

    private static void ApplyDefaultAccess(Role role)
    {
        if (string.Equals(role.Name, AuthRoles.Admin, StringComparison.Ordinal))
        {
            role.CanManageData = true;
            role.CanManageUsers = true;
            role.AllowedNavigationItems = RoleAccessRules.SerializeNavigation(RoleAccessRules.AllNavigationKeys);
            return;
        }

        if (string.Equals(role.Name, AuthRoles.Manager, StringComparison.Ordinal)
            || string.Equals(role.Name, AuthRoles.Operator, StringComparison.Ordinal))
        {
            role.CanManageData = true;
        }

        if (string.IsNullOrWhiteSpace(role.AllowedNavigationItems))
        {
            role.AllowedNavigationItems = RoleAccessRules.SerializeNavigation(RoleAccessRules.DefaultNavigationForRole(role.Name));
        }
    }

    private static string GetRoleDescription(string roleName) => roleName switch
    {
        AuthRoles.Admin => "دسترسی کامل مدیریتی",
        AuthRoles.Manager => "مدیریت عملیات و داده‌ها",
        AuthRoles.Operator => "ثبت و ویرایش عملیات روزانه",
        _ => "مشاهده‌گر فقط خواندنی"
    };

    private static string GenerateOneTimePassword()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(8)) + "!9a";
}
