using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Helpers;

// برچسب دری برای «هزینه/کرایه بدوش کیست». فقط نمایشی است.
public static class CostResponsibilityLabels
{
    public static string ToPersian(CostResponsibility? value) => value switch
    {
        CostResponsibility.Buyer => "بدوش خریدار",
        CostResponsibility.Seller => "بدوش فروشنده",
        CostResponsibility.Shared => "مشترک",
        CostResponsibility.Unspecified => "نامشخص",
        _ => "—"
    };
}
