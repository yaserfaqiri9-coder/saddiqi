using System;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services;

namespace PTGOilSystem.Web.Helpers;

/// <summary>
/// یگانه‌سرچشمهٔ محاسبهٔ ارزش/قفلِ روبلِ بارگیری.
/// هم LoadingController (هنگام ثبت بارگیری) و هم ContractsController (هنگام قطعی‌سازی نرخ)
/// از همین‌جا استفاده می‌کنند تا مبلغ روبل با منطق دوگانه دوباره‌محاسبه نشود.
/// این کلاس هیچ اثری بر لِجر/پرداخت/موجودی ندارد؛ فقط snapshot روی خود بارگیری را قفل می‌کند.
/// </summary>
public static class LoadingRubSettlement
{
    public static bool IsRubSettlement(string? currencyCode)
        => string.Equals(SystemCurrency.Normalize(currencyCode), "RUB", StringComparison.OrdinalIgnoreCase);

    public static decimal? CalculateLoadingValueUsd(decimal loadedQuantityMt, decimal? loadingPriceUsd)
        => loadingPriceUsd.HasValue && loadingPriceUsd.Value > 0m
            ? Math.Round(loadedQuantityMt * loadingPriceUsd.Value, 4)
            : null;

    public static decimal CalculateRubAmount(decimal amountUsd, decimal rubPerUsdRate)
        => Math.Round(amountUsd * rubPerUsdRate, 4, MidpointRounding.AwayFromZero);

    /// <summary>
    /// بارگیریِ روبلی‌ای که قیمت دالری‌اش تازه قطعی شده را قفل می‌کند.
    /// نرخِ روبل باید توسط فراخوان بر اساس سیاست قرارداد از قبل حل شده باشد
    /// (نرخ ثابت قرارداد یا نرخ ذخیره‌شدهٔ همان بارگیری — نه نرخ امروز).
    /// اگر تسویه روبلی نباشد، قبلاً قفل شده باشد، یا قیمت/نرخ نباشد، هیچ کاری نمی‌کند
    /// و بارگیری «در انتظار نرخ» باقی می‌ماند. فقط وقتی snapshot را تازه قفل کند true برمی‌گرداند.
    /// </summary>
    public static bool TryLockFinalizedRub(LoadingRegister loading, decimal? resolvedRubPerUsdRate, bool forceRelock = false)
    {
        ArgumentNullException.ThrowIfNull(loading);

        if (!IsRubSettlement(loading.SettlementCurrencyCode))
        {
            return false;
        }

        // در حالت عادی، بارگیریِ از پیش قفل‌شده دوباره قفل نمی‌شود (جلوگیری از تغییر تصادفی).
        // فقط مسیر صریحِ «اصلاح قیمت» با forceRelock=true اجازهٔ بازقفل با قیمت جدید را دارد.
        if (!forceRelock && loading.RubRateStatus == RubSettlementRateStatus.Locked)
        {
            return false;
        }

        var valueUsd = CalculateLoadingValueUsd(loading.LoadedQuantityMt, loading.LoadingPriceUsd);
        if (!valueUsd.HasValue)
        {
            return false;
        }

        if (!resolvedRubPerUsdRate.HasValue || resolvedRubPerUsdRate.Value <= 0m)
        {
            return false;
        }

        loading.RubPerUsdRate = resolvedRubPerUsdRate;
        loading.RubRateStatus = RubSettlementRateStatus.Locked;
        loading.AmountUsdAtRubLock = valueUsd.Value;
        loading.AmountRubAtRubLock = CalculateRubAmount(valueUsd.Value, resolvedRubPerUsdRate.Value);
        loading.RubRateDate ??= loading.LoadingDate.Date;
        return true;
    }
}
