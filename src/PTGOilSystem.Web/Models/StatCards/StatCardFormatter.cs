using System.Globalization;

namespace PTGOilSystem.Web.Models.StatCards;

/// <summary>
/// Central number formatter for stat cards so every page renders the value
/// identically: Latin digits, comma thousands separator, dot decimal separator,
/// null shown as an em dash. The formatter never appends the unit — the unit is
/// rendered on its own line by the component.
/// </summary>
public static class StatCardFormatter
{
    private static readonly CultureInfo Fmt = CultureInfo.InvariantCulture;

    /// <summary>Placeholder rendered for a null / missing value.</summary>
    public const string EmptyGlyph = "—";

    /// <summary>
    /// Format a decimal with a fixed number of decimal places and grouped
    /// thousands. <paramref name="decimals"/> defaults to 0 (whole numbers).
    /// </summary>
    public static string Number(decimal? value, int decimals = 0)
    {
        if (value is null) return EmptyGlyph;
        var pattern = decimals > 0 ? "#,##0." + new string('0', decimals) : "#,##0";
        return value.Value.ToString(pattern, Fmt);
    }

    /// <summary>Convenience overload for integer-like counts.</summary>
    public static string Count(long? value) =>
        value is null ? EmptyGlyph : value.Value.ToString("#,##0", Fmt);

    /// <summary>
    /// Format the trend percentage exactly as the reference shows it: a signed
    /// value with a single optional decimal and a trailing percent sign, e.g.
    /// "+8.4%" or "-3.1%". The arrow glyph is added by the component, not here.
    /// </summary>
    public static string Trend(decimal value)
    {
        var sign = value >= 0 ? "+" : "-";
        return sign + Math.Abs(value).ToString("0.#", Fmt) + "%";
    }
}
