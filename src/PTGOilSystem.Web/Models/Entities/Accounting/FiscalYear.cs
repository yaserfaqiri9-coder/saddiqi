using System.ComponentModel.DataAnnotations;

namespace PTGOilSystem.Web.Models.Entities;

public class FiscalYear : BaseEntity
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public FiscalYearStatus Status { get; set; } = FiscalYearStatus.Draft;

    public int? PreviousFiscalYearId { get; set; }
    public FiscalYear? PreviousFiscalYear { get; set; }
    public bool IsCurrent { get; set; }

    public int? OpeningJournalEntryId { get; set; }
    public JournalEntry? OpeningJournalEntry { get; set; }
    public int? ClosingJournalEntryId { get; set; }
    public JournalEntry? ClosingJournalEntry { get; set; }

    public DateTime? OpenedAt { get; set; }
    public int? OpenedByUserId { get; set; }
    public User? OpenedByUser { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int? ClosedByUserId { get; set; }
    public User? ClosedByUser { get; set; }

    public ICollection<FiscalPeriod> Periods { get; set; } = [];
    public uint RowVersion { get; set; }
}
