namespace PTGOilSystem.Web.Models.MasterData;

public sealed class TransportResourceProfileViewModel
{
    public int? SelectedId { get; init; }
    public string ActiveTab { get; init; } = "info";
    public IReadOnlyList<TransportResourceTripItem> Trips { get; init; } = [];
    public IReadOnlyList<TransportResourceDocumentItem> Documents { get; init; } = [];

    public static TransportResourceProfileViewModel Empty(string? activeTab = null)
        => new() { ActiveTab = NormalizeTab(activeTab) };

    public static string NormalizeTab(string? tab)
        => tab?.Trim().ToLowerInvariant() switch
        {
            "trips" => "trips",
            "docs" => "docs",
            _ => "info"
        };
}

public sealed class TransportResourceTripItem
{
    public DateTime? Date { get; init; }
    public string Title { get; init; } = "";
    public string Status { get; init; } = "";
    public string Quantity { get; init; } = "";
    public string Route { get; init; } = "";
    public string Reference { get; init; } = "";
    public string? Controller { get; init; }
    public string? Action { get; init; }
    public int? RouteId { get; init; }
}

public sealed class TransportResourceDocumentItem
{
    public DateTime? Date { get; init; }
    public string Type { get; init; } = "";
    public string Number { get; init; } = "";
    public string Source { get; init; } = "";
    public string? Controller { get; init; }
    public string? Action { get; init; }
    public int? RouteId { get; init; }
}

