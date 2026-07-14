using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTGOilSystem.Web.Models;
using PTGOilSystem.Web.Services;

namespace PTGOilSystem.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IDashboardService _dashboard;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IDashboardService dashboard,
        ILogger<HomeController> logger)
    {
        _dashboard = dashboard;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var vm = new DashboardViewModel();

        try
        {
            vm = await _dashboard.BuildDashboardAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard aggregates unavailable; database may not be migrated yet.");
            ViewData["DbWarning"] = "Database connection is unavailable or migrations have not been applied.";
        }

        return View(vm);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
        => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
