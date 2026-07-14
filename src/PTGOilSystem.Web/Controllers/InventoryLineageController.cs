using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Security;
using PTGOilSystem.Web.Services;

namespace PTGOilSystem.Web.Controllers;

// اجرای دستیِ backfill لایهٔ Inventory Lineage. فقط Admin، فقط وقتی Lineage:BackfillEnabled=true.
// هرگز در startup خودکار اجرا نمی‌شود. خروجی فقط یک خلاصهٔ JSON است (بدون UI).
[Authorize(Policy = AuthPolicies.AdminOnly)]
[Route("admin/inventory-lineage")]
public sealed class InventoryLineageController : Controller
{
    private readonly InventoryLineageBackfillService _backfill;
    private readonly LineageOptions _options;

    public InventoryLineageController(
        InventoryLineageBackfillService backfill,
        IOptions<LineageOptions> options)
    {
        _backfill = backfill;
        _options = options.Value;
    }

    // POST /admin/inventory-lineage/backfill
    [HttpPost("backfill")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Backfill(CancellationToken ct)
    {
        if (!_options.BackfillEnabled || !_options.WriteLots)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Backfill غیرفعال است. برای اجرا، Lineage:BackfillEnabled و Lineage:WriteLots را موقتاً true کنید."
            });
        }

        var summary = await _backfill.RunAsync(ct);
        return Json(new { ok = true, summary });
    }
}
