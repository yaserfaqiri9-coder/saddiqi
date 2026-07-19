using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PTGOilSystem.Web.Data;
using Xunit;

namespace PTGOilSystem.Web.Tests;

[CollectionDefinition("Accounting PostgreSQL", DisableParallelization = true)]
public sealed class AccountingPostgreSqlCollection : ICollectionFixture<AccountingPostgreSqlFixture>
{
    public const string CollectionName = "Accounting PostgreSQL";
}

public sealed class AccountingPostgreSqlFixture : IAsyncLifetime
{
    public const string UnusedLegacyAccountCompanyCode = "STAGE21-UNUSED";
    public const string UsedLegacyAccountCompanyCode = "STAGE21-USED";

    /// <summary>
    /// آخرین migration پیش از <c>SplitEmployeeAndAccruedPayableAccounts</c>. داده legacy باید
    /// دقیقاً روی همین schema ساخته شود تا migration اصلاحی قابل آزمایش باشد.
    /// </summary>
    private const string LegacyAccountingBaselineMigration = "20260715162639_AddAccountingCoreAndFiscalCalendar";

    private readonly string _databaseName = $"{DatabaseSafetyGuard.AccountingTestDatabasePrefix}{Guid.NewGuid():N}";
    private readonly string _adminConnectionString =
        Environment.GetEnvironmentVariable("PTG_TEST_POSTGRES_ADMIN")
        ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres;Timeout=10;Command Timeout=60";

    private bool _databaseCreated;

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        DatabaseSafetyGuard.EnsureIntegrationTestCreateAllowed(_databaseName);

        await using (var admin = new NpgsqlConnection(_adminConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
            _databaseCreated = true;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
            {
                Database = _databaseName
            };
            ConnectionString = builder.ConnectionString;
            DatabaseSafetyGuard.EnsureIntegrationTestUseAllowed(builder.Database);

            await using var db = CreateDbContext();
            var migrator = db.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(LegacyAccountingBaselineMigration);
            await SeedLegacySharedPayableAsync(UnusedLegacyAccountCompanyCode, hasJournalUsage: false);
            await SeedLegacySharedPayableAsync(UsedLegacyAccountCompanyCode, hasJournalUsage: true);
            await migrator.MigrateAsync();
        }
        catch
        {
            // xUnit does not call DisposeAsync when InitializeAsync throws, so the
            // test database would otherwise leak.
            await DropDatabaseAsync();
            throw;
        }
    }

    public Task DisposeAsync() => DropDatabaseAsync();

    private async Task DropDatabaseAsync()
    {
        if (!_databaseCreated)
            return;

        DatabaseSafetyGuard.EnsureIntegrationTestDropAllowed(_databaseName);
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)",
            admin);
        await drop.ExecuteNonQueryAsync();
        _databaseCreated = false;
    }

    public ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    /// <summary>
    /// داده legacy را با SQL خام و سازگار با schema همان migration پایه می‌سازد. مدل فعلی EF
    /// ستون‌هایی (مثل <c>Accounts.MonetaryTreatment</c>) دارد که migrationهای بعدی اضافه می‌کنند،
    /// بنابراین Seed با موجودیت‌های EF روی این schema شکست می‌خورد.
    /// </summary>
    private async Task SeedLegacySharedPayableAsync(string companyCode, bool hasJournalUsage)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        var companyId = await InsertReturningIdAsync(conn, """
            INSERT INTO "Companies" ("Code", "Name", "Country", "IsActive", "CreatedAtUtc")
            VALUES (@code, @code, 'AF', TRUE, NOW())
            RETURNING "Id";
            """, ("code", companyCode));

        var sharedPayableId = await InsertReturningIdAsync(conn, """
            INSERT INTO "Accounts"
                ("CompanyId", "Code", "Name", "AccountType", "NormalBalance",
                 "IsControlAccount", "AllowManualPosting", "IsActive", "CreatedAtUtc")
            VALUES (@companyId, '2500', 'Employee/Accrued Payable', 2, 2,
                    TRUE, FALSE, TRUE, NOW())
            RETURNING "Id";
            """, ("companyId", companyId));

        await ExecuteAsync(conn, """
            INSERT INTO "AccountingSettings"
                ("CompanyId", "FunctionalCurrencyCode", "CashBankControlAccountId",
                 "AccountsReceivableAccountId", "AccountsPayableAccountId", "InventoryAccountId",
                 "InventoryInTransitAccountId", "SupplierPrepaymentAccountId", "CustomerAdvanceAccountId",
                 "FreightPayableAccountId", "CommissionPayableAccountId", "EmployeeAdvanceAccountId",
                 "EmployeePayableAccountId", "AccruedExpenseAccountId", "SalesRevenueAccountId",
                 "CostOfGoodsSoldAccountId", "GeneralExpenseAccountId", "ExchangeGainAccountId",
                 "ExchangeLossAccountId", "InventoryLossAccountId", "CurrentYearProfitLossAccountId",
                 "RetainedEarningsAccountId", "CreatedAtUtc")
            VALUES (@companyId, 'USD', @account,
                    @account, @account, @account,
                    @account, @account, @account,
                    @account, @account, @account,
                    @account, @account, @account,
                    @account, @account, @account,
                    @account, @account, @account,
                    @account, NOW());
            """, ("companyId", companyId), ("account", sharedPayableId));

        if (!hasJournalUsage)
            return;

        var fiscalYearId = await InsertReturningIdAsync(conn, """
            INSERT INTO "FiscalYears"
                ("CompanyId", "Name", "StartDate", "EndDate", "Status", "IsCurrent", "CreatedAtUtc")
            VALUES (@companyId, 'FY-2026', DATE '2026-01-01', DATE '2026-12-31', 1, TRUE, NOW())
            RETURNING "Id";
            """, ("companyId", companyId));

        var fiscalPeriodId = await InsertReturningIdAsync(conn, """
            INSERT INTO "FiscalPeriods"
                ("CompanyId", "FiscalYearId", "PeriodNumber", "Name",
                 "StartDate", "EndDate", "Status", "CreatedAtUtc")
            VALUES (@companyId, @fiscalYearId, 1, 'January',
                    DATE '2026-01-01', DATE '2026-01-31', 1, NOW())
            RETURNING "Id";
            """, ("companyId", companyId), ("fiscalYearId", fiscalYearId));

        var journalEntryId = await InsertReturningIdAsync(conn, """
            INSERT INTO "JournalEntries"
                ("CompanyId", "FiscalYearId", "FiscalPeriodId", "JournalNumber", "Status",
                 "AccountingDate", "DocumentDate", "OperationDate", "SourceModule",
                 "IsOpening", "IsClosing", "IsAdjustment", "IsReversal", "CreatedAt")
            VALUES (@companyId, @fiscalYearId, @fiscalPeriodId, 'LEGACY-DRAFT', 0,
                    DATE '2026-01-15', DATE '2026-01-15', DATE '2026-01-15', 'MigrationTest',
                    FALSE, FALSE, FALSE, FALSE, NOW())
            RETURNING "Id";
            """, ("companyId", companyId), ("fiscalYearId", fiscalYearId), ("fiscalPeriodId", fiscalPeriodId));

        await ExecuteAsync(conn, """
            INSERT INTO "JournalEntryLines"
                ("JournalEntryId", "LineNumber", "AccountId", "Debit", "Credit",
                 "TransactionCurrencyCode", "TransactionAmount", "ExchangeRate", "CreatedAtUtc")
            VALUES (@journalEntryId, 1, @account, 1, 0, 'USD', 1, 1, NOW());
            """, ("journalEntryId", journalEntryId), ("account", sharedPayableId));
    }

    private static async Task<int> InsertReturningIdAsync(
        NpgsqlConnection conn,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(conn, sql, parameters);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection conn,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(conn, sql, parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static NpgsqlCommand CreateCommand(
        NpgsqlConnection conn,
        string sql,
        (string Name, object Value)[] parameters)
    {
        var command = new NpgsqlCommand(sql, conn);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return command;
    }
}
