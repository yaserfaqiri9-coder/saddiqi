using System.ComponentModel.DataAnnotations;

namespace PTGOilSystem.Web.Models.Entities;

public class FiscalYearStatusHistory : BaseEntity
{
    public int FiscalYearId { get; set; }
    public FiscalYear? FiscalYear { get; set; }
    public FiscalYearStatus OldStatus { get; set; }
    public FiscalYearStatus NewStatus { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = "";

    public DateTime ChangedAt { get; set; }
    public int? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }
}
