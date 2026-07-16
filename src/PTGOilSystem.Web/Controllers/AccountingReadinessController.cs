using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTGOilSystem.Web.Security;
using PTGOilSystem.Web.Services.Accounting;

namespace PTGOilSystem.Web.Controllers;

/// <summary>
/// مرحله ۹ — گزارش فقط‌خواندنیِ آمادگی Cutover.
///
/// این کنترلر عمداً فقط GET دارد و هیچ مسیر نوشتنی ندارد: نه Flag روشن می‌کند، نه Migration اجرا
/// می‌کند، نه Backfill می‌نویسد. برای اجرای روی Backup دیتابیس عملیاتی ساخته شده — رشتهٔ اتصال را
/// همان کسی که اجرا می‌کند از بیرون می‌دهد (پیکربندی معمول برنامه)؛ در repo هیچ رشتهٔ اتصال
/// عملیاتی ساخته یا فرض نشده است.
/// </summary>
[Authorize(Policy = AuthPolicies.AdminOnly)]
[Route("accounting/readiness")]
public class AccountingReadinessController(IAccountingReadinessService readiness) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => Json(await readiness.BuildAsync(cancellationToken));
}
