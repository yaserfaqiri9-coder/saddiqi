namespace PTGOilSystem.Web.Models.StatCards;

/// <summary>
/// Central map from a logical avatar key (e.g. "shipments") to its illustration
/// file under <c>wwwroot/images/stat-cards/</c>. This is the ONLY place that
/// knows illustration filenames, so pages reference concepts, not paths.
///
/// NOTE: the physical asset files are supplied separately (see the Asset Spec).
/// Until a file exists the component keeps the illustration area transparent.
/// </summary>
public static class StatCardAvatarRegistry
{
    /// <summary>Folder (web path) that holds every stat-card illustration.</summary>
    public const string BasePath = "/images/stat-cards/";

    /// <summary>Preferred file extension for the supplied assets.</summary>
    public const string Extension = ".webp";

    /// <summary>
    /// Logical key → base filename (without extension). Grouped to mirror the
    /// reference dashboard sections. Keys are the public contract used in views.
    /// </summary>
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Shipment operations ─────────────────────────────
        ["shipments"]         = "shipments",         // oil tanker vessel
        ["unloaded"]          = "unloaded",          // discharge tanks / facility
        ["transported"]       = "transported",       // fuel tanker truck
        ["loading"]           = "loading",           // crane / loading terminal
        ["shortage"]          = "shortage",          // oil barrel + warning
        ["realized-profit"]   = "realized-profit",   // green growth chart

        // ── Purchase & supply ───────────────────────────────
        ["purchase-contracts"] = "purchase-contracts", // contract doc + pen
        ["purchase-quantity"]  = "purchase-quantity",  // tanker truck / cargo ship
        ["purchase-value"]     = "purchase-value",     // business handshake / finance doc
        ["suppliers"]          = "suppliers",          // team of professionals
        ["purchase-requests"]  = "purchase-requests",  // clipboard

        // ── Sales & revenue ─────────────────────────────────
        ["sales-contracts"] = "sales-contracts", // approved contract
        ["sold-quantity"]   = "sold-quantity",   // transport tanker
        ["sales-value"]     = "sales-value",     // soft banknotes / coins
        ["customers"]       = "customers",       // two managers dealing
        ["average-price"]   = "average-price",   // price tag

        // ── Finance & accounting ────────────────────────────
        ["receivables"]   = "receivables",   // wallet receiving money
        ["payments"]      = "payments",      // bank cards / payment
        ["cash"]          = "cash",          // green wallet
        ["profit-loss"]   = "profit-loss",   // calculator / finance chart (net sales − expenses)
        ["expenses"]      = "expenses",      // expense receipt / outflow (dedicated, never reused)
        ["bank-accounts"] = "bank-accounts", // bank building

        // ── Inventory ───────────────────────────────────────
        ["total-inventory"] = "total-inventory", // oil barrels
        ["tank-inventory"]  = "tank-inventory",  // storage tanks
        ["in-transit"]      = "in-transit",      // tanker vessel / truck
        ["unloading"]       = "unloading",       // discharge terminal
        ["losses"]          = "losses",          // barrel + warning
    };

    /// <summary>All registered avatar keys (used by the Asset Spec / audits).</summary>
    public static IReadOnlyCollection<string> Keys => Map.Keys;

    /// <summary>
    /// Resolve a web path for the given key, or <c>null</c> when the key is
    /// unknown/blank. Returning null keeps the component visual area transparent.
    /// </summary>
    public static string? ResolvePath(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return Map.TryGetValue(key, out var file) ? BasePath + file + Extension : null;
    }
}
