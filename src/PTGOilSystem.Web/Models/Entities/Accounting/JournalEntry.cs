using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PTGOilSystem.Web.Models.Entities;

public class JournalEntry : BaseEntity
{
    [NotMapped]
    public DateTime CreatedAt
    {
        get => CreatedAtUtc;
        set => CreatedAtUtc = value;
    }

    public int CompanyId { get; set; }
    public Company? Company { get; set; }
    public int FiscalYearId { get; set; }
    public FiscalYear? FiscalYear { get; set; }
    public int FiscalPeriodId { get; set; }
    public FiscalPeriod? FiscalPeriod { get; set; }

    [Required, MaxLength(50)]
    public string JournalNumber { get; set; } = "";

    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;
    public DateTime AccountingDate { get; set; }
    public DateTime DocumentDate { get; set; }
    public DateTime OperationDate { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required, MaxLength(100)]
    public string SourceModule { get; set; } = "";

    [MaxLength(100)]
    public string? SourceEntityType { get; set; }

    public int? SourceEntityId { get; set; }

    [MaxLength(200)]
    public string? SourceEventId { get; set; }

    public bool IsOpening { get; set; }
    public bool IsClosing { get; set; }
    public bool IsAdjustment { get; set; }
    public bool IsReversal { get; set; }

    public int? ReversalOfJournalEntryId { get; set; }
    public JournalEntry? ReversalOfJournalEntry { get; set; }
    public ICollection<JournalEntry> Reversals { get; set; } = [];

    public DateTime? PostedAt { get; set; }
    public int? PostedByUserId { get; set; }
    public User? PostedByUser { get; set; }

    public ICollection<JournalEntryLine> Lines { get; set; } = [];
    public uint RowVersion { get; set; }
}
