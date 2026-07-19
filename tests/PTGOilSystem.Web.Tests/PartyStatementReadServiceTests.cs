using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Models.PartyStatements;
using PTGOilSystem.Web.Services.PartyStatements;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public sealed class PartyStatementReadServiceTests
{
    [Theory]
    [InlineData(PartyStatementPartyType.Customer)]
    [InlineData(PartyStatementPartyType.Supplier)]
    [InlineData(PartyStatementPartyType.ServiceProvider)]
    [InlineData(PartyStatementPartyType.Sarraf)]
    [InlineData(PartyStatementPartyType.Employee)]
    [InlineData(PartyStatementPartyType.Partner)]
    [InlineData(PartyStatementPartyType.Driver)]
    [InlineData(PartyStatementPartyType.Company)]
    public void PolicyResolver_DefinesEverySupportedPartyType(PartyStatementPartyType partyType)
    {
        var policy = new PartyStatementPolicyResolver().Resolve(partyType);

        Assert.Equal(partyType, policy.PartyType);
        Assert.False(string.IsNullOrWhiteSpace(policy.StatementTitleFa));
        Assert.False(string.IsNullOrWhiteSpace(policy.DebitMeaningFa));
        Assert.False(string.IsNullOrWhiteSpace(policy.CreditMeaningFa));
        Assert.NotEqual(policy.BalanceMeaning(1m), policy.BalanceMeaning(-1m));
    }

    [Fact]
    public void CustomerPolicy_UsesBonexMapping_AndSupplierKeepsPayableMapping()
    {
        var resolver = new PartyStatementPolicyResolver();

        var customer = resolver.Resolve(PartyStatementPartyType.Customer);
        var supplier = resolver.Resolve(PartyStatementPartyType.Supplier);

        Assert.True(customer.ReverseLegacyLedgerSides);
        Assert.False(supplier.ReverseLegacyLedgerSides);
        Assert.Contains("قابل دریافت", customer.BalanceMeaning(-1m));
        Assert.Contains("بدهکار", supplier.BalanceMeaning(1m));
    }

    [Fact]
    public async Task CustomerStatement_CalculatesOpeningTotalsAndRunningBalance_WithBonexFormula()
    {
        await using var db = CreateDb();
        var customer = new Customer { Name = "Atlas Petroleum", Code = "CUST-1" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.LedgerEntries.AddRange(
            Entry(new DateTime(2025, 12, 31), LedgerSide.Debit, 10m, customer.Id, "CustomerReceipt", 1),
            Entry(new DateTime(2026, 1, 2), LedgerSide.Credit, 100m, customer.Id, "Sale", 2),
            Entry(new DateTime(2026, 1, 3), LedgerSide.Debit, 40m, customer.Id, "CustomerReceipt", 3));
        await db.SaveChangesAsync();

        var service = BuildService(db);
        var statement = await service.GetStatementAsync(
            new PartyRef(PartyStatementPartyType.Customer, customer.Id),
            new PartyStatementFilter
            {
                FromDate = new DateTime(2026, 1, 1),
                ToDate = new DateTime(2026, 1, 31),
                IncludeOperationalColumns = false
            });

        Assert.Equal(10m, statement.Summary.OpeningBalance);
        Assert.Equal(100m, statement.Summary.TotalDebit);
        Assert.Equal(40m, statement.Summary.TotalCredit);
        Assert.Equal(-50m, statement.Summary.ClosingBalance);
        Assert.Equal(statement.Summary.ClosingBalance, statement.Rows[^1].RunningBalance);
        Assert.True(statement.Rows[0].IsOpeningBalance);
        Assert.Equal("OB", statement.Rows[0].Reference);
    }

    [Fact]
    public async Task SupplierStatement_UsesCreditForLoading_AndDebitForPayment()
    {
        await using var db = CreateDb();
        var supplier = new Supplier { Name = "BONEX", Code = "SUP-1" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        db.LedgerEntries.AddRange(
            new LedgerEntry
            {
                EntryDate = new DateTime(2026, 3, 29),
                Side = LedgerSide.Credit,
                AmountUsd = 100m,
                Currency = "USD",
                SupplierId = supplier.Id,
                SourceType = "Loading",
                SourceId = 17,
                Description = "MARLIN HPGO"
            },
            new LedgerEntry
            {
                EntryDate = new DateTime(2026, 3, 30),
                Side = LedgerSide.Debit,
                AmountUsd = 35m,
                Currency = "RUB",
                SourceAmount = 2_800m,
                SourceCurrencyCode = "RUB",
                AppliedFxRateToUsd = 0.0125m,
                SupplierId = supplier.Id,
                SourceType = nameof(PaymentKind.SupplierPayment),
                SourceId = 18,
                Description = "پرداخت"
            });
        await db.SaveChangesAsync();

        var statement = await BuildService(db).GetStatementAsync(
            new PartyRef(PartyStatementPartyType.Supplier, supplier.Id),
            new PartyStatementFilter { IncludeOperationalColumns = false });

        Assert.Equal(35m, statement.Summary.TotalDebit);
        Assert.Equal(100m, statement.Summary.TotalCredit);
        Assert.Equal(65m, statement.Summary.ClosingBalance);
        Assert.True(statement.ColumnOptions.ShowRub);
        Assert.True(statement.ColumnOptions.ShowFxRate);
        Assert.Contains(statement.Rows, row => row.FxRateDisplay == "1 USD = 80 RUB");
    }

    [Fact]
    public async Task EmptyAndSingleRowStatements_KeepSummaryAndLastBalanceEqual()
    {
        await using var db = CreateDb();
        var customer = new Customer { Name = "Empty then single" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var service = BuildService(db);

        var empty = await service.GetStatementAsync(
            new PartyRef(PartyStatementPartyType.Customer, customer.Id),
            new PartyStatementFilter { IncludeOperationalColumns = false });
        Assert.Empty(empty.Rows);
        Assert.Equal(0m, empty.Summary.ClosingBalance);

        db.LedgerEntries.Add(Entry(new DateTime(2026, 2, 1), LedgerSide.Credit, 25m, customer.Id, "Sale", 9));
        await db.SaveChangesAsync();
        var single = await service.GetStatementAsync(
            new PartyRef(PartyStatementPartyType.Customer, customer.Id),
            new PartyStatementFilter { IncludeOperationalColumns = false });

        Assert.Single(single.Rows);
        Assert.Equal(-25m, single.Summary.ClosingBalance);
        Assert.Equal(single.Summary.ClosingBalance, single.Rows[^1].RunningBalance);
    }

    [Fact]
    public async Task SameDateRows_AreStableByPostingSequence_AndAdjustmentReversesBalance()
    {
        await using var db = CreateDb();
        var customer = new Customer { Name = "Ordered customer" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var date = new DateTime(2026, 4, 1);
        var later = Entry(date, LedgerSide.Debit, 20m, customer.Id, "Adjustment", 30);
        later.CreatedAtUtc = date.AddHours(2);
        var earlier = Entry(date, LedgerSide.Credit, 100m, customer.Id, "Sale", 20);
        earlier.CreatedAtUtc = date.AddHours(1);
        db.LedgerEntries.AddRange(later, earlier);
        await db.SaveChangesAsync();
        var statement = await BuildService(db).GetStatementAsync(
            new PartyRef(PartyStatementPartyType.Customer, customer.Id),
            new PartyStatementFilter { IncludeOperationalColumns = false });

        Assert.Equal(30, statement.Rows[0].SourceId);
        Assert.Equal(20m, statement.Rows[0].RunningBalance);
        Assert.Equal(-80m, statement.Rows[1].RunningBalance);
        Assert.Equal(statement.Summary.ClosingBalance, statement.Rows[^1].RunningBalance);
    }

    [Fact]
    public async Task CurrencyAndCompanyFilters_KeepHistoricalCurrencyAndIsolateCompany()
    {
        await using var db = CreateDb();
        var supplier = new Supplier { Name = "Scoped supplier" };
        var firstCompany = new Company { Code = "C1", Name = "Company 1" };
        var secondCompany = new Company { Code = "C2", Name = "Company 2" };
        db.AddRange(supplier, firstCompany, secondCompany);
        await db.SaveChangesAsync();
        var firstContract = new Contract { ContractNumber = "P-1", ContractType = ContractType.Purchase, CompanyId = firstCompany.Id, SupplierId = supplier.Id };
        var secondContract = new Contract { ContractNumber = "P-2", ContractType = ContractType.Purchase, CompanyId = secondCompany.Id, SupplierId = supplier.Id };
        db.AddRange(firstContract, secondContract);
        await db.SaveChangesAsync();
        db.LedgerEntries.AddRange(
            SupplierEntry(firstContract.Id, supplier.Id, 50m, "RUB", 4_000m, 0.0125m, 1),
            SupplierEntry(secondContract.Id, supplier.Id, 90m, "AED", 330m, null, 2));
        await db.SaveChangesAsync();

        var statement = await BuildService(db).GetStatementAsync(
            new PartyRef(PartyStatementPartyType.Supplier, supplier.Id, firstCompany.Id),
            new PartyStatementFilter { CurrencyCode = "rub", IncludeOperationalColumns = false });

        Assert.Single(statement.Rows);
        Assert.Equal(50m, statement.Summary.ClosingBalance);
        Assert.True(statement.ColumnOptions.ShowRub);
        Assert.False(statement.ColumnOptions.ShowAed);
        Assert.Equal("1 USD = 80 RUB", statement.Rows[0].FxRateDisplay);
    }

    [Fact]
    public async Task MissingHistoricalFx_IsNullAndDisplayedAsMissing_NotZero()
    {
        await using var db = CreateDb();
        var supplier = new Supplier { Name = "AED supplier" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        db.LedgerEntries.Add(SupplierEntry(null, supplier.Id, 10m, "AED", 36.8m, null, 1));
        await db.SaveChangesAsync();

        var statement = await BuildService(db).GetStatementAsync(
            new PartyRef(PartyStatementPartyType.Supplier, supplier.Id),
            new PartyStatementFilter { IncludeOperationalColumns = false });

        Assert.True(statement.ColumnOptions.ShowAed);
        Assert.True(statement.ColumnOptions.ShowFxRate);
        Assert.Null(statement.Rows[0].FxRate);
        Assert.Null(statement.Rows[0].FxRateDisplay);
    }

    [Fact]
    public async Task EmployeePolicy_AccrualIncreasesAndPaymentDecreasesPayable()
    {
        await using var db = CreateDb();
        var employee = new Employee { EmployeeCode = "EMP-1", FullName = "Employee One" };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        db.EmployeeSalaryTransactions.AddRange(
            new EmployeeSalaryTransaction { EmployeeId = employee.Id, TransactionDate = new DateTime(2026, 6, 1), TransactionType = EmployeeSalaryTransactionType.SalaryAccrual, Amount = 100m, AmountUsd = 100m, Currency = "USD" },
            new EmployeeSalaryTransaction { EmployeeId = employee.Id, TransactionDate = new DateTime(2026, 6, 2), TransactionType = EmployeeSalaryTransactionType.SalaryPayment, Amount = 40m, AmountUsd = 40m, Currency = "USD" },
            new EmployeeSalaryTransaction { EmployeeId = employee.Id, TransactionDate = new DateTime(2026, 6, 3), TransactionType = EmployeeSalaryTransactionType.Bonus, Amount = 500m, AmountUsd = 500m, Currency = "USD", IsCancelled = true });
        await db.SaveChangesAsync();

        var statement = await BuildService(db).GetStatementAsync(
            new PartyRef(PartyStatementPartyType.Employee, employee.Id),
            new PartyStatementFilter { IncludeOperationalColumns = false });

        Assert.Equal(40m, statement.Summary.TotalDebit);
        Assert.Equal(100m, statement.Summary.TotalCredit);
        Assert.Equal(60m, statement.Summary.ClosingBalance);
        Assert.Contains("کارمند", statement.Summary.ClosingBalanceMeaning);
    }

    [Fact]
    public async Task SarrafStatement_CombinesPostedDirectionsAndViaPayableWithoutDraftRows()
    {
        await using var db = CreateDb();
        var sarraf = new Sarraf { Name = "Exchange House" };
        db.Sarrafs.Add(sarraf);
        await db.SaveChangesAsync();
        db.SarrafSettlements.AddRange(
            new SarrafSettlement { SarrafId = sarraf.Id, SettlementDate = new DateTime(2026, 7, 1), Direction = SarrafSettlementDirection.Out, Status = SarrafSettlementStatus.Posted, SarrafCurrency = "USD", SarrafChargedAmount = 100m, SarrafChargedAmountUsd = 100m },
            new SarrafSettlement { SarrafId = sarraf.Id, SettlementDate = new DateTime(2026, 7, 2), Direction = SarrafSettlementDirection.In, Status = SarrafSettlementStatus.Posted, SarrafCurrency = "USD", SarrafChargedAmount = 20m, SarrafChargedAmountUsd = 20m },
            new SarrafSettlement { SarrafId = sarraf.Id, SettlementDate = new DateTime(2026, 7, 3), Direction = SarrafSettlementDirection.Out, Status = SarrafSettlementStatus.Draft, SarrafCurrency = "USD", SarrafChargedAmount = 999m, SarrafChargedAmountUsd = 999m });
        db.LedgerEntries.Add(new LedgerEntry { EntryDate = new DateTime(2026, 7, 2), Side = LedgerSide.Credit, AmountUsd = 10m, Currency = "USD", SourceType = "SupplierViaSarrafPayable", SourceId = sarraf.Id, Description = "Via sarraf" });
        await db.SaveChangesAsync();

        var statement = await BuildService(db).GetStatementAsync(
            new PartyRef(PartyStatementPartyType.Sarraf, sarraf.Id),
            new PartyStatementFilter { IncludeOperationalColumns = false });

        Assert.Equal(20m, statement.Summary.TotalDebit);
        Assert.Equal(110m, statement.Summary.TotalCredit);
        Assert.Equal(90m, statement.Summary.ClosingBalance);
        Assert.Equal(3, statement.Rows.Count);
    }

    [Fact]
    public async Task PartnerStatement_AppliesContractShareOnce()
    {
        await using var db = CreateDb();
        var partner = new Partner { Code = "PAR-1", Name = "Partner One" };
        var company = new Company { Code = "CO", Name = "Company" };
        db.AddRange(partner, company);
        await db.SaveChangesAsync();
        var contract = new Contract { ContractNumber = "PC-1", ContractType = ContractType.Purchase, CompanyId = company.Id };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        db.ContractPartners.Add(new ContractPartner { ContractId = contract.Id, PartnerId = partner.Id, SharePercent = 25m });
        db.LedgerEntries.Add(new LedgerEntry { EntryDate = new DateTime(2026, 8, 1), Side = LedgerSide.Credit, AmountUsd = 200m, Currency = "USD", ContractId = contract.Id, SourceType = "Loading", SourceId = 1, Description = "Purchase share" });
        await db.SaveChangesAsync();

        var statement = await BuildService(db).GetStatementAsync(
            new PartyRef(PartyStatementPartyType.Partner, partner.Id),
            new PartyStatementFilter { IncludeOperationalColumns = false });

        Assert.Equal(50m, statement.Summary.TotalCredit);
        Assert.Equal(50m, statement.Summary.ClosingBalance);
        Assert.Single(statement.Rows);
    }

    [Fact]
    public async Task CsvExport_UsesEveryServiceRowWithoutUiPagination()
    {
        var rows = Enumerable.Range(1, 27)
            .Select(i => new PartyStatementRow
            {
                Sequence = i,
                Date = new DateTime(2026, 1, 1).AddDays(i),
                Reference = $"REF-{i}",
                Description = $"Row {i}",
                CreditBase = 1m,
                RunningBalance = i,
                SourceType = "Test",
                SourceId = i
            })
            .ToList();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var controller = new PartyStatementsController(new StubStatementService(BuildResult(rows)))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var action = await controller.Csv(
            PartyStatementPartyType.Customer,
            1,
            new PartyStatementFilter { IncludeOperationalColumns = false });

        await action.ExecuteResultAsync(new ActionContext(httpContext, new(), new()));
        httpContext.Response.Body.Position = 0;
        var csv = await new StreamReader(httpContext.Response.Body, System.Text.Encoding.UTF8).ReadToEndAsync();
        Assert.Contains("REF-27", csv);
        Assert.Equal(28, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void OfficialView_HasRequiredDocumentSections_AndNoFinancialArithmetic()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "src", "PTGOilSystem.Web", "Views", "PartyStatements", "Document.cshtml"));
        var css = File.ReadAllText(Path.Combine(root, "src", "PTGOilSystem.Web", "wwwroot", "css", "ptg", "62-party-statement.css"));

        Assert.Contains("statement-brand-header", view);
        Assert.Contains("statement-info-grid", view);
        Assert.Contains("statement-summary", view);
        Assert.Contains("statement-table", view);
        Assert.Contains("statement-note", view);
        Assert.Contains("statement-authorization", view);
        Assert.Contains("statement-footer", view);
        Assert.DoesNotContain("RunningBalance +=", view, StringComparison.Ordinal);
        Assert.DoesNotContain("TotalCredit -", view, StringComparison.Ordinal);
        Assert.Contains("@media print", css);
        Assert.Contains("@page statement-wide", css);
    }

    private static LedgerEntry Entry(
        DateTime date,
        LedgerSide side,
        decimal amount,
        int customerId,
        string sourceType,
        int sourceId)
        => new()
        {
            EntryDate = date,
            Side = side,
            AmountUsd = amount,
            Currency = "USD",
            CustomerId = customerId,
            SourceType = sourceType,
            SourceId = sourceId,
            Description = sourceType
        };

    private static LedgerEntry SupplierEntry(
        int? contractId,
        int supplierId,
        decimal amountUsd,
        string currency,
        decimal originalAmount,
        decimal? fxRateToUsd,
        int sourceId)
        => new()
        {
            EntryDate = new DateTime(2026, 5, sourceId),
            Side = LedgerSide.Credit,
            AmountUsd = amountUsd,
            Currency = currency,
            SourceAmount = originalAmount,
            SourceCurrencyCode = currency,
            AppliedFxRateToUsd = fxRateToUsd,
            SupplierId = supplierId,
            ContractId = contractId,
            SourceType = "Loading",
            SourceId = sourceId,
            Description = "Loading"
        };

    private static PartyStatementResult BuildResult(IReadOnlyList<PartyStatementRow> rows)
    {
        var policy = new PartyStatementPolicyResolver().Resolve(PartyStatementPartyType.Customer);
        return new PartyStatementResult
        {
            Party = new PartyRef(PartyStatementPartyType.Customer, 1),
            Policy = policy,
            CompanyInfo = new PartyStatementCompanyInfo(),
            PartyInfo = new PartyStatementPartyInfo { Id = 1, Name = "Customer" },
            DocumentInfo = new PartyStatementDocumentInfo(),
            Summary = new PartyStatementSummary { TotalCredit = rows.Count, ClosingBalance = rows.Count, BaseCurrencyCode = "USD" },
            ColumnOptions = new PartyStatementColumnOptions(),
            Rows = rows,
            Authorization = new PartyStatementAuthorization()
        };
    }

    private sealed class StubStatementService(PartyStatementResult result) : IPartyStatementReadService
    {
        public Task<PartyStatementResult> GetStatementAsync(
            PartyRef party,
            PartyStatementFilter filter,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private static PartyStatementReadService BuildService(ApplicationDbContext db)
        => new(
            db,
            new PartyStatementPolicyResolver(),
            Options.Create(new PartyStatementOptions()));

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ptg-oil-system.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
