using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Models.PlattsRates;
using PTGOilSystem.Web.Services;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class PlattsRatesControllerTests
{
    [Fact]
    public async Task SaveDaily_Post_Persists_DailyRate_When_DailyForm_Is_Valid()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        db.Products.Add(new Product { Id = 1, Code = "GO", Name = "Gas Oil", IsActive = true });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var model = new PlattsRatesPageViewModel
        {
            DailyForm = new DailyPlattsRateFormViewModel
            {
                ProductId = 1,
                BenchmarkCode = "ULSD",
                PriceDate = new DateTime(2026, 4, 28),
                PriceUsdPerMt = 500m,
                Source = "Platts"
            }
        };

        controller.TryValidateModel(model);

        var result = await controller.SaveDaily(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var rate = await db.DailyPlattsPrices.SingleAsync();
        Assert.Equal(1, rate.ProductId);
        Assert.Equal("ULSD", rate.BenchmarkCode);
        Assert.Equal(500m, rate.PriceUsdPerMt);
    }

    [Fact]
    public async Task SaveMonthlyManual_Post_Persists_ManualRate_When_ManualForm_Is_Valid()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        db.Products.Add(new Product { Id = 1, Code = "JET", Name = "Jet Fuel", IsActive = true });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var model = new PlattsRatesPageViewModel
        {
            MonthlyManualForm = new MonthlyManualRateFormViewModel
            {
                ProductId = 1,
                BenchmarkCode = "JET",
                Month = new DateTime(2026, 4, 1),
                PriceUsdPerMt = 610m,
                Notes = "Manual override"
            }
        };

        controller.TryValidateModel(model);

        var result = await controller.SaveMonthlyManual(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var rate = await db.PlattsMonthlyManuals.SingleAsync();
        Assert.Equal(1, rate.ProductId);
        Assert.Equal("JET", rate.BenchmarkCode);
        Assert.Equal(610m, rate.PriceUsdPerMt);
    }

    [Fact]
    public void PlattsRates_View_Hides_Inactive_Panels_And_Uses_FormLevel_Validation_Summary()
    {
        var indexViewPath = GetProjectFilePath("src", "PTGOilSystem.Web", "Views", "PlattsRates", "Index.cshtml");
        var dailyFormPath = GetProjectFilePath("src", "PTGOilSystem.Web", "Views", "PlattsRates", "_CreateDailyForm.cshtml");
        var manualFormPath = GetProjectFilePath("src", "PTGOilSystem.Web", "Views", "PlattsRates", "_CreateManualForm.cshtml");

        var indexContent = File.ReadAllText(indexViewPath);
        var dailyFormContent = File.ReadAllText(dailyFormPath);
        var manualFormContent = File.ReadAllText(manualFormPath);

        Assert.Contains("hidden=\"@(!dailyActive)\"", indexContent);
        Assert.Contains("hidden=\"@(!monthlyActive)\"", indexContent);
        Assert.Contains("hidden=\"@(!manualActive)\"", indexContent);
        Assert.DoesNotContain("var dailyModel = new DailyPlattsRateFormViewModel()", indexContent);
        Assert.DoesNotContain("var manualModel = new MonthlyManualRateFormViewModel()", indexContent);
        Assert.DoesNotContain("model=\"dailyModel\"", indexContent);
        Assert.DoesNotContain("model=\"manualModel\"", indexContent);
        Assert.Contains("<partial name=\"~/Views/Shared/_CreateModalShell.cshtml\" model=\"Model\" view-data=\"ViewData\" />", indexContent);

        Assert.Contains("asp-validation-summary=\"ModelOnly\"", dailyFormContent);
        Assert.DoesNotContain("asp-validation-summary=\"All\"", dailyFormContent);
        Assert.Contains("asp-validation-summary=\"ModelOnly\"", manualFormContent);
        Assert.DoesNotContain("asp-validation-summary=\"All\"", manualFormContent);
    }

    private static PlattsRatesController BuildController(ApplicationDbContext db)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddControllersWithViews()
            .Services
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };

        return new PlattsRatesController(db, new AuditService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            ObjectValidator = services.GetRequiredService<IObjectModelValidator>(),
            TempData = new TempDataDictionary(httpContext, new InMemoryTempDataProvider()),
            Url = new StubUrlHelper()
        };
    }

    private static DbContextOptions<ApplicationDbContext> NewDbOptions()
        => new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static string GetProjectFilePath(params string[] segments)
    {
        var relativePath = Path.Combine(segments);
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath));
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        private IDictionary<string, object> _data = new Dictionary<string, object>();

        public IDictionary<string, object> LoadTempData(HttpContext context) => _data;

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
            => _data = new Dictionary<string, object>(values);
    }

    private sealed class StubUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();

        public string? Action(UrlActionContext actionContext) => "/";

        public string? Content(string? contentPath) => contentPath;

        public bool IsLocalUrl(string? url) => true;

        public string? Link(string? routeName, object? values) => "/";

        public string? RouteUrl(UrlRouteContext routeContext) => "/";
    }
}
