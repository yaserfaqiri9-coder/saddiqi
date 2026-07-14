using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Models.Expenses;
using PTGOilSystem.Web.Services;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class ExpenseRuleControllerTests
{
    [Fact]
    public async Task Create_Post_Persists_Rule_And_Audit()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.ExpenseTypes.Add(new ExpenseType { Id = 1, Code = "TRN", Name = "Trucking" });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.Create(new ExpenseRuleEditViewModel
        {
            Name = "Trucking Flat",
            ExpenseTypeId = 1,
            CalculationKind = "Flat",
            Amount = 250m,
            Currency = "USD",
            IsActive = true
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);

        var rule = await db.ExpenseRules.SingleAsync();
        Assert.Equal("Trucking Flat", rule.Name);
        Assert.Equal("Flat", rule.CalculationKind);
        Assert.Equal(250m, rule.Amount);

        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal(nameof(ExpenseRule), audit.EntityName);
        Assert.Equal("Insert", audit.Action);
    }

    [Fact]
    public async Task Create_Post_Returns_View_When_Currency_Is_Not_Active_Master_Data()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.Currencies.Add(new Currency { Id = 1, Code = "USD", Name = "US Dollar", Symbol = "$", IsActive = true });
        db.ExpenseTypes.Add(new ExpenseType { Id = 1, Code = "TRN", Name = "Trucking" });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.Create(new ExpenseRuleEditViewModel
        {
            Name = "Bad Currency Rule",
            ExpenseTypeId = 1,
            CalculationKind = "Flat",
            Amount = 250m,
            Currency = "EUR",
            IsActive = true
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<ExpenseRuleEditViewModel>(view.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[nameof(ExpenseRuleEditViewModel.Currency)]!.Errors, e => e.ErrorMessage.Contains("ارز"));
        Assert.Empty(await db.ExpenseRules.ToListAsync());
    }

    [Fact]
    public async Task Edit_Post_Updates_Rule_And_Audits_Change()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.ExpenseTypes.Add(new ExpenseType { Id = 1, Code = "TRN", Name = "Trucking" });
        db.ExpenseRules.Add(new ExpenseRule
        {
            Id = 1,
            Name = "Initial Rule",
            ExpenseTypeId = 1,
            CalculationKind = "Flat",
            Amount = 100m,
            Currency = "USD",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.Edit(1, new ExpenseRuleEditViewModel
        {
            Id = 1,
            Name = "Updated Rule",
            ExpenseTypeId = 1,
            CalculationKind = "Percent",
            Amount = 3.5m,
            Currency = "USD",
            IsActive = false
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);

        var rule = await db.ExpenseRules.SingleAsync();
        Assert.Equal("Updated Rule", rule.Name);
        Assert.Equal("Percent", rule.CalculationKind);
        Assert.Equal(3.5m, rule.Amount);
        Assert.False(rule.IsActive);

        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal(nameof(ExpenseRule), audit.EntityName);
        Assert.Equal("Update", audit.Action);
        Assert.Contains("Amount", audit.Diff ?? string.Empty);
    }

    [Fact]
    public async Task GenerateExpense_Post_Creates_Expense_Transaction_And_Ledger()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.ExpenseTypes.Add(new ExpenseType { Id = 1, Code = "STR", Name = "Storage" });
        db.Companies.Add(new Company { Id = 1, Code = "PTG", Name = "Petro Trade Group" });
        db.Contracts.Add(new Contract
        {
            Id = 1,
            ContractNumber = "CTR-EXP-1",
            ContractType = ContractType.Purchase,
            ProductId = 1,
            CompanyId = 1,
            ContractDate = new DateTime(2026, 4, 23),
            QuantityMt = 500m
        });
        db.Products.Add(new Product { Id = 1, Code = "GO", Name = "Gas Oil" });
        db.ExpenseRules.Add(new ExpenseRule
        {
            Id = 1,
            Name = "Storage Per MT",
            ExpenseTypeId = 1,
            CalculationKind = "PerMt",
            Amount = 12.5m,
            Currency = "USD",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.GenerateExpense(1, new ExpenseRuleGenerateExpenseViewModel
        {
            ExpenseDate = new DateTime(2026, 4, 23),
            ContractId = 1,
            QuantityMt = 10m,
            Description = "Auto from rule"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);

        var expense = await db.ExpenseTransactions.SingleAsync();
        Assert.Equal(1, expense.ExpenseRuleId);
        Assert.Equal(1, expense.ContractId);
        Assert.Equal(125m, expense.AmountUsd);
        Assert.Contains("Storage Per MT", expense.Description ?? string.Empty);

        var ledger = await db.LedgerEntries.SingleAsync();
        Assert.Equal("Expense", ledger.SourceType);
        Assert.Equal(expense.Id, ledger.SourceId);
        Assert.Equal(125m, ledger.AmountUsd);
    }

    [Fact]
    public async Task GenerateExpense_Post_For_NonUsd_Rule_Converts_And_Persists_Source_Amount()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.Currencies.AddRange(
            new Currency { Id = 1, Code = "USD", Name = "US Dollar", Symbol = "$", IsActive = true },
            new Currency { Id = 2, Code = "EUR", Name = "Euro", Symbol = "EUR", IsActive = true });
        db.DailyFxRates.Add(new DailyFxRate
        {
            Id = 1,
            BaseCurrency = "EUR",
            QuoteCurrency = "USD",
            RateDate = new DateTime(2026, 4, 23),
            Rate = 1.2m,
            Source = "Test FX"
        });
        db.ExpenseTypes.Add(new ExpenseType { Id = 1, Code = "STR", Name = "Storage" });
        db.Companies.Add(new Company { Id = 1, Code = "PTG", Name = "Petro Trade Group" });
        db.Products.Add(new Product { Id = 1, Code = "GO", Name = "Gas Oil" });
        db.Contracts.Add(new Contract
        {
            Id = 1,
            ContractNumber = "CTR-EXP-2",
            ContractType = ContractType.Purchase,
            ProductId = 1,
            CompanyId = 1,
            ContractDate = new DateTime(2026, 4, 23),
            QuantityMt = 500m
        });
        db.ExpenseRules.Add(new ExpenseRule
        {
            Id = 1,
            Name = "Storage EUR",
            ExpenseTypeId = 1,
            CalculationKind = "PerMt",
            Amount = 12.5m,
            Currency = "EUR",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.GenerateExpense(1, new ExpenseRuleGenerateExpenseViewModel
        {
            ExpenseDate = new DateTime(2026, 4, 23),
            ContractId = 1,
            QuantityMt = 10m,
            Description = "Auto from EUR rule"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);

        var expense = await db.ExpenseTransactions.SingleAsync();
        Assert.Equal(125m, expense.Amount);
        Assert.Equal("EUR", expense.Currency);
        Assert.Equal(1.2m, expense.AppliedFxRateToUsd);
        Assert.Equal(150m, expense.AmountUsd);

        var ledger = await db.LedgerEntries.SingleAsync();
        Assert.Equal(125m, ledger.SourceAmount);
        Assert.Equal("EUR", ledger.SourceCurrencyCode);
        Assert.Equal(1.2m, ledger.AppliedFxRateToUsd);
        Assert.Equal(150m, ledger.AmountUsd);
    }

    private static ExpenseRulesController BuildController(ApplicationDbContext db)
    {
        var audit = new AuditService(db);
        var engine = new ExpenseRuleEngine(db, audit);

        return new ExpenseRulesController(
            db,
            engine,
            audit,
            NullLogger<ExpenseRulesController>.Instance)
        {
            TempData = BuildTempData()
        };
    }

    private static TempDataDictionary BuildTempData()
        => new(new DefaultHttpContext(), new InMemoryTempDataProvider());

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        private IDictionary<string, object> _data = new Dictionary<string, object>();

        public IDictionary<string, object> LoadTempData(HttpContext context) => _data;

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
            => _data = new Dictionary<string, object>(values);
    }
}
