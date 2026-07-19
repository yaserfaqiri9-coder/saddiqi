using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models;
using PTGOilSystem.Web.Services;

namespace PTGOilSystem.Web.Controllers;

public sealed class CustomersController : Controller
{
    private readonly AppDbContext _db;

    public CustomersController(AppDbContext db) => _db = db;

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Filter(
        [FromBody] ListFilterRequest request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 10, 100);

        var baseQuery = _db.Customers
            .AsNoTracking()
            .Apply(request);

        var total = await baseQuery.CountAsync(cancellationToken);
        var customers = await baseQuery
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        ViewData["Total"] = total;
        return PartialView("_CustomerRows", customers);
    }
}
