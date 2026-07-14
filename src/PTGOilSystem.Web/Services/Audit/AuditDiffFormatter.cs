using System.Globalization;

namespace PTGOilSystem.Web.Services.Audit;

public static class AuditDiffFormatter
{
    public static string ForCreate(params (string Name, object? Value)[] fields)
        => "Create: " + string.Join(" | ", fields.Select(f => $"{f.Name}={FormatValue(f.Value)}"));

    public static string ForDelete(params (string Name, object? Value)[] fields)
        => "Delete: " + string.Join(" | ", fields.Select(f => $"{f.Name}={FormatValue(f.Value)}"));

    public static string ForUpdate(params (string Name, object? Before, object? After)[] fields)
    {
        var changes = fields
            .Where(f => !AreEqual(f.Before, f.After))
            .Select(f => $"{f.Name}: {FormatValue(f.Before)} -> {FormatValue(f.After)}")
            .ToList();

        return changes.Count == 0
            ? "Update: no field-level changes"
            : "Update: " + string.Join(" | ", changes);
    }

    private static bool AreEqual(object? left, object? right)
    {
        if (left is DateTime ldt && right is DateTime rdt)
            return ldt == rdt;

        return Equals(left, right);
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "(null)",
        string s when string.IsNullOrWhiteSpace(s) => "(empty)",
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
        decimal d => d.ToString("N4", CultureInfo.InvariantCulture),
        Enum e => e.ToString(),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "(null)"
    };
}
