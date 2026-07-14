namespace PTGOilSystem.Web.Models.Entities;

/// <summary>
/// Idempotency guard for create/post forms. A unique token is issued on GET and
/// embedded in the form; on POST it is stamped as consumed inside the same
/// transaction that creates the primary record. A duplicate submit carries the
/// same token, violates the unique index on <see cref="Token"/>, and is rejected
/// instead of creating a second record. Purely additive — no business logic.
/// </summary>
public class ProcessedFormToken : BaseEntity
{
    /// <summary>Globally-unique value issued on GET (GUID "N").</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Logical form identity, e.g. "Contract.Create".</summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>User who consumed the token, when known.</summary>
    public int? UserId { get; set; }

    /// <summary>When the token was consumed (record saved).</summary>
    public DateTime? ConsumedAtUtc { get; set; }

    /// <summary>Entity type of the record created with this token (optional).</summary>
    public string? ReferenceType { get; set; }

    /// <summary>Id of the record created with this token (optional).</summary>
    public int? ReferenceId { get; set; }
}
