using PTGOilSystem.Web.Models.Contracts;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Helpers;

public static class ContractPricingAdapter
{
    public const string PendingStatusLabel = "در انتظار نرخ";
    public const string CompletedStatusLabel = "تکمیل‌شده";
    public const string AgreedPricingLabel = "نرخ قطعی / توافقی";
    public const string PlattsDailyLabel = "Platt's روزانه";
    public const string PlattsMonthlyAverageLabel = "Platt's میانگین ماهانه";
    public const string PlattsManualDescriptiveLabel = "Platt's دستی / توضیحی";
    public const string PlattsGenericLabel = "نرخ Platt's";
    public const string PreviousDocumentsWarning =
        "با ذخیره نرخ نهایی، قیمت خرید بارگیری‌های قبلی همین قرارداد خودکار با نرخ قرارداد هماهنگ می‌شود؛ قیمت فروش و اسناد پرداخت/دریافت تغییر نمی‌کند.";

    public static decimal? GetCanonicalFinalPrice(Contract contract)
    {
        if (contract.ManualFinalPriceUsd.HasValue && contract.ManualFinalPriceUsd.Value > 0m)
        {
            return contract.ManualFinalPriceUsd.Value;
        }

        if (contract.PricingMethod == PricingMethod.Fixed
            && contract.UnitPriceUsd.HasValue
            && contract.UnitPriceUsd.Value > 0m)
        {
            return contract.UnitPriceUsd.Value;
        }

        return null;
    }

    public static bool UsesLegacyFixedFallback(Contract contract)
        => (!contract.ManualFinalPriceUsd.HasValue || contract.ManualFinalPriceUsd.Value <= 0m)
           && contract.PricingMethod == PricingMethod.Fixed
           && contract.UnitPriceUsd.HasValue
           && contract.UnitPriceUsd.Value > 0m;

    public static PricingCompletionStatus GetPricingStatus(Contract contract)
        => GetCanonicalFinalPrice(contract).HasValue
            ? PricingCompletionStatus.Completed
            : PricingCompletionStatus.Pending;

    public static string GetPricingStatusLabel(Contract contract)
        => GetPricingStatus(contract) == PricingCompletionStatus.Completed
            ? CompletedStatusLabel
            : PendingStatusLabel;

    public static UiPricingType GetUiPricingType(Contract contract)
        => contract.PricingMethod == PricingMethod.FormulaPlatts
            ? UiPricingType.Platts
            : UiPricingType.Agreed;

    public static string GetPricingDisplayLabel(Contract contract)
        => contract.PricingMethod == PricingMethod.FormulaPlatts
            ? GetPlattsDisplayMode(contract)
            : AgreedPricingLabel;

    public static string GetPlattsDisplayMode(Contract contract)
        => contract.PricingMethod == PricingMethod.FormulaPlatts
            ? contract.PlattsPeriodType switch
            {
                PlattsPeriodType.Daily => PlattsDailyLabel,
                PlattsPeriodType.Monthly => PlattsMonthlyAverageLabel,
                PlattsPeriodType.Manual => PlattsManualDescriptiveLabel,
                _ => PlattsGenericLabel
            }
            : string.Empty;

    public static PlattsUiMode GetPlattsUiMode(Contract contract)
        => contract.PlattsPeriodType switch
        {
            PlattsPeriodType.Daily => PlattsUiMode.Daily,
            PlattsPeriodType.Monthly => PlattsUiMode.MonthlyAverage,
            _ => PlattsUiMode.ManualDescriptive
        };

    public static PricingMethod ToPricingMethod(UiPricingType pricingType)
        => pricingType == UiPricingType.Platts
            ? PricingMethod.FormulaPlatts
            : PricingMethod.ManualFinalPrice;

    public static PlattsPeriodType ToPlattsPeriodType(PlattsUiMode mode)
        => mode switch
        {
            PlattsUiMode.Daily => PlattsPeriodType.Daily,
            PlattsUiMode.MonthlyAverage => PlattsPeriodType.Monthly,
            _ => PlattsPeriodType.Manual
        };

    public static string FormatPrice(decimal? value)
        => value.HasValue ? $"{value.Value:N2} USD/MT" : "در انتظار نرخ";
}
