using System.Collections;
using System.Globalization;
using System.Reflection;

namespace PTGOilSystem.Web.Services.Exports;

public sealed record TabularExportAutoColumn(
    string TitleFa,
    string TitleEn,
    TabularExportValueType ValueType,
    double Width,
    params string[] Properties);

public static class TabularExportAuto
{
    public static TabularExportDocument Build(
        object model,
        string fileNameStem,
        string titleFa,
        string titleEn,
        IReadOnlyList<TabularExportAutoColumn> columns,
        IReadOnlyList<TabularExportFilter>? filters = null,
        bool forceLandscape = false)
    {
        var items = ExtractRows(model).Cast<object>().ToList();
        return new TabularExportDocument
        {
            FileNameStem = fileNameStem,
            TitleFa = titleFa,
            TitleEn = titleEn,
            KnownRowCount = items.Count,
            ForceLandscape = forceLandscape,
            Filters = filters ?? [],
            Columns = columns.Select(column => new TabularExportColumn(
                column.TitleFa, column.TitleEn, column.ValueType, column.Width, column.Width >= 24)).ToList(),
            Rows = items.Select(item => new TabularExportRow(columns.Select(column => BuildCell(item, column)).ToList()))
        };
    }

    private static IEnumerable ExtractRows(object model)
    {
        if (model is IEnumerable enumerable and not string)
            return enumerable;

        var type = model.GetType();
        foreach (var propertyName in new[] { "Items", "Rows", "Loadings", "Expenses" })
        {
            if (type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(model) is IEnumerable rows)
                return rows;
        }

        throw new InvalidOperationException($"No exportable row collection was found on {type.Name}.");
    }

    private static TabularExportCell BuildCell(object item, TabularExportAutoColumn column)
    {
        var values = column.Properties
            .Select(property => item.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public)?.GetValue(item))
            .Where(value => value is not null)
            .ToList();
        var value = values.Count switch
        {
            0 => null,
            1 => values[0],
            _ => string.Join(" / ", values.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)))
        };

        return column.ValueType switch
        {
            TabularExportValueType.Integer => TabularExportCell.Integer(value is null ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture)),
            TabularExportValueType.Number => TabularExportCell.Number(value is null ? null : Convert.ToDecimal(value, CultureInfo.InvariantCulture)),
            TabularExportValueType.Percentage => TabularExportCell.Percentage(value is null ? null : Convert.ToDecimal(value, CultureInfo.InvariantCulture)),
            TabularExportValueType.Date => TabularExportCell.Date(value as DateTime? ?? (value is DateTime date ? date : null)),
            TabularExportValueType.DateTime => TabularExportCell.DateTime(value as DateTime? ?? (value is DateTime dateTime ? dateTime : null)),
            TabularExportValueType.Boolean => TabularExportCell.Boolean(value is null ? null : Convert.ToBoolean(value, CultureInfo.InvariantCulture)),
            _ => TabularExportCell.Text(value is Enum ? value.ToString() : Convert.ToString(value, CultureInfo.InvariantCulture))
        };
    }
}
