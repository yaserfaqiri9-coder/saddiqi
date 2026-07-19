using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services.Accounting;
using Xunit;

namespace PTGOilSystem.Web.Tests;

/// <summary>
/// مرحله ۱۴ — Final Close اتمیک: بستنِ P&L به Equity، انتقال به Retained Earnings، HardLockِ
/// دوره‌ها و بستنِ سال؛ همه در یک Transaction و غیرقابل‌تکرارِ ناخواسته.
/// </summary>
public class FinalCloseServiceTests
{
    private const int Base = 100;
    private const int CashId = Base;
    private const int RevenueId = Base + 12;
    private const int ExpenseId = Base + 13;
    private const int CyeId = Base + 18;
    private const int ReId = Base + 19;
    private const string YearName = "FY-2025";
    private static readonly DateTime EndDate = new(2025, 12, 31);

    [Fact]
    public async Task Close_Posts_Balanced_Closing_Entries_And_Transfers_Profit_To_Retained_Earnings()
    {
        await using var db = NewDb();
        SeedClosable(db, revenue: 300m, expense: 100m);
        await db.SaveChangesAsync();

        var result = await Svc(db).CloseAsync(1, 1, userId: 4, confirmation: YearName);
        Assert.Equal(FinalCloseResultStatus.Succeeded, result.Status);

        var pnl = db.JournalEntries.Include(j => j.Lines)
            .Single(j => j.SourceEventId == "FiscalYearClose:1:ProfitAndLoss:0");
        Assert.True(pnl.IsClosing);
        Assert.Equal(EndDate, pnl.AccountingDate);
        Assert.Equal(pnl.Lines.Sum(l => l.Debit), pnl.Lines.Sum(l => l.Credit));
        Assert.Contains(pnl.Lines, l => l.AccountId == RevenueId && l.Debit == 300m);
        Assert.Contains(pnl.Lines, l => l.AccountId == ExpenseId && l.Credit == 100m);
        Assert.Contains(pnl.Lines, l => l.AccountId == CyeId && l.Credit == 200m);

        var re = db.JournalEntries.Include(j => j.Lines)
            .Single(j => j.SourceEventId == "FiscalYearClose:1:RetainedEarnings:0");
        Assert.Contains(re.Lines, l => l.AccountId == CyeId && l.Debit == 200m);
        Assert.Contains(re.Lines, l => l.AccountId == ReId && l.Credit == 200m);
    }

    [Fact]
    public async Task Close_Zeros_Revenue_And_Expense_And_Leaves_Retained_Earnings_At_Net_Profit()
    {
        await using var db = NewDb();
        SeedClosable(db, revenue: 300m, expense: 100m);
        await db.SaveChangesAsync();

        await Svc(db).CloseAsync(1, 1, userId: 1, confirmation: YearName);

        Assert.Equal(0m, NetOf(db, RevenueId));
        Assert.Equal(0m, NetOf(db, ExpenseId));
        Assert.Equal(0m, NetOf(db, CyeId));
        // Retained Earnings مانده بستانکار ۲۰۰ = سود.
        Assert.Equal(-200m, NetOf(db, ReId));
    }

    [Fact]
    public async Task Loss_Transfers_In_The_Opposite_Direction()
    {
        await using var db = NewDb();
        SeedClosable(db, revenue: 100m, expense: 300m); // زیان ۲۰۰
        await db.SaveChangesAsync();

        await Svc(db).CloseAsync(1, 1, userId: 1, confirmation: YearName);

        var re = db.JournalEntries.Include(j => j.Lines)
            .Single(j => j.SourceEventId == "FiscalYearClose:1:RetainedEarnings:0");
        // زیان → Retained Earnings بدهکار.
        Assert.Contains(re.Lines, l => l.AccountId == ReId && l.Debit == 200m);
        Assert.Contains(re.Lines, l => l.AccountId == CyeId && l.Credit == 200m);
        Assert.Equal(200m, NetOf(db, ReId)); // مانده بدهکار
    }

    [Fact]
    public async Task Close_HardLocks_All_Periods_And_Closes_Year_And_Advances_Current()
    {
        await using var db = NewDb();
        SeedClosable(db, revenue: 300m, expense: 100m);
        await db.SaveChangesAsync();

        await Svc(db).CloseAsync(1, 1, userId: 8, confirmation: YearName);

        var year = db.FiscalYears.Single(y => y.Id == 1);
        Assert.Equal(FiscalYearStatus.Closed, year.Status);
        Assert.NotNull(year.ClosedAt);
        Assert.Equal(8, year.ClosedByUserId);
        Assert.False(year.IsCurrent);
        Assert.All(db.FiscalPeriods.Where(p => p.FiscalYearId == 1),
            p => Assert.Equal(FiscalPeriodStatus.HardLocked, p.Status));

        var next = db.FiscalYears.Single(y => y.Id == 2);
        Assert.True(next.IsCurrent);
        Assert.Equal(1, db.FiscalYears.Count(y => y.CompanyId == 1 && y.IsCurrent));

        var run = db.FiscalYearCloseRuns.Single(r => r.RunType == FiscalYearCloseRunType.Final);
        Assert.Equal(1, run.FiscalYearId);
    }

    [Fact]
    public async Task No_Opening_Journal_Is_Created_And_Pnl_Is_Not_Carried_To_Next_Year()
    {
        await using var db = NewDb();
        SeedClosable(db, revenue: 300m, expense: 100m);
        await db.SaveChangesAsync();

        await Svc(db).CloseAsync(1, 1, userId: 1, confirmation: YearName);

        Assert.False(db.JournalEntries.Any(j => j.SourceEventId != null && j.SourceEventId.Contains("OpeningBalance")));
        // سندهای بستن در سالِ بسته (FY 1) و به تاریخ EndDate‌اند، نه در سال بعد.
        Assert.All(db.JournalEntries.Where(j => j.SourceModule == "FiscalYearClose" && j.IsClosing),
            j => { Assert.Equal(1, j.FiscalYearId); Assert.Equal(EndDate, j.AccountingDate); });
    }

    [Fact]
    public async Task Invalid_Confirmation_Is_Rejected()
    {
        await using var db = NewDb();
        SeedClosable(db, revenue: 300m, expense: 100m);
        await db.SaveChangesAsync();

        var result = await Svc(db).CloseAsync(1, 1, userId: 1, confirmation: "WRONG");
        Assert.Equal(FinalCloseResultStatus.Blocked, result.Status);
        Assert.Equal(FinalCloseReasons.ConfirmationInvalid, result.FailureCode);
        Assert.Equal(FiscalYearStatus.Open, db.FiscalYears.Single(y => y.Id == 1).Status);
    }

    [Fact]
    public async Task Missing_Trial_Close_Blocks()
    {
        await using var db = NewDb();
        SeedClosable(db, revenue: 300m, expense: 100m, includeTrialRun: false);
        await db.SaveChangesAsync();

        var precheck = await Svc(db).PrecheckAsync(1, 1);
        Assert.False(precheck!.CanClose);
        Assert.Contains(FinalCloseReasons.TrialCloseMissing, precheck.Blockers);
    }

    [Fact]
    public async Task Stale_Trial_Close_Blocks()
    {
        await using var db = NewDb();
        SeedClosable(db, revenue: 300m, expense: 100m, trialCutoff: 1);
        // سندِ عملیاتیِ جدید بعد از Snapshot تریال.
        db.JournalEntries.Add(new JournalEntry
        {
            Id = 7000, CompanyId = 1, FiscalYearId = 1, FiscalPeriodId = 1,
            JournalNumber = "LATE", Status = JournalEntryStatus.Posted, PostedAt = DateTime.UtcNow,
            AccountingDate = new DateTime(2025, 7, 1), SourceModule = "Purchase",
            Lines = [ new() { AccountId = CashId, LineNumber = 1, Debit = 5m },
                      new() { AccountId = RevenueId, LineNumber = 2, Credit = 5m } ]
        });
        await db.SaveChangesAsync();

        var precheck = await Svc(db).PrecheckAsync(1, 1);
        Assert.Contains(FinalCloseReasons.TrialCloseStale, precheck!.Blockers);
    }

    [Fact]
    public async Task Checklist_Blocker_Blocks_Close()
    {
        await using var db = NewDb();
        SeedClosable(db, revenue: 300m, expense: 100m);
        db.JournalEntries.Add(new JournalEntry
        {
            Id = 6000, CompanyId = 1, FiscalYearId = 1, FiscalPeriodId = 1,
            JournalNumber = "BAD", Status = JournalEntryStatus.Posted, PostedAt = DateTime.UtcNow,
            AccountingDate = new DateTime(2025, 6, 1), SourceModule = "Purchase",
            Lines = [ new() { AccountId = CashId, LineNumber = 1, Debit = 10m },
                      new() { AccountId = RevenueId, LineNumber = 2, Credit = 9m } ]
        });
        await db.SaveChangesAsync();

        var result = await Svc(db).CloseAsync(1, 1, userId: 1, confirmation: YearName);
        Assert.Equal(FinalCloseResultStatus.Blocked, result.Status);
    }

    [Fact]
    public async Task Missing_Next_Year_Blocks()
    {
        await using var db = NewDb();
        SeedClosable(db, revenue: 300m, expense: 100m, includeNextYear: false);
        await db.SaveChangesAsync();

        var precheck = await Svc(db).PrecheckAsync(1, 1);
        Assert.Contains(FinalCloseReasons.NextYearMissing, precheck!.Blockers);
    }

    [Fact]
    public async Task Second_Close_Is_Idempotent_AlreadyClosed()
    {
        await using var db = NewDb();
        SeedClosable(db, revenue: 300m, expense: 100m);
        await db.SaveChangesAsync();

        await Svc(db).CloseAsync(1, 1, userId: 1, confirmation: YearName);
        var journalsAfterFirst = db.JournalEntries.Count();

        var second = await Svc(db).CloseAsync(1, 1, userId: 1, confirmation: YearName);
        Assert.Equal(FinalCloseResultStatus.AlreadyClosed, second.Status);
        Assert.Equal(journalsAfterFirst, db.JournalEntries.Count());
    }

    [Fact]
    public async Task Company_Isolation_Unknown_Year_Returns_Null()
    {
        await using var db = NewDb();
        SeedClosable(db, revenue: 300m, expense: 100m);
        await db.SaveChangesAsync();
        Assert.Null(await Svc(db).PrecheckAsync(companyId: 999, fiscalYearId: 1));
    }

    // ---- helpers ----

    private static ApplicationDbContext NewDb()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IFinalCloseService Svc(ApplicationDbContext db)
    {
        var options = Options.Create(new AccountingOptions { Enabled = true });
        var posting = new AccountingPostingService(db, new PeriodGuard(db, new FiscalCalendarService(db)), options, new SystemCompanyProvider(db));
        var checklist = new ClosingChecklistService(db, options, new AccountingReadinessService(db, options));
        var trialClose = new TrialCloseService(db, checklist, posting);
        return new FinalCloseService(db, checklist, trialClose, posting);
    }

    private static decimal NetOf(ApplicationDbContext db, int accountId)
        => db.JournalEntryLines.Where(l => l.AccountId == accountId
                && l.JournalEntry!.Status == JournalEntryStatus.Posted)
            .Sum(l => l.Debit - l.Credit);

    private static void SeedClosable(ApplicationDbContext db, decimal revenue, decimal expense,
        bool includeTrialRun = true, bool includeNextYear = true, int trialCutoff = 999999)
    {
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
            InventoryLossAccountId = Base + 17, CurrentYearProfitLossAccountId = CyeId,
            RetainedEarningsAccountId = ReId
        });

        db.FiscalYears.Add(new FiscalYear
        {
            Id = 1, CompanyId = 1, Name = YearName, StartDate = new DateTime(2025, 1, 1), EndDate = EndDate,
            Status = FiscalYearStatus.Open, IsCurrent = true
        });
        db.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = 1, CompanyId = 1, FiscalYearId = 1, PeriodNumber = 1, Name = "FY25",
            StartDate = new DateTime(2025, 1, 1), EndDate = EndDate, Status = FiscalPeriodStatus.Open
        });

        if (includeNextYear)
        {
            db.FiscalYears.Add(new FiscalYear
            {
                Id = 2, CompanyId = 1, Name = "FY-2026", StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31), Status = FiscalYearStatus.Open, IsCurrent = false
            });
            db.FiscalPeriods.Add(new FiscalPeriod
            {
                Id = 2, CompanyId = 1, FiscalYearId = 2, PeriodNumber = 1, Name = "FY26",
                StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), Status = FiscalPeriodStatus.Open
            });
        }

        // فعالیتِ درآمد و هزینه.
        db.JournalEntries.Add(new JournalEntry
        {
            Id = 100, CompanyId = 1, FiscalYearId = 1, FiscalPeriodId = 1,
            JournalNumber = "REV", Status = JournalEntryStatus.Posted, PostedAt = DateTime.UtcNow,
            AccountingDate = new DateTime(2025, 6, 1), SourceModule = "Sale",
            Lines = [ new() { AccountId = CashId, LineNumber = 1, Debit = revenue },
                      new() { AccountId = RevenueId, LineNumber = 2, Credit = revenue } ]
        });
        db.JournalEntries.Add(new JournalEntry
        {
            Id = 101, CompanyId = 1, FiscalYearId = 1, FiscalPeriodId = 1,
            JournalNumber = "EXP", Status = JournalEntryStatus.Posted, PostedAt = DateTime.UtcNow,
            AccountingDate = new DateTime(2025, 6, 2), SourceModule = "Expense",
            Lines = [ new() { AccountId = ExpenseId, LineNumber = 1, Debit = expense },
                      new() { AccountId = CashId, LineNumber = 2, Credit = expense } ]
        });

        if (includeTrialRun)
        {
            db.FiscalYearCloseRuns.Add(new FiscalYearCloseRun
            {
                Id = 8888, CompanyId = 1, FiscalYearId = 1, RunType = FiscalYearCloseRunType.Trial,
                Status = FiscalYearCloseRunStatus.Completed, StartedAt = DateTime.UtcNow.AddMinutes(-1),
                CompletedAt = DateTime.UtcNow.AddMinutes(-1), LastJournalEntryId = trialCutoff,
                SnapshotHash = "HASH"
            });
        }
    }
}
