using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services.Accounting;
using Xunit;

namespace PTGOilSystem.Web.Tests;

/// <summary>
/// مرحله ۱۵ — بازگشاییِ کنترل‌شده. آثارِ Final Close فقط با Reversal رسمی برمی‌گردند؛ هیچ سندی حذف
/// نمی‌شود، فقط آخرین دوره باز می‌شود و تاریخچه پاک نمی‌گردد.
/// </summary>
public class ReopenFiscalYearServiceTests
{
    private const int Base = 100;
    private const int CashId = Base;
    private const int RevenueId = Base + 12;
    private const int ExpenseId = Base + 13;
    private const string YearName = "FY-2025";
    private static readonly DateTime EndDate = new(2025, 12, 31);

    [Fact]
    public async Task Reopen_Latest_Closed_Year_Reverses_Closing_Without_Deleting_And_Opens_Only_Last_Period()
    {
        await using var db = NewDb();
        await SeedAndCloseAsync(db, periods: 2);

        var closingCountBefore = db.JournalEntries.Count(j => j.SourceModule == "FiscalYearClose" && j.IsClosing);
        Assert.Equal(2, closingCountBefore); // P&L + RE

        var result = await Reopen(db).ReopenAsync(1, 1, userId: 3, reason: "اصلاح", confirmation: YearName, hasPermission: true);
        Assert.Equal(ReopenResultStatus.Succeeded, result.Status);

        var year = db.FiscalYears.Single(y => y.Id == 1);
        Assert.Equal(FiscalYearStatus.Reopened, year.Status);
        Assert.True(year.IsCurrent);
        Assert.NotNull(year.ReopenedAt);
        Assert.Equal(3, year.ReopenedByUserId);
        Assert.Equal("اصلاح", year.ReopenReason);
        Assert.NotNull(year.ClosedAt); // تاریخچه پاک نشده

        // سندهای بستنِ اصلی هنوز هستند (حذف نشده‌اند) و Reversal جدید ساخته شده.
        Assert.Equal(2, db.JournalEntries.Count(j => j.SourceModule == "FiscalYearClose" && j.IsClosing));
        Assert.True(db.JournalEntries.Count(j => j.IsReversal) >= 2);

        var periods = db.FiscalPeriods.Where(p => p.FiscalYearId == 1).OrderBy(p => p.PeriodNumber).ToList();
        Assert.Equal(FiscalPeriodStatus.HardLocked, periods[0].Status); // دورهٔ قبلی قفل می‌ماند
        Assert.Equal(FiscalPeriodStatus.Open, periods[1].Status);       // فقط آخرین دوره باز

        var run = db.FiscalYearCloseRuns.Single(r => r.RunType == FiscalYearCloseRunType.Final);
        Assert.Equal("REOPENED", run.FailureCode);
    }

    [Fact]
    public async Task Next_Year_Returns_To_Draft_When_It_Has_No_Operational_Postings()
    {
        await using var db = NewDb();
        await SeedAndCloseAsync(db, periods: 1);

        await Reopen(db).ReopenAsync(1, 1, 1, "x", YearName, true);

        var next = db.FiscalYears.Single(y => y.Id == 2);
        Assert.Equal(FiscalYearStatus.Draft, next.Status);
        Assert.False(next.IsCurrent);
    }

    [Fact]
    public async Task Next_Year_With_Only_System_Journal_Still_Allows_Reopen()
    {
        await using var db = NewDb();
        await SeedAndCloseAsync(db, periods: 1);
        // سندِ سیستمیِ FiscalYearClose در سال بعد (مثل برگشتِ خودکارِ تسعیر) نباید مانع باشد.
        db.JournalEntries.Add(SystemJournal(9100, fiscalYearId: 2, periodId: 2));
        await db.SaveChangesAsync();

        var precheck = await Reopen(db).PrecheckAsync(1, 1);
        Assert.True(precheck!.CanReopen);
    }

    [Fact]
    public async Task Next_Year_With_Operational_Posting_Blocks_Reopen()
    {
        await using var db = NewDb();
        await SeedAndCloseAsync(db, periods: 1);
        db.JournalEntries.Add(new JournalEntry
        {
            Id = 9200, CompanyId = 1, FiscalYearId = 2, FiscalPeriodId = 2,
            JournalNumber = "OP", Status = JournalEntryStatus.Posted, PostedAt = DateTime.UtcNow,
            AccountingDate = new DateTime(2026, 3, 1), SourceModule = "Sale",
            Lines = [ new() { AccountId = CashId, LineNumber = 1, Debit = 5m },
                      new() { AccountId = RevenueId, LineNumber = 2, Credit = 5m } ]
        });
        await db.SaveChangesAsync();

        var result = await Reopen(db).ReopenAsync(1, 1, 1, "x", YearName, true);
        Assert.Equal(ReopenResultStatus.Blocked, result.Status);
        Assert.Contains(ReopenReasons.NextYearHasOperationalPostings, result.Blockers);
    }

    [Fact]
    public async Task Reject_Non_Latest_When_A_Later_Closed_Year_Exists()
    {
        await using var db = NewDb();
        await SeedAndCloseAsync(db, periods: 1);
        // یک سالِ بسته‌ی بعد از این.
        db.FiscalYears.Add(new FiscalYear
        {
            Id = 3, CompanyId = 1, Name = "FY-2027", StartDate = new DateTime(2027, 1, 1),
            EndDate = new DateTime(2027, 12, 31), Status = FiscalYearStatus.Closed
        });
        await db.SaveChangesAsync();

        var precheck = await Reopen(db).PrecheckAsync(1, 1);
        Assert.Contains(ReopenReasons.LaterClosedYearExists, precheck!.Blockers);
    }

    [Fact]
    public async Task Permission_Is_Required()
    {
        await using var db = NewDb();
        await SeedAndCloseAsync(db, periods: 1);

        var result = await Reopen(db).ReopenAsync(1, 1, 1, "x", YearName, hasPermission: false);
        Assert.Equal(ReopenReasons.PermissionRequired, result.FailureCode);
    }

    [Fact]
    public async Task Reason_Is_Required()
    {
        await using var db = NewDb();
        await SeedAndCloseAsync(db, periods: 1);

        var result = await Reopen(db).ReopenAsync(1, 1, 1, reason: "  ", confirmation: YearName, hasPermission: true);
        Assert.Equal(ReopenReasons.ReasonRequired, result.FailureCode);
    }

    [Fact]
    public async Task Confirmation_Must_Match_Year_Code()
    {
        await using var db = NewDb();
        await SeedAndCloseAsync(db, periods: 1);

        var result = await Reopen(db).ReopenAsync(1, 1, 1, "x", confirmation: "WRONG", hasPermission: true);
        Assert.Equal(ReopenReasons.ConfirmationInvalid, result.FailureCode);
    }

    [Fact]
    public async Task Not_Closed_Year_Cannot_Be_Reopened()
    {
        await using var db = NewDb();
        await SeedAndCloseAsync(db, periods: 1, close: false);

        var precheck = await Reopen(db).PrecheckAsync(1, 1);
        Assert.Contains(ReopenReasons.NotClosed, precheck!.Blockers);
    }

    [Fact]
    public async Task Second_Reopen_Is_Idempotent()
    {
        await using var db = NewDb();
        await SeedAndCloseAsync(db, periods: 1);

        await Reopen(db).ReopenAsync(1, 1, 1, "x", YearName, true);
        var reversalsAfterFirst = db.JournalEntries.Count(j => j.IsReversal);

        var second = await Reopen(db).ReopenAsync(1, 1, 1, "x", YearName, true);
        Assert.Equal(ReopenResultStatus.AlreadyReopened, second.Status);
        Assert.Equal(reversalsAfterFirst, db.JournalEntries.Count(j => j.IsReversal));
    }

    [Fact]
    public async Task Company_Isolation_Unknown_Year_Returns_Null()
    {
        await using var db = NewDb();
        await SeedAndCloseAsync(db, periods: 1);
        Assert.Null(await Reopen(db).PrecheckAsync(companyId: 999, fiscalYearId: 1));
    }

    // ---- helpers ----

    private static ApplicationDbContext NewDb()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AccountingPostingService Posting(ApplicationDbContext db)
        => new(db, new PeriodGuard(db, new FiscalCalendarService(db)),
            Options.Create(new AccountingOptions { Enabled = true }), new SystemCompanyProvider(db));

    private static IReopenFiscalYearService Reopen(ApplicationDbContext db)
        => new ReopenFiscalYearService(db, Posting(db));

    private static JournalEntry SystemJournal(int id, int fiscalYearId, int periodId)
        => new()
        {
            Id = id, CompanyId = 1, FiscalYearId = fiscalYearId, FiscalPeriodId = periodId,
            JournalNumber = $"SYS-{id}", Status = JournalEntryStatus.Posted, PostedAt = DateTime.UtcNow,
            AccountingDate = new DateTime(2026, 1, 1), SourceModule = "FiscalYearClose",
            Lines = [ new() { AccountId = CashId, LineNumber = 1, Debit = 1m },
                      new() { AccountId = RevenueId, LineNumber = 2, Credit = 1m } ]
        };

    private static async Task SeedAndCloseAsync(ApplicationDbContext db, int periods, bool close = true)
    {
        var options = Options.Create(new AccountingOptions { Enabled = true });

        db.Companies.Add(new Company { Id = 1, Code = "PTG", Name = "PTG", IsActive = true, IsSystemOwner = true });
        for (var i = 0; i < 20; i++)
        {
            db.Accounts.Add(new Account
            {
                Id = Base + i, CompanyId = 1, Code = $"ACC-{i:00}", Name = $"Account {i}",
                AccountType = i == 12 ? AccountType.Revenue
                    : i is 13 or 14 or 16 or 17 ? AccountType.Expense
                    : i is 18 or 19 ? AccountType.Equity : AccountType.Asset,
                NormalBalance = NormalBalance.Debit, IsActive = true,
                MonetaryTreatment = MonetaryTreatment.NonMonetary
            });
        }

        db.AccountingSettings.Add(new AccountingSettings
        {
            Id = 1, CompanyId = 1, FunctionalCurrencyCode = "USD",
            CashBankControlAccountId = Base, AccountsReceivableAccountId = Base + 1, AccountsPayableAccountId = Base + 2,
            InventoryAccountId = Base + 3, InventoryInTransitAccountId = Base + 4,
            SupplierPrepaymentAccountId = Base + 5, CustomerAdvanceAccountId = Base + 6,
            FreightPayableAccountId = Base + 7, CommissionPayableAccountId = Base + 8,
            EmployeeAdvanceAccountId = Base + 9, EmployeePayableAccountId = Base + 10,
            AccruedExpenseAccountId = Base + 11, SalesRevenueAccountId = RevenueId,
            CostOfGoodsSoldAccountId = ExpenseId, GeneralExpenseAccountId = Base + 14,
            ExchangeGainAccountId = Base + 15, ExchangeLossAccountId = Base + 16,
            InventoryLossAccountId = Base + 17, CurrentYearProfitLossAccountId = Base + 18,
            RetainedEarningsAccountId = Base + 19
        });

        db.FiscalYears.Add(new FiscalYear
        {
            Id = 1, CompanyId = 1, Name = YearName, StartDate = new DateTime(2025, 1, 1), EndDate = EndDate,
            Status = FiscalYearStatus.Open, IsCurrent = true
        });
        if (periods == 1)
        {
            db.FiscalPeriods.Add(new FiscalPeriod
            {
                Id = 1, CompanyId = 1, FiscalYearId = 1, PeriodNumber = 1, Name = "FY25",
                StartDate = new DateTime(2025, 1, 1), EndDate = EndDate, Status = FiscalPeriodStatus.Open
            });
        }
        else
        {
            db.FiscalPeriods.Add(new FiscalPeriod
            {
                Id = 1, CompanyId = 1, FiscalYearId = 1, PeriodNumber = 1, Name = "H1",
                StartDate = new DateTime(2025, 1, 1), EndDate = new DateTime(2025, 6, 30), Status = FiscalPeriodStatus.Open
            });
            db.FiscalPeriods.Add(new FiscalPeriod
            {
                Id = 2, CompanyId = 1, FiscalYearId = 1, PeriodNumber = 2, Name = "H2",
                StartDate = new DateTime(2025, 7, 1), EndDate = EndDate, Status = FiscalPeriodStatus.Open
            });
        }

        db.FiscalYears.Add(new FiscalYear
        {
            Id = 2, CompanyId = 1, Name = "FY-2026", StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31), Status = FiscalYearStatus.Open, IsCurrent = false
        });
        db.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = 200, CompanyId = 1, FiscalYearId = 2, PeriodNumber = 1, Name = "FY26",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), Status = FiscalPeriodStatus.Open
        });

        var revPeriodId = periods == 1 ? 1 : 1;
        db.JournalEntries.Add(new JournalEntry
        {
            Id = 300, CompanyId = 1, FiscalYearId = 1, FiscalPeriodId = revPeriodId,
            JournalNumber = "REV", Status = JournalEntryStatus.Posted, PostedAt = DateTime.UtcNow,
            AccountingDate = new DateTime(2025, 3, 1), SourceModule = "Sale",
            Lines = [ new() { AccountId = CashId, LineNumber = 1, Debit = 300m },
                      new() { AccountId = RevenueId, LineNumber = 2, Credit = 300m } ]
        });
        db.JournalEntries.Add(new JournalEntry
        {
            Id = 301, CompanyId = 1, FiscalYearId = 1, FiscalPeriodId = revPeriodId,
            JournalNumber = "EXP", Status = JournalEntryStatus.Posted, PostedAt = DateTime.UtcNow,
            AccountingDate = new DateTime(2025, 3, 2), SourceModule = "Expense",
            Lines = [ new() { AccountId = ExpenseId, LineNumber = 1, Debit = 100m },
                      new() { AccountId = CashId, LineNumber = 2, Credit = 100m } ]
        });

        db.FiscalYearCloseRuns.Add(new FiscalYearCloseRun
        {
            Id = 8888, CompanyId = 1, FiscalYearId = 1, RunType = FiscalYearCloseRunType.Trial,
            Status = FiscalYearCloseRunStatus.Completed, StartedAt = DateTime.UtcNow.AddMinutes(-1),
            CompletedAt = DateTime.UtcNow.AddMinutes(-1), LastJournalEntryId = 999999, SnapshotHash = "H"
        });

        await db.SaveChangesAsync();

        if (!close)
            return;

        var posting = Posting(db);
        var checklist = new ClosingChecklistService(db, options, new AccountingReadinessService(db, options));
        var trialClose = new TrialCloseService(db, checklist, posting);
        var finalClose = new FinalCloseService(db, checklist, trialClose, posting);
        var result = await finalClose.CloseAsync(1, 1, userId: 2, confirmation: YearName);
        Assert.Equal(FinalCloseResultStatus.Succeeded, result.Status);
    }
}
