using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Models.Accounting;

public sealed record ChartOfAccountsCompanyOption(int CompanyId, bool IsSelected);

public sealed record ChartOfAccountsRowViewModel(
    int Id,
    int CompanyId,
    string Code,
    string Name,
    AccountType AccountType,
    NormalBalance NormalBalance,
    bool IsActive,
    int? ParentAccountId,
    string? ParentCode,
    string? ParentName);

public sealed record ChartOfAccountsIndexViewModel(
    IReadOnlyList<ChartOfAccountsCompanyOption> Companies,
    int? SelectedCompanyId,
    string? Search,
    IReadOnlyList<ChartOfAccountsRowViewModel> Items,
    int CurrentPage,
    int PageCount,
    int TotalCount,
    int PageSize);
