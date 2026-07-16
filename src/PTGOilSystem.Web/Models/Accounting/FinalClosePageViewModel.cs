using PTGOilSystem.Web.Services.Accounting;

namespace PTGOilSystem.Web.Models.Accounting;

/// <summary>مرحله ۱۴ — مدلِ صفحهٔ Final Close. فقط نمایشِ Precheck و فرمِ تأیید.</summary>
public sealed record FinalClosePageViewModel(
    IReadOnlyList<ClosingChecklistCompanyOption> Companies,
    int? SelectedCompanyId,
    IReadOnlyList<ClosingChecklistYearOption> Years,
    int? SelectedFiscalYearId,
    string? SelectedFiscalYearName,
    string? SelectedFiscalYearStatus,
    FinalClosePrecheck? Precheck);
