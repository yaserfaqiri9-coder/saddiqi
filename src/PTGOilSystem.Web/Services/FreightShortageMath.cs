namespace PTGOilSystem.Web.Services;

// محاسبات مشترک کسری/تلورانس/کرایه برای مسیرهای حمل (دیسپچ موتر، رسید انتقال، رسید کشتی).
// این کلاس فقط «فرمول خام» را نگه می‌دارد؛ گرد کردن (rounding) و null-semantics هر call site
// در همان‌جا اعمال می‌شود تا خروجی عددیِ هر مسیر دقیقاً مثل قبل بماند.
// هیچ داده‌ای نمی‌خواند و چیزی ذخیره نمی‌کند؛ کاملاً pure است.
public static class FreightShortageMath
{
    // کرایهٔ ناخالص = مقدار × نرخِ فی‌تن، گردشده به ۴ رقم (AwayFromZero).
    // فرمول یکسانی که در رسید انتقال و بارگیریِ کشتی/واگون تکرار شده بود.
    public static decimal GrossFreightUsd(decimal quantityMt, decimal ratePerMtUsd)
        => System.Math.Round(quantityMt * ratePerMtUsd, 4, System.MidpointRounding.AwayFromZero);

    // کسری قابل مجرا = کسری منهای تلورانس/مجاز، هرگز منفی نمی‌شود.
    public static decimal ChargeableShortage(decimal shortageMt, decimal allowanceMt)
        => System.Math.Max(0m, shortageMt - allowanceMt);

    // جریمهٔ کسری راننده (خام، بدون گرد کردن) = کسری قابل مجرا × نرخ کسری.
    // فقط وقتی نرخ و کسری قابل مجرا هر دو مثبت باشند مبلغ دارد؛ در غیر این صورت صفر.
    public static decimal ShortageChargeUsd(decimal chargeableShortageMt, decimal? shortageRateUsd)
        => shortageRateUsd.HasValue && shortageRateUsd.Value > 0m && chargeableShortageMt > 0m
            ? chargeableShortageMt * shortageRateUsd.Value
            : 0m;

    // کرایهٔ قابل پرداخت (خام، بدون گرد کردن) = کرایهٔ ناخالص منهای جریمهٔ کسری قابل مجرا.
    // اگر کرایه نامشخص باشد null؛ اگر نرخ کسری نباشد یا صفر باشد، خودِ کرایهٔ ناخالص برمی‌گردد.
    // اولویت با chargeableShortageMtOverride است؛ در نبودِ آن از shortage منهای tolerance/allowance ساخته می‌شود.
    public static decimal? FreightPayableUsd(
        decimal? freightCostUsd,
        decimal? shortageMt,
        decimal? allowanceMt,
        decimal? shortageRateUsd,
        decimal? toleranceMt = null,
        decimal? chargeableShortageMtOverride = null)
    {
        if (!freightCostUsd.HasValue) return null;
        if (!shortageRateUsd.HasValue || shortageRateUsd <= 0m) return freightCostUsd;

        decimal chargeableShortage;
        if (chargeableShortageMtOverride.HasValue)
        {
            chargeableShortage = System.Math.Max(0m, chargeableShortageMtOverride.Value);
        }
        else
        {
            var effectiveTolerance = toleranceMt ?? allowanceMt ?? 0m;
            chargeableShortage = System.Math.Max(0m, (shortageMt ?? 0m) - effectiveTolerance);
        }

        return freightCostUsd.Value - (chargeableShortage * shortageRateUsd.Value);
    }
}
