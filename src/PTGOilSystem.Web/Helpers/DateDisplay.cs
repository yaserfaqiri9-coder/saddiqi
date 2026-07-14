using System.Globalization;

namespace PTGOilSystem.Web.Helpers;

public static class DateDisplay
{
    public const string DisplayDatePattern = "yyyy/MM/dd";
    public const string DisplayDateTimePattern = "yyyy/MM/dd HH:mm";
    public const string DisplayMonthPattern = "yyyy/MM";
    public const string HtmlDateInputPattern = "yyyy-MM-dd";
    public const string HtmlMonthInputPattern = "yyyy-MM";

    private const string EmptyValue = "-";
    private const char LeftToRightMark = '\u200E';
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    public static string Date(DateTime value)
        => Isolate(value.ToString(DisplayDatePattern, InvariantCulture));

    public static string Date(DateTime? value, string empty = EmptyValue)
        => value.HasValue ? Date(value.Value) : empty;

    public static string DateTime(DateTime value)
        => Isolate(value.ToString(DisplayDateTimePattern, InvariantCulture));

    public static string DateTime(DateTime? value, string empty = EmptyValue)
        => value.HasValue ? DateTime(value.Value) : empty;

    public static string Month(DateTime value)
        => Isolate(value.ToString(DisplayMonthPattern, InvariantCulture));

    public static string Month(DateTime? value, string empty = EmptyValue)
        => value.HasValue ? Month(value.Value) : empty;

    public static string HtmlDateInput(DateTime value)
        => value.ToString(HtmlDateInputPattern, InvariantCulture);

    public static string HtmlDateInput(DateTime? value)
        => value.HasValue ? HtmlDateInput(value.Value) : string.Empty;

    public static string HtmlMonthInput(DateTime value)
        => value.ToString(HtmlMonthInputPattern, InvariantCulture);

    public static string HtmlMonthInput(DateTime? value)
        => value.HasValue ? HtmlMonthInput(value.Value) : string.Empty;

    private static string Isolate(string value)
        => string.Concat(LeftToRightMark, value, LeftToRightMark);
}

public static class DateDisplayExtensions
{
    public static string ToDisplayDate(this DateTime value)
        => DateDisplay.Date(value);

    public static string ToDisplayDate(this DateTime? value)
        => DateDisplay.Date(value);

    public static string ToDisplayDateTime(this DateTime value)
        => DateDisplay.DateTime(value);

    public static string ToDisplayDateTime(this DateTime? value)
        => DateDisplay.DateTime(value);

    public static string ToDisplayMonth(this DateTime value)
        => DateDisplay.Month(value);

    public static string ToDisplayMonth(this DateTime? value)
        => DateDisplay.Month(value);

    public static string ToHtmlDateInput(this DateTime value)
        => DateDisplay.HtmlDateInput(value);

    public static string ToHtmlDateInput(this DateTime? value)
        => DateDisplay.HtmlDateInput(value);

    public static string ToHtmlMonthInput(this DateTime value)
        => DateDisplay.HtmlMonthInput(value);

    public static string ToHtmlMonthInput(this DateTime? value)
        => DateDisplay.HtmlMonthInput(value);
}
