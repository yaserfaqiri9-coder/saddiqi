namespace PTGOilSystem.Web.Models.StatCards;

/// <summary>Arrow direction shown beside the trend percentage.</summary>
public enum StatCardTrendDirection
{
    None = 0,
    Up = 1,
    Down = 2
}

/// <summary>Colour tone of the trend text (independent from direction).</summary>
public enum StatCardTrendTone
{
    Neutral = 0,
    Positive = 1,
    Negative = 2
}

/// <summary>Overall visual state of the card.</summary>
public enum StatCardState
{
    Default = 0,
    Warning = 1,
    Loading = 2,
    Empty = 3
}
