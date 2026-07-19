using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PTGOilSystem.Web.Infrastructure.RateLimiting;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Security;
using PTGOilSystem.Web.Services.Accounting;
using PTGOilSystem.Web.Services.Exports;

namespace PTGOilSystem.Web.Controllers;

/// <summary>
/// مرحله ۱۰ — صفحه‌های سال مالی.
///
/// خواندن برای نقش‌های حسابداری/مدیریت باز است (<see cref="AuthPolicies.ManageData"/>) ولی هر
/// عملیاتِ تغییردهنده فقط POST + ضدجعل است و <see cref="AuthPolicies.AdminOnly"/> می‌خواهد:
/// ساختِ سال مالی ساختارِ دفتر را تعیین می‌کند و کارِ نقشِ عملیاتی نیست.
/// </summary>
[Authorize(Policy = AuthPolicies.ManageData)]
public class FiscalYearsController(
    IFiscalYearOverviewService overview,
    IFiscalYearProvisioningService provisioning,
    IFiscalPeriodLockService periodLocks,
    ISystemCompanyProvider systemCompany,
    ICurrentUserContext currentUser) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var ownerCompanyId = await systemCompany.GetOwnerCompanyIdAsync(cancellationToken);
        return View(await overview.BuildIndexAsync(ownerCompanyId, CanManage, cancellationToken));
    }

    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> Export(string? format, CancellationToken cancellationToken)
    {
        var ownerCompanyId = await systemCompany.GetOwnerCompanyIdAsync(cancellationToken);
        var model = await overview.BuildIndexAsync(ownerCompanyId, CanManage, cancellationToken);
        var years = (model.CurrentYear is null ? [] : new[] { model.CurrentYear })
            .Concat(model.OtherYears)
            .OrderByDescending(year => year.StartDate)
            .ToList();
        var rows = years.SelectMany(year => year.Periods.Count > 0
            ? year.Periods.Select(period => new
            {
                year.CompanyName, YearName = year.Name, YearStart = year.StartDate, YearEnd = year.EndDate,
                YearStatus = year.Status.ToString(), PeriodNumber = (int?)period.PeriodNumber, PeriodName = (string?)period.Name,
                PeriodStart = (DateTime?)period.StartDate, PeriodEnd = (DateTime?)period.EndDate, PeriodStatus = (string?)period.Status.ToString()
            })
            : new[]
            {
                new
                {
                    year.CompanyName, YearName = year.Name, YearStart = year.StartDate, YearEnd = year.EndDate,
                    YearStatus = year.Status.ToString(), PeriodNumber = (int?)null, PeriodName = (string?)null,
                    PeriodStart = (DateTime?)null, PeriodEnd = (DateTime?)null, PeriodStatus = (string?)null
                }
            }).ToList();

        return TabularExportSupport.File(this, format, new TabularExportDocument
        {
            FileNameStem = "PTG_Fiscal_Years", TitleFa = "سال‌های مالی", TitleEn = "Fiscal Years", KnownRowCount = rows.Count,
            Filters = TabularExportSupport.FilterSummary(("شرکت / Company", model.SelectedCompanyName)),
            Columns =
            [
                new("شرکت", "Company", Width: 20), new("سال مالی", "Fiscal year", Width: 14),
                new("شروع سال", "Year start", TabularExportValueType.Date, 13), new("پایان سال", "Year end", TabularExportValueType.Date, 13),
                new("وضعیت سال", "Year status", Width: 13), new("شماره دوره", "Period no.", TabularExportValueType.Integer, 11),
                new("دوره", "Period", Width: 16), new("شروع دوره", "Period start", TabularExportValueType.Date, 13),
                new("پایان دوره", "Period end", TabularExportValueType.Date, 13), new("وضعیت دوره", "Period status", Width: 13)
            ],
            Rows = rows.Select(row => new TabularExportRow(
            [
                TabularExportCell.Text(row.CompanyName), TabularExportCell.Text(row.YearName), TabularExportCell.Date(row.YearStart),
                TabularExportCell.Date(row.YearEnd), TabularExportCell.Text(row.YearStatus), TabularExportCell.Integer(row.PeriodNumber),
                TabularExportCell.Text(row.PeriodName), TabularExportCell.Date(row.PeriodStart), TabularExportCell.Date(row.PeriodEnd),
                TabularExportCell.Text(row.PeriodStatus)
            ]))
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var model = await overview.BuildDetailsAsync(id, CanManage, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<IActionResult> CreateInitialYear(
        string name,
        DateTime startDate,
        DateTime endDate,
        int periodCount,
        CancellationToken cancellationToken)
    {
        // شرکت همیشه سمت سرور تعیین می‌شود؛ هیچ CompanyId از فرم پذیرفته نمی‌شود.
        var ownerCompanyId = await systemCompany.GetOwnerCompanyIdAsync(cancellationToken);
        var result = await provisioning.CreateInitialFiscalYearAsync(
            new CreateInitialFiscalYearInput(ownerCompanyId, name, startDate, endDate, periodCount),
            currentUser.UserId,
            cancellationToken);

        if (result.Succeeded)
            TempData["ok"] = "اولین سال مالی با وضعیت باز ساخته شد.";
        else
            TempData["err"] = result.ErrorCode ?? "ساخت اولین سال مالی مجاز نیست.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<IActionResult> CreateNextYear(CancellationToken cancellationToken)
    {
        var ownerCompanyId = await systemCompany.GetOwnerCompanyIdAsync(cancellationToken);
        var result = await provisioning.CreateNextYearAsync(
            ownerCompanyId,
            currentUser.UserId,
            cancellationToken);

        if (result.Succeeded)
            TempData["ok"] = "سال مالی بعدی به‌صورت پیش‌نویس ساخته شد.";
        else
            TempData["err"] = result.ErrorCode ?? "ساخت سال مالی بعدی مجاز نیست.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// مرحله ۱۱ — تغییرِ قفلِ دوره. قفلِ سخت از اینجا هم برگشت‌ناپذیر است؛ سرویس آن را رد می‌کند.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<IActionResult> ChangePeriodLock(
        int id,
        int fiscalPeriodId,
        FiscalPeriodStatus status,
        CancellationToken cancellationToken)
    {
        var result = await periodLocks.ChangeStatusAsync(
            fiscalPeriodId,
            status,
            currentUser.UserId,
            cancellationToken);

        if (result.Succeeded)
            TempData["ok"] = "وضعیت قفل دوره تغییر کرد.";
        else
            TempData["err"] = result.ErrorCode ?? "تغییر قفل دوره مجاز نیست.";

        return RedirectToAction(nameof(Details), new { id });
    }

    private bool CanManage => RoleAccessRules.CanManageUsers(User);
}
