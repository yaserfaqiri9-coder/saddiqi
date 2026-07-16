using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Accounting;
using PTGOilSystem.Web.Security;
using PTGOilSystem.Web.Services;
using PTGOilSystem.Web.Services.Accounting;

namespace PTGOilSystem.Web.Controllers;

/// <summary>
/// مرحله ۱۴ — Final Close.
///
/// Precheck با GET؛ بستنِ سال فقط POST + antiforgery + AdminOnly و نیازمندِ عبارتِ تأییدِ شاملِ
/// کد سال مالی. عملیات اتمیک است و در صورتِ هر شکستی Rollback می‌شود.
/// </summary>
[Authorize(Policy = AuthPolicies.AdminOnly)]
[Route("accounting/final-close")]
public class FinalCloseController(
    IFinalCloseService finalClose,
    ApplicationDbContext db,
    ICurrentUserContext currentUser) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? companyId, int? fiscalYearId, CancellationToken cancellationToken)
    {
        var companies = await db.Companies.AsNoTracking()
            .Where(c => c.IsActive).OrderBy(c => c.Code)
            .Select(c => new { c.Id, c.Name }).ToListAsync(cancellationToken);

        var selectedCompany = companyId is int cid && companies.Any(c => c.Id == cid)
            ? cid : companies.Select(c => (int?)c.Id).FirstOrDefault();

        var years = selectedCompany is int scid
            ? await db.FiscalYears.AsNoTracking().Where(y => y.CompanyId == scid)
                .OrderByDescending(y => y.StartDate).ThenByDescending(y => y.Id)
                .Select(y => new { y.Id, y.Name, y.Status }).ToListAsync(cancellationToken)
            : new();

        var selectedYear = fiscalYearId is int fyid && years.Any(y => y.Id == fyid)
            ? fyid : years.Select(y => (int?)y.Id).FirstOrDefault();

        var yearRow = years.FirstOrDefault(y => y.Id == selectedYear);

        var precheck = selectedCompany is int c2 && selectedYear is int y2
            ? await finalClose.PrecheckAsync(c2, y2, cancellationToken)
            : null;

        return View(new FinalClosePageViewModel(
            companies.Select(c => new ClosingChecklistCompanyOption(c.Id, c.Name, c.Id == selectedCompany)).ToList(),
            selectedCompany,
            years.Select(y => new ClosingChecklistYearOption(y.Id, y.Name, y.Id == selectedYear)).ToList(),
            selectedYear, yearRow?.Name, yearRow?.Status.ToString(), precheck));
    }

    [HttpPost("close")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(
        int companyId, int fiscalYearId, string confirmation, CancellationToken cancellationToken)
    {
        var result = await finalClose.CloseAsync(companyId, fiscalYearId, currentUser.UserId, confirmation, cancellationToken);

        TempData[result.Status == FinalCloseResultStatus.Succeeded ? "ok" : "err"] =
            result.Status switch
            {
                FinalCloseResultStatus.Succeeded => $"سال مالی بسته شد (Run #{result.CloseRunId}).",
                FinalCloseResultStatus.AlreadyClosed => "سال از قبل بسته است.",
                _ => (result.FailureCode ?? "بستن سال ممکن نیست.")
                    + (result.Blockers.Count > 0 ? " — " + string.Join("، ", result.Blockers) : "")
            };

        return RedirectToAction(nameof(Index), new { companyId, fiscalYearId });
    }
}
