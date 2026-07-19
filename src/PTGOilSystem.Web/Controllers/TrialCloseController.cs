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
/// مرحله ۱۳ — Trial Close و تسعیرِ پایان دوره.
///
/// خواندن (Preview) با GET؛ هر عملیاتِ نوشتنی فقط POST + antiforgery + AdminOnly. هیچ عملیاتِ
/// تغییردهنده‌ای با GET نیست. Trial Close سال را نمی‌بندد و دوره‌ای را HardLock نمی‌کند.
/// </summary>
[Authorize(Policy = AuthPolicies.AdminOnly)]
[Route("accounting/trial-close")]
public class TrialCloseController(
    ITrialCloseService trialClose,
    ApplicationDbContext db,
    ISystemCompanyProvider systemCompany,
    ICurrentUserContext currentUser) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? fiscalYearId, CancellationToken cancellationToken)
    {
        // شرکت همیشه شرکتِ مالک است؛ از URL یا فرم پذیرفته نمی‌شود.
        var ownerCompanyId = await systemCompany.GetOwnerCompanyIdAsync(cancellationToken);
        var ownerName = await db.Companies.AsNoTracking()
            .Where(c => c.Id == ownerCompanyId).Select(c => c.Name).FirstAsync(cancellationToken);

        var years = await db.FiscalYears.AsNoTracking().Where(y => y.CompanyId == ownerCompanyId)
            .OrderByDescending(y => y.StartDate).ThenByDescending(y => y.Id)
            .Select(y => new { y.Id, y.Name }).ToListAsync(cancellationToken);

        var selectedYear = fiscalYearId is int fyid && years.Any(y => y.Id == fyid)
            ? fyid : years.Select(y => (int?)y.Id).FirstOrDefault();

        var preview = selectedYear is int y2
            ? await trialClose.PreviewAsync(ownerCompanyId, y2, cancellationToken)
            : null;

        var runs = selectedYear is int y3
            ? await db.FiscalYearCloseRuns.AsNoTracking()
                .Where(r => r.FiscalYearId == y3)
                .OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id)
                .Select(r => new TrialCloseRunRow(
                    r.Id, r.RunType.ToString(), r.Revision, r.Status.ToString(),
                    r.StartedAt, r.StartedByUser != null ? r.StartedByUser.Username : null,
                    r.JournalCount, r.DebitTotal, r.CreditTotal, r.SnapshotHash))
                .ToListAsync(cancellationToken)
            : new List<TrialCloseRunRow>();

        return View(new TrialClosePageViewModel(
            new List<ClosingChecklistCompanyOption> { new(ownerCompanyId, ownerName, true) },
            ownerCompanyId,
            years.Select(y => new ClosingChecklistYearOption(y.Id, y.Name, y.Id == selectedYear)).ToList(),
            selectedYear, preview, runs));
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(int fiscalYearId, CancellationToken cancellationToken)
    {
        var ownerCompanyId = await systemCompany.GetOwnerCompanyIdAsync(cancellationToken);
        var preview = await trialClose.PreviewAsync(ownerCompanyId, fiscalYearId, cancellationToken);
        return preview is null ? NotFound() : Json(preview);
    }

    [HttpPost("run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunTrialClose(
        int fiscalYearId, bool acknowledgeWarnings, CancellationToken cancellationToken)
    {
        var ownerCompanyId = await systemCompany.GetOwnerCompanyIdAsync(cancellationToken);
        var result = await trialClose.RunTrialCloseAsync(
            ownerCompanyId, fiscalYearId, currentUser.UserId, acknowledgeWarnings, cancellationToken);

        if (result.Status == TrialCloseResultStatus.Succeeded)
            TempData["ok"] = $"Trial Close ثبت شد (Run #{result.CloseRunId}).";
        else
            TempData["err"] = result.FailureCode ?? "Trial Close ممکن نیست.";

        return RedirectToAction(nameof(Index), new { fiscalYearId });
    }

    [HttpPost("apply-revaluation")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyRevaluation(int fiscalYearId, CancellationToken cancellationToken)
    {
        var ownerCompanyId = await systemCompany.GetOwnerCompanyIdAsync(cancellationToken);
        var result = await trialClose.ApplyRevaluationAsync(ownerCompanyId, fiscalYearId, currentUser.UserId, cancellationToken);

        if (result.Status == TrialCloseResultStatus.Succeeded)
            TempData["ok"] = $"تسعیر پایان دوره اعمال شد ({result.RevaluationJournalIds.Count} سند).";
        else
            TempData["err"] = result.FailureCode ?? "اعمال تسعیر ممکن نیست.";

        return RedirectToAction(nameof(Index), new { fiscalYearId });
    }
}
