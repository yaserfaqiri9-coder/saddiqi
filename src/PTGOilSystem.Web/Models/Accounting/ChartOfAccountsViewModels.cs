using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Models.Accounting;

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

/// <summary>
/// فرمِ ایجاد حساب. عمداً هیچ فیلدِ CompanyId ندارد: شرکتِ مالک را همیشه سرور از
/// <c>ISystemCompanyProvider</c> تعیین می‌کند و از ورودیِ کاربر پذیرفته نمی‌شود.
/// </summary>
public sealed class ChartOfAccountsCreateForm
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public AccountType AccountType { get; set; } = AccountType.Asset;
    public NormalBalance NormalBalance { get; set; } = NormalBalance.Debit;
    public int? ParentAccountId { get; set; }
    public MonetaryTreatment MonetaryTreatment { get; set; } = MonetaryTreatment.Unspecified;
    public bool IsActive { get; set; } = true;
}

public sealed record ChartOfAccountsIndexViewModel(
    int? OwnerCompanyId,
    string? Search,
    IReadOnlyList<ChartOfAccountsRowViewModel> Items,
    int CurrentPage,
    int PageCount,
    int TotalCount,
    int PageSize);
