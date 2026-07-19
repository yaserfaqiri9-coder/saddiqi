using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Infrastructure.RateLimiting;
using PTGOilSystem.Web.Models.Accounting;
using PTGOilSystem.Web.Security;
using PTGOilSystem.Web.Services.Accounting;

namespace PTGOilSystem.Web.Controllers;

/// <summary>
/// مرحله ۱۲ — چک‌لیستِ بستنِ سال.
///
/// این کنترلر **فقط GET** دارد و هیچ مسیر نوشتنی ندارد: نه Close، نه Lock، نه Migration، نه
/// Posting. اجرای چک‌لیست هیچ Entity یا Journal را تغییر نمی‌دهد. GET برای مشاهده و Export مجاز
/// است. دسترسی برای نقش‌های حسابداری/مدیریت باز است.
/// </summary>
[Authorize(Policy = AuthPolicies.ManageData)]
[Route("accounting/closing-checklist")]
public class ClosingChecklistController(
    IClosingChecklistService checklist,
    ApplicationDbContext db,
    ISystemCompanyProvider systemCompany) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? fiscalYearId, CancellationToken cancellationToken)
    {
        var ownerCompanyId = await systemCompany.GetOwnerCompanyIdAsync(cancellationToken);
        var ownerName = await db.Companies.AsNoTracking()
            .Where(c => c.Id == ownerCompanyId).Select(c => c.Name).FirstAsync(cancellationToken);

        var years = await db.FiscalYears.AsNoTracking()
            .Where(y => y.CompanyId == ownerCompanyId)
            .OrderByDescending(y => y.StartDate).ThenByDescending(y => y.Id)
            .Select(y => new { y.Id, y.Name })
            .ToListAsync(cancellationToken);

        var selectedYear = fiscalYearId is int fyid && years.Any(y => y.Id == fyid)
            ? fyid
            : years.Select(y => (int?)y.Id).FirstOrDefault();

        var report = selectedYear is int y2
            ? await checklist.BuildAsync(ownerCompanyId, y2, cancellationToken)
            : null;

        var model = new ClosingChecklistPageViewModel(
            new List<ClosingChecklistCompanyOption> { new(ownerCompanyId, ownerName, true) },
            ownerCompanyId,
            years.Select(y => new ClosingChecklistYearOption(y.Id, y.Name, y.Id == selectedYear)).ToList(),
            selectedYear,
            report);

        return View(model);
    }

    [HttpGet("json")]
    public async Task<IActionResult> Json(int fiscalYearId, CancellationToken cancellationToken)
    {
        var ownerCompanyId = await systemCompany.GetOwnerCompanyIdAsync(cancellationToken);
        var report = await checklist.BuildAsync(ownerCompanyId, fiscalYearId, cancellationToken);
        return report is null ? NotFound() : Json(report);
    }

    [HttpGet("csv")]
    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> Csv(int fiscalYearId, CancellationToken cancellationToken)
    {
        var ownerCompanyId = await systemCompany.GetOwnerCompanyIdAsync(cancellationToken);
        var report = await checklist.BuildAsync(ownerCompanyId, fiscalYearId, cancellationToken);
        if (report is null)
            return NotFound();

        var sb = new StringBuilder();
        sb.AppendLine("Code,Status,Title,CompanyId,FiscalYearId,RecordCount,FeatureFlag,Link,RequiredAction,SampleRecords");
        foreach (var check in report.Checks)
        {
            sb.Append(Csv(check.Code)).Append(',')
              .Append(Csv(check.Status.ToString())).Append(',')
              .Append(Csv(check.Title)).Append(',')
              .Append(check.CompanyId.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(check.FiscalYearId.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(check.RecordCount.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Csv(check.FeatureFlag)).Append(',')
              .Append(Csv(check.Link)).Append(',')
              .Append(Csv(check.RequiredAction)).Append(',')
              .Append(Csv(string.Join(" ; ", check.SampleRecords)))
              .Append('\n');
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"closing-checklist-{ownerCompanyId}-{fiscalYearId}.csv");
    }

    // نقل‌قول‌گذاری استاندارد CSV: هر فیلد در گیومه، و گیومهٔ داخلی دوبار می‌شود.
    private static string Csv(string? value)
        => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
}
