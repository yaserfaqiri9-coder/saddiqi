namespace PTGOilSystem.Web.Models.StatCards;

/// <summary>
/// Presentation model for a single statistic card. The component only renders
/// prepared data — no business calculation happens here. Callers supply an
/// already-formatted <see cref="Value"/> (typically via <see cref="StatCardFormatter"/>).
/// </summary>
public sealed class StatCardViewModel
{
    /// <summary>Small heading shown at the top of the text block.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The main, already-formatted number (e.g. "1,246,780,000").</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Unit rendered on its own line under the value (e.g. "محموله").</summary>
    public string? Unit { get; set; }

    /// <summary>Logical avatar key resolved through <see cref="StatCardAvatarRegistry"/>.</summary>
    public string? AvatarKey { get; set; }

    /// <summary>Explicit illustration path; overrides the registry lookup when set.</summary>
    public string? AvatarPath { get; set; }

    /// <summary>Formatted trend text without the arrow, e.g. "+8.4%". Null hides the trend.</summary>
    public string? TrendValue { get; set; }

    /// <summary>Arrow direction beside the trend value.</summary>
    public StatCardTrendDirection TrendDirection { get; set; } = StatCardTrendDirection.None;

    /// <summary>Colour tone of the trend text.</summary>
    public StatCardTrendTone TrendTone { get; set; } = StatCardTrendTone.Neutral;

    /// <summary>Overall card state (default / warning / loading / empty).</summary>
    public StatCardState State { get; set; } = StatCardState.Default;

    /// <summary>Accessible label for the whole card. Falls back to Title + Value.</summary>
    public string? AriaLabel { get; set; }

    /// <summary>Extra CSS class hook appended to the root element.</summary>
    public string? CssClass { get; set; }

    // ── Convenience view helpers (no business logic) ────────────────────────

    public bool IsLoading => State == StatCardState.Loading;
    public bool IsEmpty => State == StatCardState.Empty;
    public bool IsWarning => State == StatCardState.Warning;

    /// <summary>Resolved illustration path (explicit path wins over the registry).</summary>
    public string? ResolvedAvatarPath =>
        !string.IsNullOrWhiteSpace(AvatarPath)
            ? AvatarPath
            : StatCardAvatarRegistry.ResolvePath(AvatarKey);

    /// <summary>Aria label used by the component when none is supplied.</summary>
    public string EffectiveAriaLabel =>
        !string.IsNullOrWhiteSpace(AriaLabel)
            ? AriaLabel!
            : string.Join(" ", new[] { Title, Value, Unit }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
