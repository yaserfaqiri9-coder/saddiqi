using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace PTGOilSystem.Web.Security;

public static class AppClaimTypes
{
    public const string Username = "ptg:username";
    public const string Permission = "ptg:permission";
    public const string AllowedNavigation = "ptg:allowed_navigation";
}

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    int? UserId { get; }
    string? Username { get; }
    string? FullName { get; }
    string? RoleName { get; }
    ClaimsPrincipal Principal { get; }
}

public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public ClaimsPrincipal Principal
        => _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());

    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;

    public int? UserId
    {
        get
        {
            var raw = Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Username => Principal.FindFirstValue(AppClaimTypes.Username);

    public string? FullName => Principal.Identity?.Name;

    public string? RoleName => Principal.FindFirstValue(ClaimTypes.Role);
}
