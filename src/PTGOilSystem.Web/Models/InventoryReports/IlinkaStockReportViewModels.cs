using System.ComponentModel.DataAnnotations;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Models.InventoryReports;

public sealed class IlinkaStockReportFilterViewModel
{
    [Display(Name = "جنس")]
    public int? ProductId { get; set; }

    [Display(Name = "قرارداد منبع موجودی")]
    public int? ContractId { get; set; }

    [Display(Name = "ترمینال")]
    public int? TerminalId { get; set; }

    [Display(Name = "مخزن")]
    public int? StorageTankId { get; set; }

    [Display(Name = "از تاریخ")]
    [DataType(DataType.Date)]
    public DateTime? FromDate { get; set; }

    [Display(Name = "تا تاریخ")]
    [DataType(DataType.Date)]
    public DateTime? ToDate { get; set; }
}

public sealed class IlinkaStockReportRowViewModel
{
    public int MovementId { get; init; }
    public DateTime Date { get; init; }
    public string? Reference { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string? ContractNumber { get; init; }
    public string TerminalCode { get; init; } = string.Empty;
    public string TerminalName { get; init; } = string.Empty;
    public string? StorageTankCode { get; init; }
    public MovementDirection Direction { get; init; }
    public decimal InQuantityMt { get; init; }
    public decimal OutQuantityMt { get; init; }
    public decimal AdjustmentQuantityMt { get; init; }
    public decimal TransferQuantityMt { get; init; }
    public decimal RunningBalanceMt { get; init; }
    public string SourceType { get; init; } = string.Empty;
    public string? SourceReference { get; init; }
    public string? Notes { get; init; }
}

public sealed class IlinkaStockReportViewModel
{
    public IlinkaStockReportFilterViewModel Filter { get; init; } = new();
    public decimal OpeningBalanceMt { get; init; }
    public decimal ClosingBalanceMt { get; init; }
    public IReadOnlyList<IlinkaStockReportRowViewModel> Rows { get; init; } = [];
}
