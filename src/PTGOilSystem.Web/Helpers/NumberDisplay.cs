using System.Globalization;

namespace PTGOilSystem.Web.Helpers;

/// <summary>
/// نمایش یک‌دستِ اعداد در کل سیستم (فقط نمایش — هیچ محاسبه/ذخیره‌ای اینجا نیست).
/// قواعد:
///   • پول (Money):      جداکنندهٔ هزارگان، حداکثر ۲ اعشار، صفرهای آخر حذف.
///   • مقدار (Quantity): جداکنندهٔ هزارگان، حداکثر ۳ اعشار، صفرهای آخر حذف.
///   • قیمت واحد:        جداکنندهٔ هزارگان، حداقل ۲ تا حداکثر ۴ اعشار.
///   • نرخ ارز (Fx):     جداکنندهٔ هزارگان، حداکثر ۶ اعشار، صفرهای آخر حذف.
///   • فیصدی (Percent):  حداکثر ۲ اعشار + علامت ٪.
/// همهٔ خروجی‌ها با علامت LTR ایزوله می‌شوند تا در صفحهٔ RTL درست خوانده شوند
/// (مطابق الگوی <see cref="DateDisplay"/>).
/// </summary>
public static class NumberDisplay
{
    public const string EmptyValue = "—";

    private const char LeftToRightMark = '‎';
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    public static string Money(decimal value, string? unit = null)
        => Compose(value, maxDecimals: 2, minDecimals: 0, unit);

    public static string Money(decimal? value, string? unit = null, string empty = EmptyValue)
        => value.HasValue ? Money(value.Value, unit) : empty;

    public static string Quantity(decimal value, string? unit = null)
        => Compose(value, maxDecimals: 3, minDecimals: 0, unit);

    public static string Quantity(decimal? value, string? unit = null, string empty = EmptyValue)
        => value.HasValue ? Quantity(value.Value, unit) : empty;

    public static string UnitPrice(decimal value, string? unit = null)
        => Compose(value, maxDecimals: 4, minDecimals: 2, unit);

    public static string UnitPrice(decimal? value, string? unit = null, string empty = EmptyValue)
        => value.HasValue ? UnitPrice(value.Value, unit) : empty;

    public static string FxRate(decimal value, string? unit = null)
        => Compose(value, maxDecimals: 6, minDecimals: 0, unit);

    public static string FxRate(decimal? value, string? unit = null, string empty = EmptyValue)
        => value.HasValue ? FxRate(value.Value, unit) : empty;

    public static string Percent(decimal value)
        => Isolate(Raw(value, maxDecimals: 2, minDecimals: 0) + "٪");

    public static string Percent(decimal? value, string empty = EmptyValue)
        => value.HasValue ? Percent(value.Value) : empty;

    public static string Number(decimal value, int maxDecimals = 2, int minDecimals = 0)
        => Compose(value, maxDecimals, minDecimals, unit: null);

    public static string Number(decimal? value, int maxDecimals = 2, int minDecimals = 0, string empty = EmptyValue)
        => value.HasValue ? Number(value.Value, maxDecimals, minDecimals) : empty;

    /// <summary>خروجی بدون علامت LTR و بدون واحد — برای جاهایی که خودِ صفحه ایزوله می‌کند.</summary>
    public static string Raw(decimal value, int maxDecimals, int minDecimals = 0)
    {
        if (minDecimals < 0) minDecimals = 0;
        if (maxDecimals < minDecimals) maxDecimals = minDecimals;

        var pattern = "#,##0";
        if (maxDecimals > 0)
        {
            pattern += "." + new string('0', minDecimals) + new string('#', maxDecimals - minDecimals);
        }

        var text = value.ToString(pattern, InvariantCulture);
        // "-0" را به "0" تبدیل کن (گرد شدن مقدار خیلی کوچک منفی)
        if (text is "-0") text = "0";
        return text;
    }

    private static string Compose(decimal value, int maxDecimals, int minDecimals, string? unit)
    {
        var text = Raw(value, maxDecimals, minDecimals);
        if (!string.IsNullOrWhiteSpace(unit))
        {
            text = text + " " + unit.Trim();
        }
        return Isolate(text);
    }

    private static string Isolate(string value)
        => string.Concat(LeftToRightMark, value, LeftToRightMark);
}

public static class NumberDisplayExtensions
{
    public static string ToMoney(this decimal value, string? unit = null) => NumberDisplay.Money(value, unit);
    public static string ToMoney(this decimal? value, string? unit = null) => NumberDisplay.Money(value, unit);

    public static string ToQuantity(this decimal value, string? unit = null) => NumberDisplay.Quantity(value, unit);
    public static string ToQuantity(this decimal? value, string? unit = null) => NumberDisplay.Quantity(value, unit);

    public static string ToUnitPrice(this decimal value, string? unit = null) => NumberDisplay.UnitPrice(value, unit);
    public static string ToUnitPrice(this decimal? value, string? unit = null) => NumberDisplay.UnitPrice(value, unit);

    public static string ToFxRate(this decimal value, string? unit = null) => NumberDisplay.FxRate(value, unit);
    public static string ToFxRate(this decimal? value, string? unit = null) => NumberDisplay.FxRate(value, unit);

    public static string ToPercentDisplay(this decimal value) => NumberDisplay.Percent(value);
    public static string ToPercentDisplay(this decimal? value) => NumberDisplay.Percent(value);

    public static string ToNumberDisplay(this decimal value, int maxDecimals = 2, int minDecimals = 0)
        => NumberDisplay.Number(value, maxDecimals, minDecimals);
    public static string ToNumberDisplay(this decimal? value, int maxDecimals = 2, int minDecimals = 0)
        => NumberDisplay.Number(value, maxDecimals, minDecimals);
}
