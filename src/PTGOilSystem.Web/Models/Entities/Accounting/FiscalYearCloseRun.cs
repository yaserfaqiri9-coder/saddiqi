using System.ComponentModel.DataAnnotations;

namespace PTGOilSystem.Web.Models.Entities;

public class FiscalYearCloseRun : BaseEntity
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }
    public int FiscalYearId { get; set; }
    public FiscalYear? FiscalYear { get; set; }
    public FiscalYearCloseRunStatus Status { get; set; } = FiscalYearCloseRunStatus.Pending;
    public DateTime StartedAt { get; set; }
    public int? StartedByUserId { get; set; }
    public User? StartedByUser { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CompletedByUserId { get; set; }
    public User? CompletedByUser { get; set; }
    public int? ClosingJournalEntryId { get; set; }
    public JournalEntry? ClosingJournalEntry { get; set; }
    public int? OpeningJournalEntryId { get; set; }
    public JournalEntry? OpeningJournalEntry { get; set; }

    [MaxLength(2000)]
    public string? FailureMessage { get; set; }

    public uint RowVersion { get; set; }
}
