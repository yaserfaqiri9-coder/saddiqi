using Microsoft.AspNetCore.Mvc;
using PTGOilSystem.Web.Models.StatCards;

namespace PTGOilSystem.Web.ViewComponents;

/// <summary>
/// Shared statistic card. Usage:
/// <code>
/// &lt;vc:stat-card title="کل محموله‌ها" value="714" unit="محموله"
///               avatar="shipments" trend-value="8.4" trend-direction="up"&gt;&lt;/vc:stat-card&gt;
/// </code>
/// The component only maps prepared values onto <see cref="StatCardViewModel"/> —
/// it performs no business calculation and no number crunching beyond formatting
/// the supplied trend value.
/// </summary>
public sealed class StatCardViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        StatCardViewModel? model = null,
        string? title = null,
        string? value = null,
        string? unit = null,
        string? avatar = null,
        string? avatarPath = null,
        decimal? trendValue = null,
        string? trendDirection = null,
        string? trendTone = null,
        string? state = null,
        string? cssClass = null,
        string? ariaLabel = null)
    {
        // Callers may hand over a fully-built model instead of primitive args.
        if (model is not null) return View(model);

        var direction = ParseDirection(trendDirection);
        var vm = new StatCardViewModel
        {
            Title = title ?? string.Empty,
            Value = value ?? string.Empty,
            Unit = unit,
            AvatarKey = avatar,
            AvatarPath = avatarPath,
            TrendValue = trendValue.HasValue ? StatCardFormatter.Trend(trendValue.Value) : null,
            TrendDirection = direction,
            TrendTone = ParseTone(trendTone, direction, trendValue),
            State = ParseState(state),
            CssClass = cssClass,
            AriaLabel = ariaLabel
        };
        return View(vm);
    }

    private static StatCardTrendDirection ParseDirection(string? v) => v?.Trim().ToLowerInvariant() switch
    {
        "up" or "positive" or "increase" => StatCardTrendDirection.Up,
        "down" or "negative" or "decrease" => StatCardTrendDirection.Down,
        _ => StatCardTrendDirection.None
    };

    private static StatCardTrendTone ParseTone(string? v, StatCardTrendDirection dir, decimal? trendValue)
    {
        switch (v?.Trim().ToLowerInvariant())
        {
            case "positive" or "up" or "good": return StatCardTrendTone.Positive;
            case "negative" or "down" or "bad": return StatCardTrendTone.Negative;
            case "neutral": return StatCardTrendTone.Neutral;
        }
        // Derive tone from the direction, then from the sign of the value.
        if (dir == StatCardTrendDirection.Up) return StatCardTrendTone.Positive;
        if (dir == StatCardTrendDirection.Down) return StatCardTrendTone.Negative;
        if (trendValue is > 0) return StatCardTrendTone.Positive;
        if (trendValue is < 0) return StatCardTrendTone.Negative;
        return StatCardTrendTone.Neutral;
    }

    private static StatCardState ParseState(string? v) => v?.Trim().ToLowerInvariant() switch
    {
        "warning" => StatCardState.Warning,
        "loading" => StatCardState.Loading,
        "empty" => StatCardState.Empty,
        _ => StatCardState.Default
    };
}
