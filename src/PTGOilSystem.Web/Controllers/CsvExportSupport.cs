using System.Text;
using Microsoft.AspNetCore.Mvc;
using PTGOilSystem.Web.Helpers;

namespace PTGOilSystem.Web.Controllers;

internal static class CsvExportSupport
{
    public static FileContentResult File(Controller controller, string filename, string[] headers, IEnumerable<string?[]> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", headers.Select(Escape)));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", row.Select(Escape)));
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(builder.ToString());
        var bytes = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, bytes, preamble.Length, content.Length);
        return controller.File(bytes, "text/csv; charset=utf-8", filename);
    }

    public static string Date(DateTime? value) => DateDisplay.HtmlDateInput(value);
    public static string Decimal(decimal? value) => value?.ToString("0.####") ?? "";

    private static string Escape(string? value)
    {
        value ??= "";
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
