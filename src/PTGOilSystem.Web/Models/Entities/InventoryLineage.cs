using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PTGOilSystem.Web.Models.Entities;

// ============================================================================
// Inventory Lineage (Phase 2) — لایهٔ موازیِ ردیابی بار برای «پرونده کشتی».
//
// این لایه روی InventoryMovement/StockService/Ledger سوار می‌شود و آن‌ها را
// عوض نمی‌کند. منبع حقیقت موجودی فیزیکی هنوز InventoryMovement است؛ این جدول‌ها
// فقط «نسب‌نامهٔ» بار را نگه می‌دارند: هر مقدار بار از کدام کشتی/قرارداد آمده،
// از کجا به کجا منتقل شده، و کجا فروش/کسری/مصرف/گمرک خورده است.
//
// همهٔ نوشتن‌ها پشت feature flag «Lineage:WriteLots» هستند و خواندن در پرونده کشتی
// پشت «Lineage:UseInPnl». با flag خاموش، رفتار سیستم دقیقاً مثل قبل است.
// ============================================================================

// منشأ یک Lot ریشه/مشتق.
public enum InventoryLotSourceType
{
    VesselInbound = 1,        // تخلیهٔ کشتی به مخزن کشور ثالث (ریشهٔ محموله)
    TransportReceipt = 2,     // رسید مقصدِ یک حمل (واگن/موتر) → Lot جدید در مقصد
    LoadingReceiptAllocation = 3, // ورود از مسیر LoadingReceipt (بدون leg)
    TankTransfer = 4,         // انتقال تانک‌به‌تانک
    TruckLeg = 5,             // مقصد یک حملِ موتری
    WagonLeg = 6,             // مقصد یک حملِ واگنی
    DirectReceipt = 7,        // ورود مستقیم بدون مرحلهٔ کشتی
    LegacyOpening = 8,        // موجودی افتتاحیه/قدیمی بدون نسب‌نامه
    FreeStock = 9,            // موجودی آزاد بدون ریشهٔ مشخص
    Adjustment = 10           // تعدیل
}

public enum InventoryLotStatus
{
    Open = 0,
    Depleted = 1,
    Cancelled = 2
}

// سطح اطمینان نسب‌نامه — جایگزین boolean. روی Lot و هر allocation/movement ذخیره می‌شود.
public enum LineageConfidence
{
    Verified = 0,     // اتصال قطعی (leg با ShipmentId + رسید/حرکت)
    Estimated = 1,    // تخمینی (FIFO/استنتاج از Vessel)
    Legacy = 2,       // دادهٔ قدیمی بدون اتصال
    NeedsReview = 3   // نیازمند بازبینی انسانی
}

// نوع یک حرکت نسب‌نامه‌ای بین Lotها.
public enum InventoryLotMovementKind
{
    VesselInbound = 1,
    TankTransfer = 2,
    TruckLeg = 3,
    WagonLeg = 4,
    DirectSale = 5,
    SaleOut = 6,
    Loss = 7,
    Expense = 8,
    Customs = 9,
    Adjustment = 10,
    LegacyOpening = 11
}

public enum InventoryLotMovementStatus
{
    Draft = 0,
    Loaded = 1,
    InTransit = 2,
    Received = 3,
    Cancelled = 4
}

// روش تخصیص یک allocation به Lot.
public enum LotAllocationMethod
{
    FIFO = 0,
    Manual = 1,
    Proportional = 2,
    LegacyEstimated = 3
}

/// <summary>
/// یک «بستهٔ بار» قابل ردیابی: مقداری از یک محصول در یک ترمینال/مخزن که می‌داند
/// ریشه‌اش کدام کشتی و کدام قرارداد است. هر انتقال یک Lot جدید در مقصد می‌سازد.
/// </summary>
public class InventoryLot : BaseEntity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int TerminalId { get; set; }
    public Terminal? Terminal { get; set; }
    public int? StorageTankId { get; set; }
    public StorageTank? StorageTank { get; set; }

    public decimal QuantityMt { get; set; }
    public decimal RemainingQuantityMt { get; set; }

    // ریشهٔ نسب‌نامه: اگر بار از کشتی نیست، RootShipmentId خالی می‌ماند.
    public int? RootShipmentId { get; set; }
    public Shipment? RootShipment { get; set; }
    public int? RootContractId { get; set; }
    public Contract? RootContract { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    // زنجیرهٔ انتقال: Lot مقصد به Lot مبدأ اشاره می‌کند.
    public int? ParentLotId { get; set; }
    public InventoryLot? ParentLot { get; set; }
    public ICollection<InventoryLot> Children { get; set; } = new List<InventoryLot>();

    public InventoryLotSourceType SourceType { get; set; }
    [MaxLength(40)] public string? SourceReferenceType { get; set; }
    public int? SourceReferenceId { get; set; }

    // حرکت موجودی فیزیکیِ ورودی که این Lot را ساخت (پل به InventoryMovement).
    public int? CreatedFromMovementId { get; set; }
    public InventoryMovement? CreatedFromMovement { get; set; }

    public InventoryLotStatus Status { get; set; } = InventoryLotStatus.Open;
    public LineageConfidence LineageConfidence { get; set; } = LineageConfidence.Estimated;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(1000)] public string? Notes { get; set; }
}

/// <summary>
/// یک حرکت نسب‌نامه‌ای: مقداری از یک Lot مبدأ بارگیری و در مقصد به Lot جدید تبدیل می‌شود.
/// انتقال تانک‌به‌تانک، حملِ موتر/واگن، و فروش/کسری همگی با همین مدل بیان می‌شوند
/// (نیاز به TransportMovement جدا نیست).
/// </summary>
public class InventoryLotMovement : BaseEntity
{
    public int? FromLotId { get; set; }
    public InventoryLot? FromLot { get; set; }
    public int? ToLotId { get; set; }
    public InventoryLot? ToLot { get; set; }

    public InventoryLotMovementKind MovementKind { get; set; }

    public int? FromTerminalId { get; set; }
    public Terminal? FromTerminal { get; set; }
    public int? FromStorageTankId { get; set; }
    public StorageTank? FromStorageTank { get; set; }
    public int? ToTerminalId { get; set; }
    public Terminal? ToTerminal { get; set; }
    public int? ToStorageTankId { get; set; }
    public StorageTank? ToStorageTank { get; set; }

    // نوع وسیله (در صورت وجود) — از enum موجودِ بارگیری استفاده می‌شود.
    public LoadingTransportType? VehicleType { get; set; }
    [MaxLength(40)] public string? VehicleRefType { get; set; }
    public int? VehicleRefId { get; set; }

    public decimal LoadedQuantityMt { get; set; }
    public decimal? ReceivedQuantityMt { get; set; }
    public decimal? ShortageQuantityMt { get; set; }
    public DateTime MovementDate { get; set; }
    public InventoryLotMovementStatus Status { get; set; } = InventoryLotMovementStatus.Draft;

    // فقط برای راحتیِ گزارش/فیلتر؛ منبع حقیقت ریشه همچنان Lot است.
    public int? ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }

    [MaxLength(40)] public string? SourceReferenceType { get; set; }
    public int? SourceReferenceId { get; set; }
    public int? InventoryMovementId { get; set; }
    public InventoryMovement? InventoryMovement { get; set; }

    public LineageConfidence LineageConfidence { get; set; } = LineageConfidence.Estimated;
    [MaxLength(1000)] public string? Notes { get; set; }
}

/// <summary>تخصیص یک فروش به یک Lot (FIFO پیش‌فرض، با امکان override دستی).</summary>
public class SaleLotAllocation : BaseEntity
{
    public int SalesTransactionId { get; set; }
    public SalesTransaction? SalesTransaction { get; set; }
    public int LotId { get; set; }
    public InventoryLot? InventoryLot { get; set; }

    public decimal QuantityMt { get; set; }
    public decimal? AmountUsd { get; set; }
    public decimal? UnitCostUsd { get; set; }
    public LotAllocationMethod AllocationMethod { get; set; } = LotAllocationMethod.FIFO;
    public LineageConfidence LineageConfidence { get; set; } = LineageConfidence.Estimated;
    [MaxLength(1000)] public string? Notes { get; set; }
}

/// <summary>تخصیص یک رویداد کسری/ضایعه به یک Lot.</summary>
public class LossLotAllocation : BaseEntity
{
    public int LossEventId { get; set; }
    public LossEvent? LossEvent { get; set; }
    public int LotId { get; set; }
    public InventoryLot? InventoryLot { get; set; }

    public decimal QuantityMt { get; set; }
    public decimal? ValueUsd { get; set; }
    public LotAllocationMethod AllocationMethod { get; set; } = LotAllocationMethod.Proportional;
    public LineageConfidence LineageConfidence { get; set; } = LineageConfidence.Estimated;
    [MaxLength(1000)] public string? Notes { get; set; }
}

/// <summary>تخصیص یک مصرف به یک Lot (مبلغ USD).</summary>
public class ExpenseLotAllocation : BaseEntity
{
    public int ExpenseTransactionId { get; set; }
    public ExpenseTransaction? ExpenseTransaction { get; set; }
    public int LotId { get; set; }
    public InventoryLot? InventoryLot { get; set; }

    public decimal AmountUsd { get; set; }
    public LotAllocationMethod AllocationMethod { get; set; } = LotAllocationMethod.Proportional;
    public LineageConfidence LineageConfidence { get; set; } = LineageConfidence.Estimated;
    [MaxLength(1000)] public string? Notes { get; set; }
}
