using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PTGOilSystem.Web.Helpers;

/// <summary>
/// One row of the «تخلیه و تسویه موترها» import sheet. The user fills only the
/// vehicle/wagon number, the discharge weight and (optionally) the allowance
/// (حواکت). These are matched against the real remaining legs/dispatches on the
/// settlement list — no new records are created by parsing.
/// </summary>
public sealed record TruckSettlementImportRow(
    string VehicleNumber,
    decimal DischargeWeightMt,
    decimal? AllowanceMt);

/// <summary>
/// Tolerant parser for the truck-settlement import sheet. Columns are located by
/// header aliases so the exact order/labels do not matter. Expected columns:
/// نمبر موتر/واگن | وزن تخلیه | حواکت. A row must carry a vehicle number and a
/// positive discharge weight to be returned.
/// </summary>
public static class TruckSettlementWorkbookParser
{
    private static readonly string[] NumberAliases =
        ["نمبرموتر", "نمبرموتور", "نمبرواگن", "نمبرواگون", "نمبرپلیت", "پلیت", "نمبر", "موتر", "واگن", "وسیله",
         "platenumber", "plate", "trucknumber", "truckno", "truck", "wagonnumber", "wagonno", "wagon", "number", "vehicle"];
    private static readonly string[] DischargeAliases =
        ["وزنتخلیه", "تخلیه", "وزنتحویل", "تحویل", "وزنرسیده", "dischargeweight", "discharge", "received", "receivedweight",
         "وزن", "مقدار", "مقدارmt", "quantity", "quantitymt", "weight", "netweight", "وزنخالص", "mt", "تن"];
    private static readonly string[] AllowanceAliases =
        ["حواکت", "حواله", "مجاز", "وزنمجاز", "تلورانس", "allowance", "tolerance"];

    public static IReadOnlyList<TruckSettlementImportRow> Parse(Stream stream)
    {
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("ساختار فایل اکسل معتبر نیست.");
        var sheet = workbookPart.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault()
            ?? throw new InvalidDataException("در فایل اکسل هیچ شیتی پیدا نشد.");
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
        if (sheetData is null)
        {
            return [];
        }

        var rows = sheetData.Elements<Row>().ToList();
        var headerRow = FindHeaderRow(rows, workbookPart);

        var numberCol = MatchColumn(headerRow, workbookPart, NumberAliases) ?? "A";
        var dischargeCol = MatchColumn(headerRow, workbookPart, DischargeAliases) ?? "B";
        var allowanceCol = MatchColumn(headerRow, workbookPart, AllowanceAliases);

        var headerIndex = headerRow?.RowIndex?.Value ?? 0;
        var result = new List<TruckSettlementImportRow>();

        foreach (var row in rows.Where(r => (r.RowIndex?.Value ?? 0) > headerIndex))
        {
            var cells = ToCellMap(row);
            var number = ReadText(cells, numberCol, workbookPart);
            var discharge = ReadDecimal(cells, dischargeCol, workbookPart) ?? 0m;

            if (string.IsNullOrWhiteSpace(number) || discharge <= 0m)
            {
                continue;
            }

            var allowance = allowanceCol is null ? null : ReadDecimal(cells, allowanceCol, workbookPart);

            result.Add(new TruckSettlementImportRow(
                number.Trim(),
                discharge,
                allowance is > 0m ? allowance : null));
        }

        return result;
    }

    private static Row? FindHeaderRow(IReadOnlyList<Row> rows, WorkbookPart workbookPart)
    {
        foreach (var row in rows)
        {
            var values = row.Elements<Cell>()
                .Select(cell => Normalize(ReadCellText(cell, workbookPart)))
                .Where(value => value.Length > 0)
                .ToList();
            if (values.Count == 0)
            {
                continue;
            }

            if (NumberAliases.Any(a => values.Contains(a)) && DischargeAliases.Any(a => values.Contains(a)))
            {
                return row;
            }
        }

        return null;
    }

    private static string? MatchColumn(Row? headerRow, WorkbookPart workbookPart, string[] aliases)
    {
        if (headerRow is null)
        {
            return null;
        }

        foreach (var cell in headerRow.Elements<Cell>())
        {
            if (cell.CellReference is null)
            {
                continue;
            }

            var normalized = Normalize(ReadCellText(cell, workbookPart));
            if (normalized.Length > 0 && aliases.Contains(normalized))
            {
                return GetColumnName(cell.CellReference.Value ?? string.Empty);
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, Cell> ToCellMap(Row row)
        => row.Elements<Cell>()
            .Where(c => c.CellReference is not null)
            .ToDictionary(
                c => GetColumnName(c.CellReference!.Value ?? string.Empty),
                c => c,
                StringComparer.OrdinalIgnoreCase);

    private static string? ReadText(IReadOnlyDictionary<string, Cell> cells, string? column, WorkbookPart workbookPart)
    {
        if (column is null || !cells.TryGetValue(column, out var cell))
        {
            return null;
        }

        var value = ReadCellText(cell, workbookPart);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().Trim('"');
    }

    private static decimal? ReadDecimal(IReadOnlyDictionary<string, Cell> cells, string? column, WorkbookPart workbookPart)
    {
        if (column is null || !cells.TryGetValue(column, out var cell))
        {
            return null;
        }

        var text = ReadCellText(cell, workbookPart);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalized = text
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace("€", string.Empty, StringComparison.Ordinal)
            .Trim();

        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string ReadCellText(Cell cell, WorkbookPart workbookPart)
    {
        if (cell.DataType?.Value == CellValues.SharedString)
        {
            var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
            if (sharedStrings is null)
            {
                return string.Empty;
            }

            return int.TryParse(cell.CellValue?.Text, out var index)
                ? sharedStrings.ElementAt(index).InnerText
                : string.Empty;
        }

        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InlineString?.InnerText ?? string.Empty;
        }

        return cell.CellValue?.Text ?? cell.InnerText ?? string.Empty;
    }

    private static string GetColumnName(string cellReference)
        => new(cellReference.TakeWhile(char.IsLetter).ToArray());

    private static string Normalize(string? value)
        => new((value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
}
