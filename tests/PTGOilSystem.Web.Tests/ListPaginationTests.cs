using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.ContractBalanceTransfers;
using PTGOilSystem.Web.Models.Entities;
using Xunit;

namespace PTGOilSystem.Web.Tests;

/// <summary>
/// صفحه‌بندی این فهرست‌ها باید سمت SQL باشد (Skip/Take)، نه پس از ToList.
/// </summary>
public class ListPaginationTests
{
    private const int PageSize = 50;

    [Theory]
    [InlineData(1, PageSize)]
    [InlineData(2, PageSize)]
    [InlineData(3, 20)]   // صفحهٔ آخر: ۱۲۰ - ۱۰۰ = ۲۰ ردیف
    public async Task ContractBalanceTransfers_Index_Returns_One_Page_Of_Rows(int page, int expectedRows)
    {
        await using var db = NewDb();
        SeedTransfers(db, count: 120);
        await db.SaveChangesAsync();

        var controller = new ContractBalanceTransfersController(db, transfers: null!);

        var view = Assert.IsType<ViewResult>(await controller.Index(page: page));
        var model = Assert.IsType<ContractBalanceTransferIndexViewModel>(view.Model);

        Assert.Equal(120, model.TotalCount);
        Assert.Equal(expectedRows, model.Items.Count);
        Assert.Equal(3, model.PageCount);
        Assert.Equal(page, model.CurrentPage);
    }

    [Fact]
    public async Task ContractBalanceTransfers_Index_Pages_Do_Not_Overlap()
    {
        await using var db = NewDb();
        SeedTransfers(db, count: 120);
        await db.SaveChangesAsync();

        var controller = new ContractBalanceTransfersController(db, transfers: null!);

        var first = ItemsOf(await controller.Index(page: 1));
        var second = ItemsOf(await controller.Index(page: 2));

        Assert.Empty(first.Select(r => r.Id).Intersect(second.Select(r => r.Id)));
    }

    [Theory]
    [InlineData(0)]      // زیر بازه
    [InlineData(999)]    // بالای بازه
    public async Task ContractBalanceTransfers_Index_Clamps_Out_Of_Range_Page(int page)
    {
        await using var db = NewDb();
        SeedTransfers(db, count: 120);
        await db.SaveChangesAsync();

        var controller = new ContractBalanceTransfersController(db, transfers: null!);

        var view = Assert.IsType<ViewResult>(await controller.Index(page: page));
        var model = Assert.IsType<ContractBalanceTransferIndexViewModel>(view.Model);

        Assert.InRange(model.CurrentPage, 1, model.PageCount);
    }

    [Fact]
    public async Task ContractBalanceTransfers_Index_Handles_Empty_List()
    {
        await using var db = NewDb();
        var controller = new ContractBalanceTransfersController(db, transfers: null!);

        var view = Assert.IsType<ViewResult>(await controller.Index());
        var model = Assert.IsType<ContractBalanceTransferIndexViewModel>(view.Model);

        Assert.Empty(model.Items);
        Assert.Equal(0, model.TotalCount);
        Assert.Equal(1, model.PageCount);   // هرگز صفر نشود تا صفحه‌بندی نشکند
    }

    private static IReadOnlyList<ContractBalanceTransferListItemViewModel> ItemsOf(IActionResult result)
        => Assert.IsType<ContractBalanceTransferIndexViewModel>(
            Assert.IsType<ViewResult>(result).Model).Items;

    private static void SeedTransfers(ApplicationDbContext db, int count)
    {
        // FromContractId/ToContractId اجباری‌اند، پس Include یک INNER JOIN می‌سازد؛
        // بدون این قراردادها هیچ ردیفی برنمی‌گردد (در محیط واقعی همیشه وجود دارند).
        db.Contracts.AddRange(
            new Contract { Id = 1, ContractNumber = "C-1", CompanyId = 1, ProductId = 1 },
            new Contract { Id = 2, ContractNumber = "C-2", CompanyId = 1, ProductId = 1 });

        for (var i = 1; i <= count; i++)
        {
            db.ContractBalanceTransfers.Add(new ContractBalanceTransfer
            {
                Id = i,
                TransferDate = new DateTime(2026, 1, 1).AddDays(i),
                FromContractId = 1,
                ToContractId = 2,
                AmountOriginal = i,
                CurrencyCode = "USD",
                FxRateToUsd = 1m,
                AmountUsd = i,
                Reference = $"TRF-{i}"
            });
        }
    }

    private static ApplicationDbContext NewDb()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
