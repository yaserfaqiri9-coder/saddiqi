using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTGOilSystem.Web.Services.Accounting;

namespace PTGOilSystem.Web.Controllers;

[Authorize]
[Route("accounting/chart-of-accounts")]
public sealed class ChartOfAccountsController(IChartOfAccountsReadService service) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        int? companyId,
        string? q,
        int page = 1,
        CancellationToken cancellationToken = default)
        => View(await service.BuildAsync(companyId, q, page, cancellationToken));
}
