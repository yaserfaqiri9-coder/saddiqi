using Microsoft.EntityFrameworkCore;
using Npgsql;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Security;

namespace PTGOilSystem.Web.Services;

/// <summary>
/// Idempotency guard preventing duplicate form submissions from creating
/// duplicate records. Front-end button locking stops most double-clicks; this is
/// the server-side backstop for slow saves, retries, and concurrent requests.
/// </summary>
public interface IFormTokenGuard
{
    /// <summary>New unique token for a GET-rendered form.</summary>
    string Issue();

    /// <summary>
    /// Adds a consumed-token row to the change tracker so it is persisted in the
    /// SAME <c>SaveChanges</c> (and therefore the same transaction) as the record
    /// being created. Empty/missing token is a no-op (fail-open — never blocks a
    /// legitimate save). Call after model validation passes, before SaveChanges.
    /// </summary>
    void Stamp(string? token, string purpose, string? referenceType = null);

    /// <summary>
    /// True when the exception is the unique-index violation raised by a duplicate
    /// token (and not some other unique constraint on the same SaveChanges).
    /// </summary>
    bool IsDuplicate(Exception ex);
}

public sealed class FormTokenGuard : IFormTokenGuard
{
    /// <summary>Explicit index name so duplicate detection is unambiguous.</summary>
    public const string TokenIndexName = "IX_ProcessedFormTokens_Token";

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserContext? _currentUser;

    public FormTokenGuard(ApplicationDbContext db, ICurrentUserContext? currentUser = null)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public string Issue() => Guid.NewGuid().ToString("N");

    public void Stamp(string? token, string purpose, string? referenceType = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return; // fail-open: no token => no idempotency, but never block the save
        }

        _db.ProcessedFormTokens.Add(new ProcessedFormToken
        {
            Token = token.Trim(),
            Purpose = purpose,
            UserId = _currentUser?.UserId,
            ConsumedAtUtc = DateTime.UtcNow,
            ReferenceType = referenceType
        });
    }

    public bool IsDuplicate(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            // Precise (production/PostgreSQL): unique violation on the token index.
            if (current is PostgresException pg
                && pg.SqlState == PostgresErrorCodes.UniqueViolation
                && string.Equals(pg.ConstraintName, TokenIndexName, StringComparison.Ordinal))
            {
                return true;
            }

            // Provider-agnostic safety net, scoped tightly to our own table so it
            // can't swallow an unrelated constraint failure (also covers SQLite
            // used in tests, where there is no PostgresException).
            var message = current.Message;
            if (!string.IsNullOrEmpty(message)
                && message.Contains("ProcessedFormTokens", StringComparison.OrdinalIgnoreCase)
                && (message.Contains("unique", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
