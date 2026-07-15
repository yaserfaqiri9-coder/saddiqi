using System.ComponentModel.DataAnnotations;

namespace PTGOilSystem.Web.Models.Entities;

public class FiscalPeriod : BaseEntity
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }
    public int FiscalYearId { get; set; }
    public FiscalYear? FiscalYear { get; set; }
    public int PeriodNumber { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public FiscalPeriodStatus Status { get; set; } = FiscalPeriodStatus.Open;
    public DateTime? LockedAt { get; set; }
    public int? LockedByUserId { get; set; }
    public User? LockedByUser { get; set; }
    public uint RowVersion { get; set; }
}
