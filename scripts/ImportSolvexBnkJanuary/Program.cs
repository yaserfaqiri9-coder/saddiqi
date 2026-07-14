using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

var options = ImportOptions.Parse(args);
var rows = SolvexBnkJanuaryWorkbook.Read(options.ExcelPath, options.SheetName);
if (options.InferMissingPricing)
{
    rows = rows.InferMissingPricingFromSheet();
}

var totalQuantity = rows.Sum(r => r.LoadedQuantityMt);
var totalUsd = rows.Sum(r => r.TotalUsd ?? 0m);
var rowsWithReceiptData = rows.Count(r => r.LeakDate.HasValue || r.ActualQuantityMt.HasValue);

Console.WriteLine($"Workbook: {Path.GetFullPath(options.ExcelPath)}");
Console.WriteLine($"Sheet: {options.SheetName}");
Console.WriteLine($"Rows: {rows.Count:N0}");
Console.WriteLine($"Loaded MT: {totalQuantity:N3}");
Console.WriteLine($"Total USD from workbook: {totalUsd:N2}");
Console.WriteLine($"Rows with leak/quantity columns: {rowsWithReceiptData:N0}");
Console.WriteLine($"Infer missing pricing: {(options.InferMissingPricing ? "yes" : "no")}");

if (options.DryRun)
{
    Console.WriteLine("Dry run only. Pass --apply with a database connection string to insert.");
    return;
}

var rawConnectionString = options.ConnectionString
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

if (string.IsNullOrWhiteSpace(rawConnectionString))
{
    throw new InvalidOperationException(
        "Database connection is not configured. Set DATABASE_URL or ConnectionStrings__DefaultConnection, or pass --connection \"...\".");
}

var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(BuildPostgresConnectionString(rawConnectionString))
    .Options;

await using var db = new ApplicationDbContext(dbOptions);
var contract = await db.Contracts
    .AsNoTracking()
    .FirstOrDefaultAsync(c => c.Id == options.ContractId);

if (contract is null)
{
    throw new InvalidOperationException($"Contract Id {options.ContractId} was not found.");
}

if (contract.ContractType != ContractType.Purchase && !options.AllowNonPurchaseContract)
{
    throw new InvalidOperationException(
        $"Contract Id {options.ContractId} is {contract.ContractType}. LoadingRegisters are system-owned by purchase contracts. "
        + "Use a purchase contract or pass --allow-non-purchase only if you intentionally want to bypass that rule.");
}

var existingRows = await db.LoadingRegisters
    .Where(l => l.ContractId == options.ContractId)
    .Select(l => new
    {
        l.Id,
        Key = new LoadingKey(l.LoadingDate.Date, l.RwbNo ?? l.BillOfLadingNumber, l.WagonNumber, l.LoadedQuantityMt)
    })
    .ToListAsync();
var existingByKey = existingRows
    .GroupBy(r => r.Key)
    .Where(g => g.Count() == 1)
    .ToDictionary(g => g.Key, g => g.Single().Id);
var existingKeySet = existingRows.Select(r => r.Key).ToHashSet();

var toInsert = rows
    .Where(r => !existingKeySet.Contains(r.Key))
    .ToList();
var skipped = rows.Count - toInsert.Count;

Console.WriteLine($"Contract: {contract.ContractNumber} (Id {contract.Id}, {contract.ContractType})");
Console.WriteLine($"Existing matching rows skipped: {skipped:N0}");
Console.WriteLine($"Rows to insert: {toInsert.Count:N0}");
Console.WriteLine($"Refresh existing rows: {(options.RefreshExisting ? "yes" : "no")}");

if (toInsert.Count == 0 && !options.RefreshExisting)
{
    Console.WriteLine("No new loading rows to insert.");
    return;
}

await using var tx = await db.Database.BeginTransactionAsync();

foreach (var row in toInsert)
{
    db.LoadingRegisters.Add(new LoadingRegister
    {
        ContractId = contract.Id,
        ProductId = contract.ProductId,
        TransportType = LoadingTransportType.Wagon,
        LoadingDate = row.LoadingDate,
        LoadedQuantityMt = row.LoadedQuantityMt,
        BillOfLadingNumber = row.RwbNo,
        RwbNo = row.RwbNo,
        WagonNumber = row.WagonNumber,
        ConsigneeName = row.ConsigneeName,
        DestinationName = row.DestinationName,
        PlattsUsd = row.PlattsUsd,
        LoadingPriceUsd = row.EffectiveLoadingPriceUsd,
        Notes = row.BuildNotes(options.ExcelPath, options.SheetName)
    });
}

var refreshed = 0;
if (options.RefreshExisting)
{
    foreach (var row in rows)
    {
        if (!existingByKey.TryGetValue(row.Key, out var loadingId))
        {
            continue;
        }

        var loading = await db.LoadingRegisters.FirstAsync(l => l.Id == loadingId);
        loading.BillOfLadingNumber = row.RwbNo;
        loading.RwbNo = row.RwbNo;
        loading.WagonNumber = row.WagonNumber;
        loading.ConsigneeName = row.ConsigneeName;
        loading.DestinationName = row.DestinationName;
        loading.PlattsUsd = row.PlattsUsd;
        loading.LoadingPriceUsd = row.EffectiveLoadingPriceUsd;
        loading.Notes = row.BuildNotes(options.ExcelPath, options.SheetName);
        refreshed++;
    }
}

await db.SaveChangesAsync();
await tx.CommitAsync();

Console.WriteLine($"Inserted {toInsert.Count:N0} loading rows into contract Id {contract.Id}.");
Console.WriteLine($"Refreshed {refreshed:N0} existing loading rows.");

static string BuildPostgresConnectionString(string raw)
{
    if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 5432;
        return $"Host={uri.Host};Port={port};Username={username};Password={password};Database={database};SSL Mode=Prefer;Trust Server Certificate=true";
    }

    return raw;
}

internal sealed record ImportOptions(
    string ExcelPath,
    string SheetName,
    int ContractId,
    bool DryRun,
    bool RefreshExisting,
    bool InferMissingPricing,
    bool AllowNonPurchaseContract,
    string? ConnectionString)
{
    public static ImportOptions Parse(string[] args)
    {
        var excelPath = @"ACCOUNT-LOADING SOLVEX-BNK 2026.xlsx";
        var sheetName = "9600 MT January";
        var contractId = 2;
        var dryRun = true;
        var refreshExisting = false;
        var inferMissingPricing = false;
        var allowNonPurchaseContract = false;
        string? connectionString = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string Next()
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException($"{arg} requires a value.");
                }

                return args[++i];
            }

            switch (arg)
            {
                case "--excel":
                    excelPath = Next();
                    break;
                case "--sheet":
                    sheetName = Next();
                    break;
                case "--contract-id":
                    contractId = int.Parse(Next(), CultureInfo.InvariantCulture);
                    break;
                case "--connection":
                    connectionString = Next();
                    break;
                case "--apply":
                    dryRun = false;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--refresh-existing":
                    refreshExisting = true;
                    break;
                case "--infer-missing-pricing":
                    inferMissingPricing = true;
                    break;
                case "--allow-non-purchase":
                    allowNonPurchaseContract = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        return new ImportOptions(excelPath, sheetName, contractId, dryRun, refreshExisting, inferMissingPricing, allowNonPurchaseContract, connectionString);
    }
}

internal sealed record LoadingKey(DateTime LoadingDate, string? RwbNo, string? WagonNumber, decimal LoadedQuantityMt);

internal sealed record WorkbookLoadingRow(
    int ExcelRowNumber,
    int SourceNo,
    DateTime LoadingDate,
    string RwbNo,
    string WagonNumber,
    decimal LoadedQuantityMt,
    decimal? PlattsUsd,
    decimal? NetLoadingPriceUsd,
    decimal? TotalUsd,
    decimal? PriceRub,
    decimal? TotalRub,
    string? ConsigneeName,
    string? DestinationName,
    DateTime? ArrivalDate,
    DateTime? LeakDate,
    decimal? ActualQuantityMt,
    decimal? DifferenceMt,
    string? Remarks)
{
    public LoadingKey Key => new(LoadingDate.Date, RwbNo, WagonNumber, LoadedQuantityMt);
    public decimal? EffectiveLoadingPriceUsd
        => TotalUsd.HasValue && LoadedQuantityMt > 0m
            ? decimal.Round(TotalUsd.Value / LoadedQuantityMt, 4, MidpointRounding.AwayFromZero)
            : NetLoadingPriceUsd;

    public string BuildNotes(string excelPath, string sheetName)
    {
        var parts = new List<string>
        {
            $"Source={Path.GetFileName(excelPath)}",
            $"Sheet={sheetName}",
            $"ExcelRow={ExcelRowNumber}",
            $"No={SourceNo}"
        };

        AddDecimal("TotalUsd", TotalUsd);
        AddDecimal("DiscountColumnUsd", NetLoadingPriceUsd);
        AddDecimal("PriceRub", PriceRub);
        AddDecimal("TotalRub", TotalRub);
        AddText("ArrivalDate", ArrivalDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddText("LeakDate", LeakDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddDecimal("ActualQuantityMt", ActualQuantityMt);
        AddDecimal("DifferenceMt", DifferenceMt);
        AddText("Remarks", Remarks);

        return string.Join("; ", parts);

        void AddDecimal(string key, decimal? value)
        {
            if (value.HasValue)
            {
                parts.Add($"{key}={value.Value.ToString("0.####", CultureInfo.InvariantCulture)}");
            }
        }

        void AddText(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{key}={value.Trim()}");
            }
        }
    }
}

internal static class SolvexBnkJanuaryWorkbook
{
    public static IReadOnlyList<WorkbookLoadingRow> Read(string path, string sheetName)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Excel workbook was not found.", path);
        }

        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var copy = new MemoryStream();
        source.CopyTo(copy);
        copy.Position = 0;
        using var document = SpreadsheetDocument.Open(copy, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidDataException("Workbook part is missing.");
        var sheet = workbookPart.Workbook.Sheets?.Elements<Sheet>()
            .FirstOrDefault(s => string.Equals(s.Name?.Value, sheetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"Sheet '{sheetName}' was not found.");
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        var rows = worksheetPart.Worksheet.GetFirstChild<SheetData>()?.Elements<Row>() ?? Enumerable.Empty<Row>();

        var result = new List<WorkbookLoadingRow>();
        foreach (var row in rows.Where(r => (r.RowIndex?.Value ?? 0) > 4))
        {
            var cells = row.Elements<Cell>().ToDictionary(c => ColumnName(c.CellReference?.Value ?? ""), StringComparer.OrdinalIgnoreCase);
            var sourceNo = ReadInt(cells, "A", workbookPart);
            if (!sourceNo.HasValue)
            {
                continue;
            }

            var loadedQuantityMt = ReadDecimal(cells, "E", workbookPart);
            if (!loadedQuantityMt.HasValue || loadedQuantityMt.Value <= 0m)
            {
                continue;
            }

            var loadingDate = ReadDate(cells, "B", workbookPart)
                ?? throw new InvalidDataException($"Missing loading date at Excel row {row.RowIndex}.");
            var rwbNo = ReadRequiredText(cells, "C", workbookPart, row.RowIndex?.Value ?? 0, "RWB No");
            if (rwbNo.Length < 8 && rwbNo.All(char.IsDigit))
            {
                rwbNo = rwbNo.PadLeft(8, '0');
            }

            result.Add(new WorkbookLoadingRow(
                ExcelRowNumber: checked((int)(row.RowIndex?.Value ?? 0)),
                SourceNo: sourceNo.Value,
                LoadingDate: loadingDate.Date,
                RwbNo: rwbNo,
                WagonNumber: ReadRequiredText(cells, "D", workbookPart, row.RowIndex?.Value ?? 0, "Wagon No"),
                LoadedQuantityMt: decimal.Round(loadedQuantityMt.Value, 4, MidpointRounding.AwayFromZero),
                PlattsUsd: Round4(ReadDecimal(cells, "F", workbookPart)),
                NetLoadingPriceUsd: Round4(ReadDecimal(cells, "G", workbookPart)),
                TotalUsd: Round4(ReadDecimal(cells, "H", workbookPart)),
                PriceRub: Round4(ReadDecimal(cells, "I", workbookPart)),
                TotalRub: Round4(ReadDecimal(cells, "J", workbookPart)),
                ConsigneeName: ReadText(cells, "K", workbookPart),
                DestinationName: ReadText(cells, "L", workbookPart),
                ArrivalDate: ReadDate(cells, "N", workbookPart),
                LeakDate: ReadDate(cells, "O", workbookPart),
                ActualQuantityMt: Round4(ReadDecimal(cells, "P", workbookPart)),
                DifferenceMt: Round4(ReadDecimal(cells, "Q", workbookPart)),
                Remarks: ReadText(cells, "R", workbookPart)));
        }

        return result;
    }

    private static decimal? Round4(decimal? value)
        => value.HasValue ? decimal.Round(value.Value, 4, MidpointRounding.AwayFromZero) : null;

    private static string ReadRequiredText(IReadOnlyDictionary<string, Cell> cells, string column, WorkbookPart workbookPart, uint rowNumber, string name)
        => ReadText(cells, column, workbookPart)
           ?? throw new InvalidDataException($"Missing {name} at Excel row {rowNumber}.");

    private static int? ReadInt(IReadOnlyDictionary<string, Cell> cells, string column, WorkbookPart workbookPart)
        => ReadDecimal(cells, column, workbookPart) is { } value ? (int)value : null;

    private static decimal? ReadDecimal(IReadOnlyDictionary<string, Cell> cells, string column, WorkbookPart workbookPart)
    {
        var text = ReadText(cells, column, workbookPart);
        if (string.IsNullOrWhiteSpace(text) || text == "#REF!")
        {
            return null;
        }

        text = text.Replace("$", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal).Trim();
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static DateTime? ReadDate(IReadOnlyDictionary<string, Cell> cells, string column, WorkbookPart workbookPart)
    {
        var text = ReadText(cells, column, workbookPart);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial))
        {
            return DateTime.FromOADate(serial);
        }

        string[] formats = ["M/d/yyyy", "M/d/yy", "MM-dd-yy", "dd.MM.yyyy", "yyyy-MM-dd"];
        return DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            || DateTime.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
            ? parsed
            : null;
    }

    private static string? ReadText(IReadOnlyDictionary<string, Cell> cells, string column, WorkbookPart workbookPart)
    {
        if (!cells.TryGetValue(column, out var cell))
        {
            return null;
        }

        var raw = cell.CellValue?.InnerText;
        if (raw is null)
        {
            return null;
        }

        if (cell.DataType?.Value == CellValues.SharedString
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedStringIndex))
        {
            return workbookPart.SharedStringTablePart?.SharedStringTable
                .Elements<SharedStringItem>()
                .ElementAtOrDefault(sharedStringIndex)
                ?.InnerText
                ?.Trim();
        }

        return raw.Trim();
    }

    private static string ColumnName(string cellReference)
        => new(cellReference.TakeWhile(char.IsLetter).ToArray());
}

internal static class WorkbookLoadingRowExtensions
{
    public static IReadOnlyList<WorkbookLoadingRow> InferMissingPricingFromSheet(this IReadOnlyList<WorkbookLoadingRow> rows)
    {
        var fallbackPlatts = rows.FirstOrDefault(r => r.PlattsUsd.HasValue)?.PlattsUsd;
        var fallbackNetPrice = rows.FirstOrDefault(r => r.NetLoadingPriceUsd.HasValue)?.NetLoadingPriceUsd;

        return rows.Select(row =>
        {
            var platts = row.PlattsUsd ?? fallbackPlatts;
            var netPrice = row.NetLoadingPriceUsd ?? fallbackNetPrice;
            var totalUsd = row.TotalUsd;
            if (!totalUsd.HasValue && netPrice.HasValue && row.LoadedQuantityMt > 0m)
            {
                totalUsd = decimal.Round(row.LoadedQuantityMt * netPrice.Value, 4, MidpointRounding.AwayFromZero);
            }

            return row with
            {
                PlattsUsd = platts,
                NetLoadingPriceUsd = netPrice,
                TotalUsd = totalUsd
            };
        }).ToList();
    }
}
