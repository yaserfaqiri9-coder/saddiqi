using System.ComponentModel.DataAnnotations;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Models.Contracts;

public class EditPricingViewModel
{
    public int Id { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public PricingMethod PricingMethod { get; set; }

    [Display(Name = "نوع نرخ")]
    public UiPricingType UiPricingType { get; set; } = UiPricingType.Agreed;

    [Display(Name = "نوع Platt's")]
    public PlattsUiMode PlattsUiMode { get; set; } = PlattsUiMode.ManualDescriptive;

    [Display(Name = "قیمت نهایی USD/MT")]
    [Range(typeof(decimal), "0.0001", "79228162514264337593543950335", ErrorMessage = "قیمت نهایی باید بزرگ‌تر از صفر باشد.")]
    public decimal? FinalPriceUsdPerMt { get; set; }

    [Display(Name = "یادداشت نرخ")]
    [StringLength(500)]
    public string? PricingNote { get; set; }

    public bool IsPricingCompleted { get; set; }

    public string? ReturnUrl { get; set; }

    [Display(Name = "نوع دوره Platt's")]
    public PlattsPeriodType? PlattsPeriodType { get; set; }

    [Display(Name = "قیمت پایه Platt's (USD/MT)")]
    public decimal? PlattsManualPriceUsd { get; set; }

    [Display(Name = "Premium / Discount USD/MT")]
    public decimal? PremiumDiscountUsd { get; set; }

    [Display(Name = "توضیح فرمول قیمت")]
    [StringLength(500)]
    public string? PricingFormulaNote { get; set; }

    [Display(Name = "قیمت نهایی توافقی USD/MT")]
    public decimal? ManualFinalPriceUsd { get; set; }

    // قیمت روبلی کل قرارداد — فقط وقتی سیاست روبل قرارداد «نرخ بعداً مشخص می‌شود» باشد
    // در همین فرم تکمیل نرخ قابل ورود است. این نرخ برای کل قرارداد است، نه هر بارگیری.
    public RubSettlementRatePolicy RubRatePolicy { get; set; } = RubSettlementRatePolicy.NotApplicable;

    public bool ShowRubRateEntry { get; set; }

    [Display(Name = "هر 1 دالر چند روبل است؟")]
    [Range(typeof(decimal), "0.000001", "79228162514264337593543950335", ErrorMessage = "نرخ روبل باید بزرگ‌تر از صفر باشد.")]
    public decimal? ContractRubPerUsdRate { get; set; }

    [Display(Name = "تاریخ نرخ روبل")]
    public DateTime? ContractRubRateDate { get; set; }

    [Display(Name = "مرجع نرخ روبل")]
    [StringLength(200)]
    public string? ContractRubRateSource { get; set; }
}
