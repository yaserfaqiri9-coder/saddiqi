using System.Text;
using Npgsql;

var rawConnectionString =
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=ptg_oil_system;SSL Mode=Prefer;Trust Server Certificate=true";

var connectionString = BuildPostgresConnectionString(rawConnectionString);
var dryRun = IsTruthy(Environment.GetEnvironmentVariable("DRY_RUN"));

var preserve = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "Users",
    "Roles",
    "__EFMigrationsHistory",
};

Console.WriteLine("Using connection: " + MaskPassword(connectionString));
Console.WriteLine(dryRun ? "Mode: DRY_RUN" : "Mode: BACKUP_AND_TRUNCATE");

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

var allTables = await GetPublicTablesAsync(connection);
var preservedTables = allTables
    .Where(table => preserve.Contains(table))
    .OrderBy(table => table, StringComparer.OrdinalIgnoreCase)
    .ToList();
var tablesToTruncate = allTables
    .Where(table => !preserve.Contains(table))
    .OrderBy(table => table, StringComparer.OrdinalIgnoreCase)
    .ToList();

Console.WriteLine();
Console.WriteLine($"PRESERVE ({preservedTables.Count}): {string.Join(", ", preservedTables)}");
Console.WriteLine($"TRUNCATE ({tablesToTruncate.Count}): {string.Join(", ", tablesToTruncate)}");

var preservedRows = await CountRowsAsync(connection, preservedTables);
Console.WriteLine("PRESERVED ROWS: " + string.Join(", ", preservedRows.Select(item => $"{item.Key}={item.Value}")));

if (tablesToTruncate.Count == 0)
{
    Console.WriteLine("Nothing to truncate.");
    return 0;
}

if (dryRun)
{
    return 0;
}

var backupDirectory = Path.Combine("artifacts", $"db-clear-except-users-{DateTime.UtcNow:yyyyMMddHHmmss}");
Directory.CreateDirectory(backupDirectory);

Console.WriteLine();
Console.WriteLine($"Backup directory: {backupDirectory}");
await WriteManifestAsync(backupDirectory, preservedTables, tablesToTruncate);
await BackupTablesAsync(connection, backupDirectory, tablesToTruncate);

Console.WriteLine();
Console.WriteLine("Truncating non-user tables...");
await using (var transaction = await connection.BeginTransactionAsync())
{
    var tableList = string.Join(", ", tablesToTruncate.Select(table => $"public.{QuoteIdentifier(table)}"));
    var sql = $"TRUNCATE TABLE {tableList} RESTART IDENTITY";
    await using var command = new NpgsqlCommand(sql, connection, transaction);
    await command.ExecuteNonQueryAsync();
    await transaction.CommitAsync();
}

var remainingRows = await CountRowsAsync(connection, tablesToTruncate);
var nonEmpty = remainingRows.Where(item => item.Value != 0).ToList();

Console.WriteLine("Truncate finished.");
Console.WriteLine(nonEmpty.Count == 0
    ? "Verification: all truncated tables are empty."
    : "Verification warning: non-empty tables: " + string.Join(", ", nonEmpty.Select(item => $"{item.Key}={item.Value}")));

return nonEmpty.Count == 0 ? 0 : 2;

static async Task<List<string>> GetPublicTablesAsync(NpgsqlConnection connection)
{
    const string sql = """
        SELECT tablename
        FROM pg_tables
        WHERE schemaname = 'public'
          AND tablename NOT LIKE 'pg_%'
        ORDER BY lower(tablename)
        """;

    var tables = new List<string>();
    await using var command = new NpgsqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        tables.Add(reader.GetString(0));
    }

    return tables;
}

static async Task BackupTablesAsync(NpgsqlConnection connection, string backupDirectory, IReadOnlyCollection<string> tables)
{
    foreach (var table in tables)
    {
        var filePath = Path.Combine(backupDirectory, table + ".csv");
        Console.WriteLine($"Backing up {table} -> {filePath}");
        await using var command = new NpgsqlCommand($"SELECT * FROM public.{QuoteIdentifier(table)}", connection);
        await using var reader = await command.ExecuteReaderAsync();
        await using var writer = new StreamWriter(filePath, false, new UTF8Encoding(false));

        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        await writer.WriteLineAsync(string.Join(",", columns.Select(EscapeCsv)));

        while (await reader.ReadAsync())
        {
            var values = new string[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
            {
                values[index] = reader.IsDBNull(index)
                    ? string.Empty
                    : EscapeCsv(Convert.ToString(reader.GetValue(index)) ?? string.Empty);
            }

            await writer.WriteLineAsync(string.Join(",", values));
        }
    }
}

static async Task WriteManifestAsync(string backupDirectory, IReadOnlyList<string> preservedTables, IReadOnlyList<string> truncatedTables)
{
    var filePath = Path.Combine(backupDirectory, "_manifest.txt");
    var lines = new List<string>
    {
        "CreatedUtc=" + DateTime.UtcNow.ToString("O"),
        "Preserved=" + string.Join(", ", preservedTables),
        "Truncated=" + string.Join(", ", truncatedTables),
    };

    await File.WriteAllLinesAsync(filePath, lines, new UTF8Encoding(false));
}

static async Task<Dictionary<string, long>> CountRowsAsync(NpgsqlConnection connection, IReadOnlyCollection<string> tables)
{
    var counts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    foreach (var table in tables)
    {
        await using var command = new NpgsqlCommand($"SELECT COUNT(*) FROM public.{QuoteIdentifier(table)}", connection);
        counts[table] = (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    return counts;
}

static string QuoteIdentifier(string identifier)
    => "\"" + identifier.Replace("\"", "\"\"") + "\"";

static string EscapeCsv(string value)
{
    if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
    {
        return '"' + value.Replace("\"", "\"\"") + '"';
    }

    return value;
}

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

static string MaskPassword(string connectionString)
{
    var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
    for (var index = 0; index < parts.Length; index++)
    {
        if (parts[index].StartsWith("Password=", StringComparison.OrdinalIgnoreCase) ||
            parts[index].StartsWith("Pwd=", StringComparison.OrdinalIgnoreCase))
        {
            var separatorIndex = parts[index].IndexOf('=');
            if (separatorIndex >= 0)
            {
                parts[index] = parts[index][..(separatorIndex + 1)] + "****";
            }
        }
    }

    return string.Join(";", parts) + ";";
}

static bool IsTruthy(string? value)
    => string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
       string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
       string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
