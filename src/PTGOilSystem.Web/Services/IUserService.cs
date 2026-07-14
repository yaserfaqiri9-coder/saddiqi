using System.Threading;
using System.Threading.Tasks;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services;

/// <summary>
/// Minimal user/role skeleton (Phase 4). Provides creation, password hashing,
/// and verification — the bare minimum to allow later phases to layer real
/// authentication (cookies, claims, MFA) on top without refactoring data.
///
/// Hashing uses PBKDF2 (HMAC-SHA256, 100k iterations) from BCL — no extra
/// packages required.
/// </summary>
public interface IUserService
{
    Task<User> CreateUserAsync(
        string username,
        string fullName,
        string password,
        int? roleId = null,
        string? email = null,
        CancellationToken ct = default);

    /// <summary>Returns the user if the password matches and the account is active; otherwise null.</summary>
    Task<User?> VerifyPasswordAsync(
        string username,
        string password,
        CancellationToken ct = default);

    /// <summary>Hashes a password into a stable, self-contained string suitable for <see cref="User.PasswordHash"/>.</summary>
    string HashPassword(string password);

    /// <summary>Verifies a plaintext password against a previously stored hash.</summary>
    bool VerifyHash(string password, string storedHash);

    Task ChangePasswordAsync(
        int userId,
        string currentPassword,
        string newPassword,
        CancellationToken ct = default);

    Task ResetPasswordAsync(
        int userId,
        string newPassword,
        CancellationToken ct = default);
}
