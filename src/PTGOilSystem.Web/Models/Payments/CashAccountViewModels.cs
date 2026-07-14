using System.ComponentModel.DataAnnotations;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Models.Payments;

public sealed class CashAccountIndexFilterViewModel
{
    [Display(Name = "جستجو")]
    [StringLength(200)]
    public string? Query { get; set; }

    [Display(Name = "نوع حساب")]
    public CashAccountType? AccountType { get; set; }

    [Display(Name = "ارز")]
    [StringLength(10)]
    public string? Currency { get; set; }

    [Display(Name = "وضعیت")]
    public bool? IsActive { get; set; }
}

public sealed class CashAccountIndexViewModel
{
    public CashAccountIndexFilterViewModel Filter { get; init; } = new();
    public IReadOnlyList<CashAccount> Items { get; init; } = [];
    public int CurrentPage { get; init; } = 1;
    public int PageCount { get; init; } = 1;
    public int TotalCount { get; init; }
    public FinanceMetricCardsViewModel FinanceMetrics { get; init; } = new();
}

public static class CashAccountTypeLabels
{
    public static string ToPersian(CashAccountType accountType) => accountType switch
    {
        CashAccountType.Cash => "نقد",
        CashAccountType.Bank => "بانک",
        CashAccountType.Mixed => "مختلط (همه ارزها)",
        _ => accountType.ToString()
    };
}
