using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
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

    private readonly string _databaseName = $"{DatabaseSafetyGuard.AccountingTestDatabasePrefix}{Guid.NewGuid():N}";
    private readonly string _adminConnectionString =
        Environment.GetEnvironmentVariable("PTG_TEST_POSTGRES_ADMIN")
        ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres;Timeout=10;Command Timeout=60";

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        DatabaseSafetyGuard.EnsureIntegrationTestCreateAllowed(_databaseName);

        await using (var admin = new NpgsqlConnection(_adminConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = _databaseName,
            Pooling = false
        };
        ConnectionString = builder.ConnectionString;
        DatabaseSafetyGuard.EnsureIntegrationTestUseAllowed(builder.Database);

        await using var db = CreateDbContext();
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260715162639_AddAccountingCoreAndFiscalCalendar");
        await SeedLegacySharedPayableAsync(db, UnusedLegacyAccountCompanyCode, hasJournalUsage: false);
        await SeedLegacySharedPayableAsync(db, UsedLegacyAccountCompanyCode, hasJournalUsage: true);
        await migrator.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        DatabaseSafetyGuard.EnsureIntegrationTestDropAllowed(_databaseName);
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)",
            admin);
        await drop.ExecuteNonQueryAsync();
    }

    public ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task SeedLegacySharedPayableAsync(
        ApplicationDbContext db,
        string companyCode,
        bool hasJournalUsage)
    {
        var company = new Company
        {
            Code = companyCode,
            Name = companyCode,
            Country = "AF",
            IsActive = true
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var sharedPayable = new Account
        {
            CompanyId = company.Id,
            Code = "2500",
            Name = "Employee/Accrued Payable",
            AccountType = AccountType.Liability,
            NormalBalance = NormalBalance.Credit,
            IsControlAccount = true,
            AllowManualPosting = false,
            IsActive = true
        };
        db.Accounts.Add(sharedPayable);
        await db.SaveChangesAsync();

        db.AccountingSettings.Add(new AccountingSettings
        {
            CompanyId = company.Id,
            FunctionalCurrencyCode = "USD",
            CashBankControlAccountId = sharedPayable.Id,
            AccountsReceivableAccountId = sharedPayable.Id,
            AccountsPayableAccountId = sharedPayable.Id,
            InventoryAccountId = sharedPayable.Id,
            InventoryInTransitAccountId = sharedPayable.Id,
            SupplierPrepaymentAccountId = sharedPayable.Id,
            CustomerAdvanceAccountId = sharedPayable.Id,
            FreightPayableAccountId = sharedPayable.Id,
            CommissionPayableAccountId = sharedPayable.Id,
            EmployeeAdvanceAccountId = sharedPayable.Id,
            EmployeePayableAccountId = sharedPayable.Id,
            AccruedExpenseAccountId = sharedPayable.Id,
            SalesRevenueAccountId = sharedPayable.Id,
            CostOfGoodsSoldAccountId = sharedPayable.Id,
            GeneralExpenseAccountId = sharedPayable.Id,
            ExchangeGainAccountId = sharedPayable.Id,
            ExchangeLossAccountId = sharedPayable.Id,
            InventoryLossAccountId = sharedPayable.Id,
            CurrentYearProfitLossAccountId = sharedPayable.Id,
            RetainedEarningsAccountId = sharedPayable.Id
        });
        await db.SaveChangesAsync();

        if (!hasJournalUsage)
            return;

        var year = new FiscalYear
        {
            CompanyId = company.Id,
            Name = "FY-2026",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            Status = FiscalYearStatus.Open,
            IsCurrent = true
        };
        db.FiscalYears.Add(year);
        await db.SaveChangesAsync();

        var period = new FiscalPeriod
        {
            CompanyId = company.Id,
            FiscalYearId = year.Id,
            PeriodNumber = 1,
            Name = "January",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31),
            Status = FiscalPeriodStatus.Open
        };
        db.FiscalPeriods.Add(period);
        await db.SaveChangesAsync();

        db.JournalEntries.Add(new JournalEntry
        {
            CompanyId = company.Id,
            FiscalYearId = year.Id,
            FiscalPeriodId = period.Id,
            JournalNumber = "LEGACY-DRAFT",
            Status = JournalEntryStatus.Draft,
            AccountingDate = new DateTime(2026, 1, 15),
            DocumentDate = new DateTime(2026, 1, 15),
            OperationDate = new DateTime(2026, 1, 15),
            SourceModule = "MigrationTest",
            Lines =
            [
                new JournalEntryLine
                {
                    LineNumber = 1,
                    AccountId = sharedPayable.Id,
                    Debit = 1m,
                    TransactionCurrencyCode = "USD",
                    TransactionAmount = 1m,
                    ExchangeRate = 1m
                }
            ]
        });
        await db.SaveChangesAsync();
    }
}
