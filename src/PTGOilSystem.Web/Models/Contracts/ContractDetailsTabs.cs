namespace PTGOilSystem.Web.Models.Contracts;

public static class ContractDetailsTabs
{
    public const string Summary = "summary";
    public const string Loading = "loading";
    public const string LoadingExpenses = "loading-expenses";
    public const string Dispatch = "dispatch";
    public const string Sales = "sales";
    public const string Expenses = "expenses";
    public const string Losses = "losses";
    public const string ShipmentPnl = "shipment-pnl";

    public static string Normalize(string? tab)
        => tab?.Trim().ToLowerInvariant() switch
        {
            Loading => Loading,
            LoadingExpenses => LoadingExpenses,
            Dispatch => Dispatch,
            Sales => Sales,
            Expenses => Expenses,
            Losses => Losses,
            ShipmentPnl => ShipmentPnl,
            _ => Summary
        };
}
