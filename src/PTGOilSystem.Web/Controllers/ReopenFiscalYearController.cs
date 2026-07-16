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
    ICurrentUserContext currentUser) : Controller
{
    private bool HasReopenPermission
        => User.HasClaim(AppClaimTypes.Permission, AppPermissions.ReopenFiscalYear);

    [HttpGet("")]
    public async Task<IActionResult> Index(int? companyId, int? fiscalYearId, CancellationToken cancellationToken)
    {
        var companies = await db.Companies.AsNoTracking()
            .Where(c => c.IsActive).OrderBy(c => c.Code)
            .Select(c => new { c.Id, c.Name }).ToListAsync(cancellationToken);

        var selectedCompany = companyId is int cid && companies.Any(c => c.Id == cid)
            ? cid : companies.Select(c => (int?)c.Id).FirstOrDefault();

        // فقط سال‌های بسته قابل بازگشایی‌اند.
        var years = selectedCompany is int scid
            ? await db.FiscalYears.AsNoTracking()
                .Where(y => y.CompanyId == scid && y.Status == FiscalYearStatus.Closed)
                .OrderByDescending(y => y.StartDate).ThenByDescending(y => y.Id)
                .Select(y => new { y.Id, y.Name, y.Status }).ToListAsync(cancellationToken)
            : new();

        var selectedYear = fiscalYearId is int fyid && years.Any(y => y.Id == fyid)
            ? fyid : years.Select(y => (int?)y.Id).FirstOrDefault();

        var yearRow = years.FirstOrDefault(y => y.Id == selectedYear);

        var precheck = selectedCompany is int c2 && selectedYear is int y2
            ? await reopen.PrecheckAsync(c2, y2, cancellationToken)
            : null;

        return View(new ReopenPageViewModel(
            companies.Select(c => new ClosingChecklistCompanyOption(c.Id, c.Name, c.Id == selectedCompany)).ToList(),
            selectedCompany,
            years.Select(y => new ClosingChecklistYearOption(y.Id, y.Name, y.Id == selectedYear)).ToList(),
            selectedYear, yearRow?.Name, yearRow?.Status.ToString(), HasReopenPermission, precheck));
    }

    [HttpPost("do")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(
        int companyId, int fiscalYearId, string reason, string confirmation, CancellationToken cancellationToken)
    {
        var result = await reopen.ReopenAsync(
            companyId, fiscalYearId, currentUser.UserId, reason, confirmation, HasReopenPermission, cancellationToken);

        TempData[result.Status == ReopenResultStatus.Succeeded ? "ok" : "err"] =
            result.Status switch
            {
                ReopenResultStatus.Succeeded => $"سال مالی بازگشایی شد ({result.ReversalJournalIds.Count} سند برگشت).",
                ReopenResultStatus.AlreadyReopened => "سال از قبل بازگشایی شده است.",
                _ => (result.FailureCode ?? "بازگشایی ممکن نیست.")
                    + (result.Blockers.Count > 0 ? " — " + string.Join("، ", result.Blockers) : "")
            };

        return RedirectToAction(nameof(Index), new { companyId, fiscalYearId });
    }
}
