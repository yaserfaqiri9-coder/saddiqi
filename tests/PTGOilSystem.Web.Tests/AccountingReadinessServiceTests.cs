using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services.Accounting;
using Xunit;

namespace PTGOilSystem.Web.Tests;

/// <summary>
/// مرحله ۹ — Readiness. سرویس فقط می‌خواند و هیچ چیز را تغییر نمی‌دهد؛ مهم‌ترین چیزی که باید
/// تضمین شود این است که آنچه اثبات‌نشدنی است Ready اعلام نشود.
/// </summary>
public class AccountingReadinessServiceTests
{
    [Fact]
    public async Task Company_Without_AccountingSettings_Is_Blocked()
    {
        await using var db = NewDb();
        SeedCompany(db);
        await db.SaveChangesAsync();

        var report = await NewService(db).BuildAsync();

        var company = Assert.Single(report.Companies);
        Assert.Equal(AccountingReadinessStatus.Blocked, company.Status);
        Assert.Contains(company.Findings, f => f.Code == "ACCOUNTING_SETTINGS_MISSING");
        Assert.Equal(AccountingReadinessStatus.Blocked, report.OverallStatus);
    }

    [Fact]
    public async Task Account_Owned_By_Another_Company_Is_Blocked()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedCompany(db, id: 2, code: "OTHER");
        SeedAccountsAndSettings(db, companyId: 1);
        SeedFiscal(db, companyId: 1);
        await db.SaveChangesAsync();

        // حسابِ نقدیِ شرکت ۱ به شرکت ۲ منتقل می‌شود: سند به دفتر اشتباه می‌نشیند.
        db.Accounts.Single(a => a.Id == AccountBaseId).CompanyId = 2;
        await db.SaveChangesAsync();

        var report = await NewService(db).BuildAsync();

        var company = report.Companies.Single(c => c.CompanyId == 1);
        var finding = Assert.Single(company.Findings, f => f.Code == "REQUIRED_ACCOUNT_WRONG_COMPANY");
        Assert.Equal(AccountingReadinessSeverity.Blocker, finding.Severity);
        Assert.Contains("OwnerCompanyId=2", finding.SampleRecords.Single());
        Assert.Equal(AccountingReadinessStatus.Blocked, company.Status);
    }

    [Fact]
    public async Task Inactive_Required_Account_Is_Blocked()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedAccountsAndSettings(db, companyId: 1);
        SeedFiscal(db, companyId: 1);
        await db.SaveChangesAsync();

        db.Accounts.Single(a => a.Id == AccountBaseId).IsActive = false;
        await db.SaveChangesAsync();

        var report = await NewService(db).BuildAsync();

        var company = report.Companies.Single(c => c.CompanyId == 1);
        Assert.Contains(company.Findings, f => f.Code == "REQUIRED_ACCOUNT_INACTIVE");
        Assert.Equal(AccountingReadinessStatus.Blocked, company.Status);
    }

    [Fact]
    public async Task Missing_Open_Fiscal_Period_Is_Blocked()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedAccountsAndSettings(db, companyId: 1);
        db.FiscalYears.Add(new FiscalYear
        {
            Id = 1,
            CompanyId = 1,
            Name = "1405",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            Status = FiscalYearStatus.Open,
            IsCurrent = true
        });
        await db.SaveChangesAsync();

        var report = await NewService(db).BuildAsync();

        var company = report.Companies.Single(c => c.CompanyId == 1);
        Assert.Contains(company.Findings, f => f.Code == "NO_OPEN_FISCAL_PERIOD");
        Assert.Equal(AccountingReadinessStatus.Blocked, company.Status);
    }

    [Fact]
    public async Task Unbalanced_Journal_Is_Blocked()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedAccountsAndSettings(db, companyId: 1);
        SeedFiscal(db, companyId: 1);
        db.JournalEntries.Add(new JournalEntry
        {
            Id = 1,
            CompanyId = 1,
            FiscalYearId = 1,
            FiscalPeriodId = 1,
            JournalNumber = "JV-1",
            Status = JournalEntryStatus.Posted,
            PostedAt = DateTime.UtcNow,
            SourceModule = "Purchase",
            Lines =
            [
                new JournalEntryLine { AccountId = AccountBaseId, LineNumber = 1, Debit = 100m },
                new JournalEntryLine { AccountId = AccountBaseId + 1, LineNumber = 2, Credit = 90m }
            ]
        });
        await db.SaveChangesAsync();

        var report = await NewService(db).BuildAsync();

        var company = report.Companies.Single(c => c.CompanyId == 1);
        var finding = Assert.Single(company.Findings, f => f.Code == "UNBALANCED_JOURNAL");
        Assert.Equal(AccountingReadinessSeverity.Blocker, finding.Severity);
        Assert.Contains("Debit=100", finding.SampleRecords.Single());
    }

    [Fact]
    public async Task Duplicate_SourceEventId_Is_A_Global_Blocker()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedAccountsAndSettings(db, companyId: 1);
        SeedFiscal(db, companyId: 1);
        db.JournalEntries.AddRange(
            BalancedJournal(1, "Purchase:5:Created:0"),
            BalancedJournal(2, "Purchase:5:Created:0"));
        await db.SaveChangesAsync();

        var report = await NewService(db).BuildAsync();

        var finding = Assert.Single(report.GlobalFindings, f => f.Code == "DUPLICATE_SOURCE_EVENT_ID");
        Assert.Equal(AccountingReadinessSeverity.Blocker, finding.Severity);
        Assert.Contains(report.Blockers, f => f.Code == "DUPLICATE_SOURCE_EVENT_ID");
    }

    [Fact]
    public async Task Payment_Without_Company_Is_Blocked()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedAccountsAndSettings(db, companyId: 1);
        SeedFiscal(db, companyId: 1);
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 1,
            CompanyId = null,
            PaymentKind = PaymentKind.SupplierPayment,
            Direction = PaymentDirection.Out,
            PaymentDate = new DateTime(2026, 5, 1),
            Amount = 100m,
            AmountUsd = 100m
        });
        await db.SaveChangesAsync();

        var report = await NewService(db).BuildAsync();

        var company = report.Companies.Single(c => c.CompanyId == 1);
        var finding = Assert.Single(company.Findings, f => f.Code == "PAYMENT_WITHOUT_COMPANY");
        Assert.Equal(1, finding.RecordCount);
    }

    [Fact]
    public async Task Customer_Receipt_Without_Advance_Marker_Is_Blocked()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedAccountsAndSettings(db, companyId: 1);
        SeedFiscal(db, companyId: 1);
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 1,
            CompanyId = 1,
            CustomerId = 1,
            PaymentKind = PaymentKind.CustomerReceipt,
            Direction = PaymentDirection.In,
            PaymentDate = new DateTime(2026, 5, 1),
            Amount = 100m,
            AmountUsd = 100m,
            IsCustomerAdvance = null
        });
        await db.SaveChangesAsync();

        var report = await NewService(db).BuildAsync();

        var company = report.Companies.Single(c => c.CompanyId == 1);
        Assert.Contains(company.Findings, f => f.Code == "CUSTOMER_ADVANCE_MARKER_UNKNOWN");
    }

    [Fact]
    public async Task Marked_Customer_Receipt_Raises_No_Advance_Finding()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedAccountsAndSettings(db, companyId: 1);
        SeedFiscal(db, companyId: 1);
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 1,
            CompanyId = 1,
            CustomerId = 1,
            PaymentKind = PaymentKind.CustomerReceipt,
            Direction = PaymentDirection.In,
            PaymentDate = new DateTime(2026, 5, 1),
            Amount = 100m,
            AmountUsd = 100m,
            IsCustomerAdvance = true
        });
        await db.SaveChangesAsync();

        var report = await NewService(db).BuildAsync();

        var company = report.Companies.Single(c => c.CompanyId == 1);
        Assert.DoesNotContain(company.Findings, f => f.Code == "CUSTOMER_ADVANCE_MARKER_UNKNOWN");
    }

    [Fact]
    public async Task Inventory_Quantity_Without_Value_Is_Blocked()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedAccountsAndSettings(db, companyId: 1);
        SeedFiscal(db, companyId: 1);
        db.InventoryAverageCosts.Add(new InventoryAverageCost
        {
            Id = 1,
            CompanyId = 1,
            ProductId = 1,
            TerminalId = 1,
            QuantityMt = 50m,
            TotalValueUsd = 0m
        });
        await db.SaveChangesAsync();

        var report = await NewService(db).BuildAsync();

        var company = report.Companies.Single(c => c.CompanyId == 1);
        var finding = Assert.Single(company.Findings, f => f.Code == "INVENTORY_QUANTITY_WITHOUT_VALUE");
        Assert.Equal(AccountingReadinessSeverity.Blocker, finding.Severity);
        Assert.Equal("Accounting:Pilots:Cogs", finding.FeatureFlag);
    }

    [Fact]
    public async Task Sarraf_Settlements_Require_Operational_Validation_And_Never_Read_Ready()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedAccountsAndSettings(db, companyId: 1);
        SeedFiscal(db, companyId: 1);
        db.Sarrafs.Add(new Sarraf { Id = 1, Name = "Sarraf A" });
        db.SarrafSettlements.Add(new SarrafSettlement
        {
            Id = 1,
            SarrafId = 1,
            SettlementDate = new DateTime(2026, 5, 1),
            RequestedAmount = 1_000m,
            RequestedAmountUsd = 1_000m,
            SarrafChargedAmount = 80_000m,
            SarrafChargedAmountUsd = 1_010m,
            SupplierAcceptedAmount = 990m,
            SupplierAcceptedAmountUsd = 990m
        });
        await db.SaveChangesAsync();

        var report = await NewService(db).BuildAsync();

        var company = report.Companies.Single(c => c.CompanyId == 1);
        var finding = Assert.Single(company.Findings, f => f.Code == "SARRAF_OPERATIONAL_VALIDATION_REQUIRED");
        Assert.Equal(AccountingReadinessSeverity.OperationalDataValidationRequired, finding.Severity);
        Assert.Equal("Accounting:Pilots:SarrafSettlement", finding.FeatureFlag);
        Assert.NotEqual(AccountingReadinessStatus.Ready, company.Status);
    }

    [Fact]
    public async Task Transfer_Legs_Block_While_The_Transfer_Flag_Is_Off()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedAccountsAndSettings(db, companyId: 1);
        SeedFiscal(db, companyId: 1);
        SeedTransferLeg(db);
        await db.SaveChangesAsync();

        var report = await NewService(db).BuildAsync();

        var company = report.Companies.Single(c => c.CompanyId == 1);
        var finding = Assert.Single(company.Findings, f => f.Code == "TRANSFER_COST_NOT_MOVED");
        Assert.Equal(AccountingReadinessSeverity.Blocker, finding.Severity);
        Assert.Equal("Accounting:Pilots:InventoryTransfer", finding.FeatureFlag);
    }

    [Fact]
    public async Task Adapters_Report_Every_Record_Skipping_While_Accounting_Is_Disabled()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedAccountsAndSettings(db, companyId: 1);
        SeedFiscal(db, companyId: 1);
        SeedTransferLeg(db);
        await db.SaveChangesAsync();

        var report = await NewService(db).BuildAsync();

        Assert.False(report.AccountingEnabled);
        Assert.All(report.Adapters, a =>
        {
            Assert.False(a.FeatureFlagEnabled);
            Assert.Equal("ACCOUNTING_DISABLED", a.ProjectedSkipReason);
        });

        var transfer = report.Adapters.Single(a => a.SourceModule == InventoryTransferAccountingAdapter.SourceModule);
        Assert.Equal(1, transfer.CandidateRecordCount);
        Assert.Equal(0, transfer.PostedJournalCount);
    }

    [Fact]
    public async Task Pilot_Disabled_Is_Reported_Separately_From_Accounting_Disabled()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedAccountsAndSettings(db, companyId: 1);
        SeedFiscal(db, companyId: 1);
        await db.SaveChangesAsync();

        var options = NewOptions();
        options.Enabled = true; // کلید اصلی روشن، ولی هیچ Pilot روشن نیست.
        var report = await new AccountingReadinessService(db, Options.Create(options)).BuildAsync();

        Assert.True(report.AccountingEnabled);
        Assert.All(report.Adapters, a => Assert.Equal("PILOT_DISABLED", a.ProjectedSkipReason));
        Assert.DoesNotContain(report.GlobalFindings, f => f.Code == "ACCOUNTING_DISABLED");
    }

    [Fact]
    public async Task Full_Suite_And_Skip_Counts_Are_Reported_As_External_Evidence()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedAccountsAndSettings(db, companyId: 1);
        SeedFiscal(db, companyId: 1);
        await db.SaveChangesAsync();

        var report = await NewService(db).BuildAsync();

        // هیچ‌کدام از دیتابیس اثبات‌شدنی نیستند، پس هرگز به‌عنوان Ready جا نمی‌زنند.
        Assert.Contains(report.GlobalFindings, f =>
            f.Code == "FULL_SUITE_EXTERNAL_EVIDENCE"
            && f.Severity == AccountingReadinessSeverity.OperationalDataValidationRequired);
        Assert.Contains(report.GlobalFindings, f =>
            f.Code == "SKIP_COUNTS_REQUIRE_LOG_HARVEST"
            && f.Severity == AccountingReadinessSeverity.OperationalDataValidationRequired);
    }

    [Fact]
    public async Task Report_Writes_Nothing()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedAccountsAndSettings(db, companyId: 1);
        SeedFiscal(db, companyId: 1);
        SeedTransferLeg(db);
        await db.SaveChangesAsync();

        var before = await SnapshotAsync(db);
        await NewService(db).BuildAsync();
        var after = await SnapshotAsync(db);

        Assert.Equal(before, after);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
    }

    private const int AccountBaseId = 100;

    private static async Task<string> SnapshotAsync(ApplicationDbContext db)
        => string.Join("|",
            await db.JournalEntries.AsNoTracking().CountAsync(),
            await db.LedgerEntries.AsNoTracking().CountAsync(),
            await db.InventoryAverageCosts.AsNoTracking().CountAsync(),
            await db.PaymentTransactions.AsNoTracking().CountAsync(),
            await db.AccountingSettings.AsNoTracking().CountAsync(),
            await db.Accounts.AsNoTracking().CountAsync());

    private static ApplicationDbContext NewDb()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // پیش‌فرضِ repo: همه چیز خاموش. تست‌ها همین را می‌سنجند.
    private static AccountingOptions NewOptions() => new();

    private static AccountingReadinessService NewService(ApplicationDbContext db)
        => new(db, Options.Create(NewOptions()));

    private static void SeedCompany(ApplicationDbContext db, int id = 1, string code = "PTG")
        => db.Companies.Add(new Company { Id = id, Code = code, Name = $"Company {code}", IsActive = true });

    private static JournalEntry BalancedJournal(int id, string sourceEventId)
        => new()
        {
            Id = id,
            CompanyId = 1,
            FiscalYearId = 1,
            FiscalPeriodId = 1,
            JournalNumber = $"JV-{id}",
            Status = JournalEntryStatus.Posted,
            PostedAt = DateTime.UtcNow,
            SourceModule = "Purchase",
            SourceEventId = sourceEventId,
            Lines =
            [
                new JournalEntryLine { AccountId = AccountBaseId, LineNumber = 1, Debit = 100m },
                new JournalEntryLine { AccountId = AccountBaseId + 1, LineNumber = 2, Credit = 100m }
            ]
        };

    private static void SeedTransferLeg(ApplicationDbContext db)
    {
        db.Products.Add(new Product { Id = 1, Code = "GO", Name = "Gas Oil", IsActive = true });
        db.Contracts.Add(new Contract
        {
            Id = 1,
            ContractNumber = "PUR-1",
            ContractType = ContractType.Purchase,
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 1,
            ContractDate = new DateTime(2026, 5, 1),
            QuantityMt = 100m
        });
        db.InventoryTransportLegs.Add(new InventoryTransportLeg
        {
            Id = 1,
            SourcePurchaseContractId = 1,
            ProductId = 1,
            SourceTerminalId = 1,
            DestinationTerminalId = 2,
            LoadedDate = new DateTime(2026, 5, 2)
        });
    }

    private static void SeedFiscal(ApplicationDbContext db, int companyId)
    {
        db.FiscalYears.Add(new FiscalYear
        {
            Id = 1,
            CompanyId = companyId,
            Name = "1405",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            Status = FiscalYearStatus.Open,
            IsCurrent = true
        });
        db.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = 1,
            CompanyId = companyId,
            FiscalYearId = 1,
            PeriodNumber = 1,
            Name = "P1",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31),
            Status = FiscalPeriodStatus.Open
        });
    }

    // بیست حساب لازم، همه فعال و متعلق به همین شرکت — یعنی حالت سالم، تا هر تست فقط
    // همان یک چیزی را که می‌سنجد خراب کند.
    private static void SeedAccountsAndSettings(ApplicationDbContext db, int companyId)
    {
        for (var i = 0; i < 20; i++)
        {
            db.Accounts.Add(new Account
            {
                Id = AccountBaseId + i,
                CompanyId = companyId,
                Code = $"ACC-{i:00}",
                Name = $"Account {i}",
                AccountType = AccountType.Asset,
                NormalBalance = NormalBalance.Debit,
                IsActive = true
            });
        }

        db.AccountingSettings.Add(new AccountingSettings
        {
            Id = companyId,
            CompanyId = companyId,
            FunctionalCurrencyCode = "USD",
            CashBankControlAccountId = AccountBaseId,
            AccountsReceivableAccountId = AccountBaseId + 1,
            AccountsPayableAccountId = AccountBaseId + 2,
            InventoryAccountId = AccountBaseId + 3,
            InventoryInTransitAccountId = AccountBaseId + 4,
            SupplierPrepaymentAccountId = AccountBaseId + 5,
            CustomerAdvanceAccountId = AccountBaseId + 6,
            FreightPayableAccountId = AccountBaseId + 7,
            CommissionPayableAccountId = AccountBaseId + 8,
            EmployeeAdvanceAccountId = AccountBaseId + 9,
            EmployeePayableAccountId = AccountBaseId + 10,
            AccruedExpenseAccountId = AccountBaseId + 11,
            SalesRevenueAccountId = AccountBaseId + 12,
            CostOfGoodsSoldAccountId = AccountBaseId + 13,
            GeneralExpenseAccountId = AccountBaseId + 14,
            ExchangeGainAccountId = AccountBaseId + 15,
            ExchangeLossAccountId = AccountBaseId + 16,
            InventoryLossAccountId = AccountBaseId + 17,
            CurrentYearProfitLossAccountId = AccountBaseId + 18,
            RetainedEarningsAccountId = AccountBaseId + 19
        });
    }
}
