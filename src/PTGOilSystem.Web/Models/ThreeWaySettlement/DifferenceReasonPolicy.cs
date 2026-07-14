using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Models.ThreeWaySettlement;

// نوع سرنوشت تفاوت مبلغ در تسویه سه‌طرفه (فقط برای برچسب/راهنما؛ هیچ منطق posting به آن وابسته نیست).
public enum DifferencePolicyKind
{
    None,
    FxReview,          // تفاوت نرخ — فقط بررسی/گزارش
    PotentialExpense,  // کمیشن / کرایه حواله — هزینه احتمالی در فاز بعد
    AdjustmentOnly,    // تخفیف / اصلاح حساب — بدون حسابداری
    NeedsReview        // مارجین دلال / سایر — نیاز به بررسی یا توضیح
}

// فاز C1 — سیاست trace-only تفاوت مبلغ.
// این کلاس فقط برچسب و توضیح دری می‌دهد که هر نوع تفاوت فعلاً چه وضعی دارد و در آینده چه می‌شود.
// هیچ LedgerEntry / ExpenseTransaction / Payment / posting حسابداری در اینجا انجام نمی‌شود.
public static class DifferenceReasonPolicy
{
    public const string TraceOnlyNote = "فعلاً فقط برای ردیابی است؛ در حسابداری/P&L ثبت نشده.";
    public const string PotentialExpenseNote = "برای تبدیل به هزینه، فاز جدا لازم است.";

    public static string Label(DifferenceReason? reason) => reason switch
    {
        DifferenceReason.FxDifference => "تفاوت نرخ",
        DifferenceReason.Commission => "کمیشن",
        DifferenceReason.TransferFee => "کرایه حواله",
        DifferenceReason.BrokerMargin => "مارجین دلال",
        DifferenceReason.Discount => "تخفیف",
        DifferenceReason.Adjustment => "اصلاح حساب",
        DifferenceReason.Other => "سایر",
        _ => "دلیل مشخص نشده"
    };

    public static DifferencePolicyKind Kind(DifferenceReason? reason) => reason switch
    {
        DifferenceReason.FxDifference => DifferencePolicyKind.FxReview,
        DifferenceReason.Commission => DifferencePolicyKind.PotentialExpense,
        DifferenceReason.TransferFee => DifferencePolicyKind.PotentialExpense,
        DifferenceReason.BrokerMargin => DifferencePolicyKind.NeedsReview,
        DifferenceReason.Discount => DifferencePolicyKind.AdjustmentOnly,
        DifferenceReason.Adjustment => DifferencePolicyKind.AdjustmentOnly,
        DifferenceReason.Other => DifferencePolicyKind.NeedsReview,
        _ => DifferencePolicyKind.None
    };

    public static bool IsPotentialExpense(DifferenceReason? reason)
        => Kind(reason) == DifferencePolicyKind.PotentialExpense;

    // توضیح سیاست هر دلیل تفاوت — مطابق با خواسته‌ٔ فاز C1.
    public static string PolicyText(DifferenceReason? reason) => reason switch
    {
        DifferenceReason.FxDifference => "فقط گزارش/بررسی نرخ — در حسابداری ثبت نشده.",
        DifferenceReason.Commission => "هزینه احتمالی — فعلاً فقط ثبت توضیح؛ برای تبدیل به هزینه، فاز جدا لازم است.",
        DifferenceReason.TransferFee => "هزینه احتمالی — فعلاً فقط ثبت توضیح؛ برای تبدیل به هزینه، فاز جدا لازم است.",
        DifferenceReason.BrokerMargin => "نیاز به بررسی — فعلاً فقط ثبت می‌شود.",
        DifferenceReason.Discount => "فقط اصلاح/تخفیف — بدون ثبت حسابداری.",
        DifferenceReason.Adjustment => "فقط اصلاح توضیحی — بدون ثبت حسابداری.",
        DifferenceReason.Other => "نیاز به توضیح — فعلاً فقط ثبت می‌شود.",
        _ => "دلیل تفاوت مشخص نشده — لطفاً دلیل را انتخاب کنید."
    };
}
