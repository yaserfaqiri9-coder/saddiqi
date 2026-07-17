using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Accounting;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services.Accounting;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class ChartOfAccountsTests
{
    [Fact]
    public async Task BuildAsync_EmptyDatabase_ReturnsEmptyStateModel()
    {
        await using var db = NewDb();
        var model = await new ChartOfAccountsReadService(db).BuildAsync(null, null, 1);

        Assert.Empty(model.Companies);
        Assert.Null(model.SelectedCompanyId);
        Assert.Empty(model.Items);
        Assert.Equal(0, model.TotalCount);
        Assert.Equal(1, model.CurrentPage);
        Assert.Equal(1, model.PageCount);
    }

    [Fact]
    public async Task BuildAsync_ReturnsOnlySelectedCompany_AndProjectsParentDetails()
    {
        await using var db = NewDb();
        var parent = NewAccount(1, 10, "1100", "Cash Control");
        db.Accounts.AddRange(
            parent,
            NewAccount(2, 10, "1110", "Petty Cash", parent),
            NewAccount(3, 20, "1100", "Other Company Cash"));
        await db.SaveChangesAsync();

        var model = await new ChartOfAccountsReadService(db)
            .BuildAsync(companyId: 10, search: "Petty", page: 1);

        var row = Assert.Single(model.Items);
        Assert.Equal(10, row.CompanyId);
        Assert.Equal("1110", row.Code);
        Assert.Equal(1, row.ParentAccountId);
        Assert.Equal("1100", row.ParentCode);
        Assert.Equal("Cash Control", row.ParentName);
        Assert.DoesNotContain(model.Items, item => item.CompanyId == 20);
    }

    [Fact]
    public async Task BuildAsync_AppliesServerPageSize()
    {
        await using var db = NewDb();
        db.Accounts.AddRange(Enumerable.Range(1, 25)
            .Select(id => NewAccount(id, 10, $"{id:0000}", $"Account {id:00}")));
        await db.SaveChangesAsync();

        var service = new ChartOfAccountsReadService(db);
        var first = await service.BuildAsync(10, null, 1);
        var second = await service.BuildAsync(10, null, 2);

        Assert.Equal(20, first.Items.Count);
        Assert.Equal(5, second.Items.Count);
        Assert.Equal(25, first.TotalCount);
        Assert.Equal(2, first.PageCount);
        Assert.Equal(2, second.CurrentPage);
    }

    [Fact]
    public async Task Controller_Index_ReturnsDedicatedViewModel()
    {
        await using var db = NewDb();
        db.Accounts.Add(NewAccount(1, 10, "1100", "Cash Control"));
        await db.SaveChangesAsync();

        var controller = new ChartOfAccountsController(new ChartOfAccountsReadService(db));
        var result = await controller.Index(10, null, 1);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ChartOfAccountsIndexViewModel>(view.Model);
        Assert.Equal(10, model.SelectedCompanyId);
        Assert.Single(model.Items);
    }

    [Fact]
    public void Implementation_UsesAccountingAccountsProjection_AndNeverLegacyLedgerEntries()
    {
        var service = ReadRepoFile("src/PTGOilSystem.Web/Services/Accounting/ChartOfAccountsReadService.cs");
        var view = ReadRepoFile("src/PTGOilSystem.Web/Views/ChartOfAccounts/Index.cshtml");

        Assert.Contains("db.Accounts.AsNoTracking()", service);
        Assert.Contains(".Select(account => new ChartOfAccountsRowViewModel", service);
        Assert.Contains(".Skip((currentPage - 1) * PageSize)", service);
        Assert.Contains(".Take(PageSize)", service);
        Assert.DoesNotContain("LedgerEntries", service);
        Assert.DoesNotContain("LedgerEntries", view);
        Assert.Contains("ChartOfAccountsIndexViewModel", view);
        Assert.Contains("name=\"companyId\"", view);
        Assert.Contains("name=\"q\"", view);
        Assert.Contains("_Pagination", view);
        Assert.Contains("هنوز سرفصل حسابی ثبت نشده است", view);
    }

    private static ApplicationDbContext NewDb()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Account NewAccount(int id, int companyId, string code, string name, Account? parent = null)
        => new()
        {
            Id = id,
            CompanyId = companyId,
            Code = code,
            Name = name,
            AccountType = AccountType.Asset,
            NormalBalance = NormalBalance.Debit,
            ParentAccountId = parent?.Id,
            ParentAccount = parent,
            IsActive = true
        };

    private static string ReadRepoFile(string relativePath)
        => File.ReadAllText(GetRepoPath(relativePath));

    private static string GetRepoPath(string relativePath, [CallerFilePath] string sourceFilePath = "")
    {
        var normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        foreach (var start in new[]
                 {
                     Environment.CurrentDirectory,
                     AppContext.BaseDirectory,
                     Path.GetDirectoryName(sourceFilePath) ?? string.Empty
                 })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, normalizedPath);
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException($"Repository file not found: {relativePath}");
    }
}
