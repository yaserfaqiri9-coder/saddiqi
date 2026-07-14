using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PTGOilSystem.Web.Models.Entities;

public enum ThreeWayPayeeType
{
    [Display(Name = "تأمین‌کننده")]
    Supplier = 1,

    [Display(Name = "صراف")]
    Sarraf = 2,

    [Display(Name = "حساب دیگر")]
    OtherAccount = 3
}

public enum ThreeWaySettlementStatus
{
    Draft = 1,
    Posted = 2,
    Cancelled = 3
}

public class ThreeWaySettlement : BaseEntity
{
    public DateTime SettlementDate { get; set; }
    public ThreeWayPayeeType PayeeType { get; set; } = ThreeWayPayeeType.Supplier;
    public ThreeWaySettlementStatus Status { get; set; } = ThreeWaySettlementStatus.Posted;

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public int? SarrafId { get; set; }
    public Sarraf? Sarraf { get; set; }

    [MaxLength(200)]
    public string? OtherPayeeName { get; set; }

    public decimal CustomerPaidAmount { get; set; }
    public decimal SupplierAcceptedAmount { get; set; }

    // ارز/نرخ پایه (legacy). برای رکوردهای قدیمی و به‌عنوان مقدار نماینده/پیش‌فرض نگه داشته می‌شود.
    [Required, MaxLength(10)]
    public string Currency { get; set; } = "USD";

    public decimal FxRateToUsd { get; set; } = 1m;

    // فاز B1 — چندارزی: ارز و نرخ مستقل برای هر طرف. nullable برای سازگاری با رکوردهای قدیمی؛
    // اگر null باشد، Effective* به Currency/FxRateToUsd بالا برمی‌گردد (رفتار تک‌ارز قبلی).
    [MaxLength(10)]
    public string? CustomerPaidCurrency { get; set; }
    public decimal? CustomerPaidFxRateToUsd { get; set; }

    [MaxLength(10)]
    public string? SupplierAcceptedCurrency { get; set; }
    public decimal? SupplierAcceptedFxRateToUsd { get; set; }

    public decimal CustomerPaidUsd { get; set; }
    public decimal SupplierAcceptedUsd { get; set; }
    public decimal DifferenceUsd { get; set; }
    public DifferenceReason? DifferenceReason { get; set; }

    [NotMapped]
    public string EffectiveCustomerPaidCurrency
        => string.IsNullOrWhiteSpace(CustomerPaidCurrency) ? Currency : CustomerPaidCurrency!;

    [NotMapped]
    public decimal EffectiveCustomerPaidFxRateToUsd
        => CustomerPaidFxRateToUsd is > 0m ? CustomerPaidFxRateToUsd.Value : FxRateToUsd;

    [NotMapped]
    public string EffectiveSupplierAcceptedCurrency
        => string.IsNullOrWhiteSpace(SupplierAcceptedCurrency) ? Currency : SupplierAcceptedCurrency!;

    [NotMapped]
    public decimal EffectiveSupplierAcceptedFxRateToUsd
        => SupplierAcceptedFxRateToUsd is > 0m ? SupplierAcceptedFxRateToUsd.Value : FxRateToUsd;

    public int? CustomerSaleContractId { get; set; }
    public Contract? CustomerSaleContract { get; set; }

    public int? SupplierPurchaseContractId { get; set; }
    public Contract? SupplierPurchaseContract { get; set; }

    [MaxLength(200)]
    public string? HawalaReference { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime? PostedAtUtc { get; set; }

    [MaxLength(150)]
    public string? CreatedByUserName { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    [MaxLength(150)]
    public string? CancelledByUserName { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }

    public int? CustomerLedgerEntryId { get; set; }
    public LedgerEntry? CustomerLedgerEntry { get; set; }

    public int? SupplierLedgerEntryId { get; set; }
    public LedgerEntry? SupplierLedgerEntry { get; set; }
}
