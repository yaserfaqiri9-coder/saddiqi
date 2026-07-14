using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class DailyFxRatesControllerTests
{
    [Fact]
    public async Task Create_Persists_Fx_Rate_When_Currencies_Are_Valid()
    {
        var options = NewDbOptions();

        await using var db = new ApplicationDbContext(options);
        SeedActiveCurrencies(db, "USD", "EUR");
        var controller = BuildController(db);

        var result = await controller.Create(new DailyFxRate
        {
            BaseCurrency = "eur",
            QuoteCurrency = "usd",
            RateDate = new DateTime(2026, 4, 28),
            Rate = 1.125m,
            Source = "Market close"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var item = await db.DailyFxRates.SingleAsync();
        Assert.Equal("EUR", item.BaseCurrency);
        Assert.Equal("USD", item.QuoteCurrency);
        Assert.Equal(new DateTime(2026, 4, 28), item.RateDate);
        Assert.Equal(1.125m, item.Rate);
    }

    [Fact]
    public async Task Create_Returns_View_When_Currency_Is_Not_Active_Master_Data()
    {
        var options = NewDbOptions();

        await using var db = new ApplicationDbContext(options);
        SeedActiveCurrencies(db, "USD");
        var controller = BuildController(db);

        var result = await controller.Create(new DailyFxRate
        {
            BaseCurrency = "EUR",
            QuoteCurrency = "USD",
            RateDate = new DateTime(2026, 4, 28),
            Rate = 1.125m
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DailyFxRate>(view.Model);
        Assert.Equal("EUR", model.BaseCurrency);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(db.DailyFxRates);
    }

    private static void SeedActiveCurrencies(ApplicationDbContext db, params string[] codes)
    {
        var items = codes
            .Select((code, index) => new Currency
            {
                Id = index + 1,
                Code = code,
                Name = code,
                IsActive = true
            });
        db.Currencies.AddRange(items);
        db.SaveChanges();
    }

    private static DailyFxRatesController BuildController(ApplicationDbContext db)
        => new(db, new AuditService(db))
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

    private static DbContextOptions<ApplicationDbContext> NewDbOptions()
        => new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
