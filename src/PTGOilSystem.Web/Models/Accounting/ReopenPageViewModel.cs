using PTGOilSystem.Web.Services.Accounting;

namespace PTGOilSystem.Web.Models.Accounting;

/// <summary>مرحله ۱۵ — مدلِ صفحهٔ بازگشاییِ کنترل‌شده.</summary>
public sealed record ReopenPageViewModel(
    IReadOnlyList<ClosingChecklistCompanyOption> Companies,
    int? SelectedCompanyId,
    IReadOnlyList<ClosingChecklistYearOption> Years,
    int? SelectedFiscalYearId,
    string? SelectedFiscalYearName,
    string? SelectedFiscalYearStatus,
    bool HasReopenPermission,
    ReopenPrecheck? Precheck);
