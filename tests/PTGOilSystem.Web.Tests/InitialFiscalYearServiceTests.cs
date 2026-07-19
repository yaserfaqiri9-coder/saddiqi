using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Security;
using PTGOilSystem.Web.Services.Accounting;
using Xunit;

namespace PTGOilSystem.Web.Tests;

/// <summary>
/// مسیرِ رسمیِ ساختِ «اولین سال مالی» یک شرکت. برخلافِ ساختِ سالِ بعد که Draft از روی سالِ منبع
/// آینه می‌کند، سالِ اول باید Open و جاری باشد و دوره‌هایش کلِ بازهٔ سال را دقیقاً بپوشانند.
/// </summary>
public class InitialFiscalYearServiceTests
{
    // ── دسترسی ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateInitialYear_Is_Post_Only_With_Antiforgery_And_AdminOnly()
    {
        var action = typeof(FiscalYearsController)
            .GetMethod(nameof(FiscalYearsController.CreateInitialYear))!;

        Assert.NotEmpty(action.GetCustomAttributes(typeof(HttpPostAttribute), true));
        Assert.Empty(action.GetCustomAttributes(typeof(HttpGetAttribute), true));
        Assert.NotEmpty(action.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true));

        var policies = action
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Select(a => a.Policy);
        Assert.Contains(AuthPolicies.AdminOnly, policies);
    }

    // ── ساختِ موفق ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Creates_The_First_Year_As_Open_Current_With_No_Previous_And_Audits_It()
    {
        await using var db = NewDb();
        SeedCompany(db);
        await db.SaveChangesAsync();

        var result = await NewProvisioning(db).CreateInitialFiscalYearAsync(
            Input(name: "FY-2026", start: new DateTime(2026, 1, 1), end: new DateTime(2026, 12, 31), periods: 12),
            actorUserId: 7);

        Assert.True(result.Succeeded);
        var year = await db.FiscalYears.SingleAsync(y => y.Id == result.FiscalYearId);
        Assert.Equal("FY-2026", year.Name);
        Assert.Equal(FiscalYearStatus.Open, year.Status);
        Assert.True(year.IsCurrent);
        Assert.Null(year.PreviousFiscalYearId);
        Assert.Equal(7, year.OpenedByUserId);
        Assert.NotNull(year.OpenedAt);

        var audit = await db.AuditLogs.SingleAsync(a => a.EntityName == nameof(FiscalYear));
        Assert.Contains("CreateInitialFiscalYear", audit.Diff);
    }

    [Fact]
    public async Task Generates_Contiguous_Monthly_Periods_That_Exactly_Cover_The_Year()
    {
        await using var db = NewDb();
        SeedCompany(db);
        await db.SaveChangesAsync();

        var result = await NewProvisioning(db).CreateInitialFiscalYearAsync(
            Input(name: "FY-2026", start: new DateTime(2026, 1, 1), end: new DateTime(2026, 12, 31), periods: 12),
            actorUserId: 7);
        Assert.True(result.Succeeded);

        var periods = await db.FiscalPeriods
            .Where(p => p.FiscalYearId == result.FiscalYearId)
            .OrderBy(p => p.PeriodNumber)
            .ToListAsync();

        Assert.Equal(12, periods.Count);
        Assert.All(periods, p => Assert.Equal(FiscalPeriodStatus.Open, p.Status));
        Assert.All(periods, p => Assert.Equal(1, p.CompanyId));
        // پوششِ دقیق: اول از شروع، آخر تا پایان، و هیچ فاصله/هم‌پوشانی‌ای بین دوره‌ها.
        Assert.Equal(new DateTime(2026, 1, 1), periods[0].StartDate);
        Assert.Equal(new DateTime(2026, 12, 31), periods[^1].EndDate);
        Assert.Equal(new DateTime(2026, 1, 31), periods[0].EndDate);
        Assert.Equal(new DateTime(2026, 2, 1), periods[1].StartDate);
        for (var i = 1; i < periods.Count; i++)
            Assert.Equal(periods[i - 1].EndDate.AddDays(1), periods[i].StartDate);
    }

    [Fact]
    public async Task Only_One_Current_And_One_Open_Year_Exists_After_Creation()
    {
        await using var db = NewDb();
        SeedCompany(db);
        await db.SaveChangesAsync();

        await NewProvisioning(db).CreateInitialFiscalYearAsync(
            Input(name: "FY-2026", start: new DateTime(2026, 1, 1), end: new DateTime(2026, 12, 31), periods: 12),
            actorUserId: 7);

        Assert.Equal(1, await db.FiscalYears.CountAsync(y => y.CompanyId == 1 && y.IsCurrent));
        Assert.Equal(1, await db.FiscalYears.CountAsync(y => y.CompanyId == 1 && y.Status == FiscalYearStatus.Open));
    }

    // ── جلوگیری از سالِ اولِ دوم ────────────────────────────────────────────────────

    [Fact]
    public async Task Refuses_When_The_Company_Already_Has_A_Fiscal_Year()
    {
        await using var db = NewDb();
        SeedCompany(db);
        db.FiscalYears.Add(new FiscalYear
        {
            Id = 1,
            CompanyId = 1,
            Name = "FY-2026",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            Status = FiscalYearStatus.Open,
            IsCurrent = true
        });
        await db.SaveChangesAsync();

        var result = await NewProvisioning(db).CreateInitialFiscalYearAsync(
            Input(name: "FY-2027", start: new DateTime(2027, 1, 1), end: new DateTime(2027, 12, 31), periods: 12),
            actorUserId: 7);

        Assert.False(result.Succeeded);
        Assert.Equal("FISCAL_YEAR_ALREADY_EXISTS", result.ErrorCode);
        Assert.Equal(1, await db.FiscalYears.CountAsync());
    }

    [Fact]
    public async Task A_Second_Call_Is_Rejected_Even_When_The_First_Created_The_Year()
    {
        await using var db = NewDb();
        SeedCompany(db);
        await db.SaveChangesAsync();
        var provisioning = NewProvisioning(db);

        var first = await provisioning.CreateInitialFiscalYearAsync(
            Input(name: "FY-2026", start: new DateTime(2026, 1, 1), end: new DateTime(2026, 12, 31), periods: 12),
            actorUserId: 7);
        Assert.True(first.Succeeded);

        var second = await provisioning.CreateInitialFiscalYearAsync(
            Input(name: "FY-2026-again", start: new DateTime(2026, 1, 1), end: new DateTime(2026, 12, 31), periods: 12),
            actorUserId: 7);

        Assert.False(second.Succeeded);
        Assert.Equal(1, await db.FiscalYears.CountAsync());
    }

    // ── اعتبارسنجی و نبودِ نوشتنِ ناقص ──────────────────────────────────────────────

    [Theory]
    [InlineData("", "2026-01-01", "2026-12-31", 12, "FISCAL_YEAR_NAME_REQUIRED")]
    [InlineData("FY", "2026-12-31", "2026-01-01", 12, "END_DATE_NOT_AFTER_START_DATE")]
    [InlineData("FY", "2026-01-01", "2026-01-01", 12, "END_DATE_NOT_AFTER_START_DATE")]
    [InlineData("FY", "2026-01-01", "2026-12-31", 0, "PERIOD_COUNT_INVALID")]
    // تعداد دوره از طولِ بازه بیشتر است → پوشش دقیق ممکن نیست.
    [InlineData("FY", "2026-01-01", "2026-06-30", 12, "PERIOD_LAYOUT_DOES_NOT_COVER_YEAR")]
    public async Task Invalid_Input_Fails_And_Writes_Nothing(
        string name, string start, string end, int periods, string expectedCode)
    {
        await using var db = NewDb();
        SeedCompany(db);
        await db.SaveChangesAsync();

        var result = await NewProvisioning(db).CreateInitialFiscalYearAsync(
            Input(name, DateTime.Parse(start), DateTime.Parse(end), periods),
            actorUserId: 7);

        Assert.False(result.Succeeded);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal(0, await db.FiscalYears.CountAsync());
        Assert.Equal(0, await db.FiscalPeriods.CountAsync());
        Assert.Equal(0, await db.AuditLogs.CountAsync());
        Assert.False(db.ChangeTracker.HasChanges());
    }

    // ── Readiness ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Creating_The_Initial_Year_Clears_The_No_Open_Fiscal_Year_Finding()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedAccountsAndSettings(db);
        await db.SaveChangesAsync();

        var readiness = new AccountingReadinessService(db, Options.Create(new AccountingOptions()));
        var before = await readiness.BuildAsync();
        Assert.Contains(
            before.Companies.Single(c => c.CompanyId == 1).Findings,
            f => f.Code == "NO_OPEN_FISCAL_YEAR");

        var result = await NewProvisioning(db).CreateInitialFiscalYearAsync(
            Input(name: "FY-2026", start: new DateTime(2026, 1, 1), end: new DateTime(2026, 12, 31), periods: 12),
            actorUserId: 7);
        Assert.True(result.Succeeded);

        var after = await readiness.BuildAsync();
        var companyFindings = after.Companies.Single(c => c.CompanyId == 1).Findings;
        Assert.DoesNotContain(companyFindings, f => f.Code == "NO_OPEN_FISCAL_YEAR");
        Assert.DoesNotContain(companyFindings, f => f.Code == "NO_OPEN_FISCAL_PERIOD");
    }

    // ── داربست ────────────────────────────────────────────────────────────────────

    private static CreateInitialFiscalYearInput Input(string name, DateTime start, DateTime end, int periods)
        => new(CompanyId: 1, Name: name, StartDate: start, EndDate: end, PeriodCount: periods);

    private static ApplicationDbContext NewDb()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FiscalYearOverviewService NewOverview(ApplicationDbContext db)
        => new(db, new AccountingReadinessService(db, Options.Create(new AccountingOptions())));

    private static FiscalYearProvisioningService NewProvisioning(ApplicationDbContext db)
        => new(db, NewOverview(db), new PTGOilSystem.Web.Services.AuditService(db));

    private static void SeedCompany(ApplicationDbContext db, int id = 1, string code = "A")
        => db.Companies.Add(new Company { Id = id, Code = code, Name = $"Company {code}", Country = "AF", IsActive = true });

    // بیست حسابِ لازم + تنظیمات، تا Readiness از گاردِ «تنظیمات نیست» عبور کند و به بررسیِ سالِ
    // مالی برسد — همان الگوی FiscalYearUiTests.
    private static void SeedAccountsAndSettings(ApplicationDbContext db)
    {
        for (var i = 0; i < 20; i++)
        {
            db.Accounts.Add(new Account
            {
                Id = 900 + i,
                CompanyId = 1,
                Code = $"ACC-{i:00}",
                Name = $"Account {i}",
                AccountType = AccountType.Asset,
                NormalBalance = NormalBalance.Debit,
                IsActive = true
            });
        }

        db.AccountingSettings.Add(new AccountingSettings
        {
            Id = 1,
            CompanyId = 1,
            FunctionalCurrencyCode = "USD",
            CashBankControlAccountId = 900,
            AccountsReceivableAccountId = 901,
            AccountsPayableAccountId = 902,
            InventoryAccountId = 903,
            InventoryInTransitAccountId = 904,
            SupplierPrepaymentAccountId = 905,
            CustomerAdvanceAccountId = 906,
            FreightPayableAccountId = 907,
            CommissionPayableAccountId = 908,
            EmployeeAdvanceAccountId = 909,
            EmployeePayableAccountId = 910,
            AccruedExpenseAccountId = 911,
            SalesRevenueAccountId = 912,
            CostOfGoodsSoldAccountId = 913,
            GeneralExpenseAccountId = 914,
            ExchangeGainAccountId = 915,
            ExchangeLossAccountId = 916,
            InventoryLossAccountId = 917,
            CurrentYearProfitLossAccountId = 918,
            RetainedEarningsAccountId = 919
        });
    }
}
