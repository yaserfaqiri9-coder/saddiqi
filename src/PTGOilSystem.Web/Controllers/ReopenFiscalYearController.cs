using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Accounting;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Security;
using PTGOilSystem.Web.Services;
using PTGOilSystem.Web.Services.Accounting;

namespace PTGOilSystem.Web.Controllers;

/// <summary>
/// مرحله ۱۵ — بازگشاییِ کنترل‌شدهٔ سالِ بسته.
///
/// نمایشِ Precheck با GET؛ بازگشایی فقط POST + antiforgery + AdminOnly **و** Permissionِ صریحِ
/// ReopenFiscalYear + Reason + عبارتِ تأییدِ شاملِ کد سال. عملیات اتمیک است و آثارِ Final Close را
/// فقط با Reversal رسمی برمی‌گرداند.
/// </summary>
[Authorize(Policy = AuthPolicies.AdminOnly)]
[Route("accounting/reopen")]
public class ReopenFiscalYearController(
    IReopenFiscalYearService reopen,
    ApplicationDbContext db,
    ISystemCompanyProvider systemCompany,
    ICurrentUserContext currentUser) : Controller
{
    private bool HasReopenPermission
        => User.HasClaim(AppClaimTypes.Permission, AppPermissions.ReopenFiscalYear);

    [HttpGet("")]
    public async Task<IActionResult> Index(int? fiscalYearId, CancellationToken cancellationToken)
    {
        var ownerCompanyId = await systemCompany.GetOwnerCompanyIdAsync(cancellationToken);
        var ownerName = await db.Companies.AsNoTracking()
            .Where(c => c.Id == ownerCompanyId).Select(c => c.Name).FirstAsync(cancellationToken);

        // فقط سال‌های بسته قابل بازگشایی‌اند.
        var years = await db.FiscalYears.AsNoTracking()
            .Where(y => y.CompanyId == ownerCompanyId && y.Status == FiscalYearStatus.Closed)
            .OrderByDescending(y => y.StartDate).ThenByDescending(y => y.Id)
            .Select(y => new { y.Id, y.Name, y.Status }).ToListAsync(cancellationToken);

        var selectedYear = fiscalYearId is int fyid && years.Any(y => y.Id == fyid)
            ? fyid : years.Select(y => (int?)y.Id).FirstOrDefault();

        var yearRow = years.FirstOrDefault(y => y.Id == selectedYear);

        var precheck = selectedYear is int y2
            ? await reopen.PrecheckAsync(ownerCompanyId, y2, cancellationToken)
            : null;

        return View(new ReopenPageViewModel(
            new List<ClosingChecklistCompanyOption> { new(ownerCompanyId, ownerName, true) },
            ownerCompanyId,
            years.Select(y => new ClosingChecklistYearOption(y.Id, y.Name, y.Id == selectedYear)).ToList(),
            selectedYear, yearRow?.Name, yearRow?.Status.ToString(), HasReopenPermission, precheck));
    }

    [HttpPost("do")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(
        int fiscalYearId, string reason, string confirmation, CancellationToken cancellationToken)
    {
        var ownerCompanyId = await systemCompany.GetOwnerCompanyIdAsync(cancellationToken);
        var result = await reopen.ReopenAsync(
            ownerCompanyId, fiscalYearId, currentUser.UserId, reason, confirmation, HasReopenPermission, cancellationToken);

        TempData[result.Status == ReopenResultStatus.Succeeded ? "ok" : "err"] =
            result.Status switch
            {
                ReopenResultStatus.Succeeded => $"سال مالی بازگشایی شد ({result.ReversalJournalIds.Count} سند برگشت).",
                ReopenResultStatus.AlreadyReopened => "سال از قبل بازگشایی شده است.",
                _ => (result.FailureCode ?? "بازگشایی ممکن نیست.")
                    + (result.Blockers.Count > 0 ? " — " + string.Join("، ", result.Blockers) : "")
            };

        return RedirectToAction(nameof(Index), new { fiscalYearId });
    }
}
