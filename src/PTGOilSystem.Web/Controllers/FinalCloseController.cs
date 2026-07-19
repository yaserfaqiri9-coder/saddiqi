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
    ISystemCompanyProvider systemCompany,
    ICurrentUserContext currentUser) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? fiscalYearId, CancellationToken cancellationToken)
    {
        var ownerCompanyId = await systemCompany.GetOwnerCompanyIdAsync(cancellationToken);
        var ownerName = await db.Companies.AsNoTracking()
            .Where(c => c.Id == ownerCompanyId).Select(c => c.Name).FirstAsync(cancellationToken);

        var years = await db.FiscalYears.AsNoTracking().Where(y => y.CompanyId == ownerCompanyId)
            .OrderByDescending(y => y.StartDate).ThenByDescending(y => y.Id)
            .Select(y => new { y.Id, y.Name, y.Status }).ToListAsync(cancellationToken);

        var selectedYear = fiscalYearId is int fyid && years.Any(y => y.Id == fyid)
            ? fyid : years.Select(y => (int?)y.Id).FirstOrDefault();

        var yearRow = years.FirstOrDefault(y => y.Id == selectedYear);

        var precheck = selectedYear is int y2
            ? await finalClose.PrecheckAsync(ownerCompanyId, y2, cancellationToken)
            : null;

        return View(new FinalClosePageViewModel(
            new List<ClosingChecklistCompanyOption> { new(ownerCompanyId, ownerName, true) },
            ownerCompanyId,
            years.Select(y => new ClosingChecklistYearOption(y.Id, y.Name, y.Id == selectedYear)).ToList(),
            selectedYear, yearRow?.Name, yearRow?.Status.ToString(), precheck));
    }

    [HttpPost("close")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(
        int fiscalYearId, string confirmation, CancellationToken cancellationToken)
    {
        var ownerCompanyId = await systemCompany.GetOwnerCompanyIdAsync(cancellationToken);
        var result = await finalClose.CloseAsync(ownerCompanyId, fiscalYearId, currentUser.UserId, confirmation, cancellationToken);

        TempData[result.Status == FinalCloseResultStatus.Succeeded ? "ok" : "err"] =
            result.Status switch
            {
                FinalCloseResultStatus.Succeeded => $"سال مالی بسته شد (Run #{result.CloseRunId}).",
                FinalCloseResultStatus.AlreadyClosed => "سال از قبل بسته است.",
                _ => (result.FailureCode ?? "بستن سال ممکن نیست.")
                    + (result.Blockers.Count > 0 ? " — " + string.Join("، ", result.Blockers) : "")
            };

        return RedirectToAction(nameof(Index), new { fiscalYearId });
    }
}
