using System;

namespace PTGOilSystem.Web.Models.Entities;

/// <summary>
/// Base class with audit timestamps inherited by every persistent entity.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? UpdatedByUserId { get; set; }
}
