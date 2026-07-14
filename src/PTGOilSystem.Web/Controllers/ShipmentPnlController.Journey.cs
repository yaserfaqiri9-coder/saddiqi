using Microsoft.AspNetCore.Mvc;

namespace PTGOilSystem.Web.Controllers;

// «نمای کشتی / Shipment Journey» در صفحهٔ مادر Details ادغام شد
// (تب‌های خلاصه/جریان حمل و موجودی/مصارف و کسورات شامل stageها، shortageها، ضایعات، خلاصه و اخطارها).
// این action فقط برای نشکستن لینک‌های قدیمی نگه داشته شده و به Details هدایت می‌کند.
public partial class ShipmentPnlController
{
#pragma warning disable IDE0060 // tab فقط برای سازگاری URL قدیمی نگه داشته شده است
    public Task<IActionResult> Journey(int id, string tab = "summary")
        => Task.FromResult<IActionResult>(RedirectToAction(nameof(Details), new { id }));
#pragma warning restore IDE0060
}
