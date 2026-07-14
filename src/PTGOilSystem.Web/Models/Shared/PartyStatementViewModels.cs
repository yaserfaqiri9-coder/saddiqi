namespace PTGOilSystem.Web.Models.Shared;

// مدل ردیف صورت‌حساب مشترک (فقط نمایش/خواندنی) برای یک‌دست‌کردن جدول حساب
// تأمین‌کننده و مشتری. هیچ محاسبهٔ مالی اینجا انجام نمی‌شود؛ همهٔ مقادیر و
// برچسب‌ها در View صدازننده از روی ردیف‌های موجود ساخته و پاس داده می‌شوند.
public sealed class PartyStatementRowViewModel
{
    public int LedgerEntryId { get; init; }
    public System.DateTime Date { get; init; }
    public string Type { get; init; } = string.Empty;

    public string? Reference { get; init; }
    public string? SourceDetailsController { get; init; }
    public string? SourceDetailsAction { get; init; }
    public int? SourceDetailsRouteId { get; init; }

    public string? ContractNumber { get; init; }
    public string Description { get; init; } = string.Empty;

    // مبلغ سند به ارز اصلی + کد ارز
    public decimal? SourceAmount { get; init; }
    public string Currency { get; init; } = "USD";

    // نرخ ارز برای نمایش (ارز اصلی در برابر ۱ دالر) و نرخ داخلی برای tooltip
    public decimal? FxRateDisplayPerUsd { get; init; }
    public decimal? FxRateUsed { get; init; }

    // اثر روی حساب (برچسب/کلاس از قبل در View تعیین می‌شود تا معناشناسی هر طرف حفظ شود)
    // (برای جدول‌های خلاصهٔ «آخرین حرکت‌ها» نگه داشته می‌شود؛ جدول اصلی از Credit/Debit زیر استفاده می‌کند.)
    public string EffectLabel { get; init; } = string.Empty;
    public string EffectClass { get; init; } = "is-neutral";
    public string SignedUsd { get; init; } = "-";

    // ── قاعدهٔ یک‌دستِ «داده/گرفته» (سبک BNK-HIMOR) ──
    // StatementCredit (داده‌شده) = چیزی که شرکت به طرف مقابل داده است (پرداخت، فروش جنس و …)
    // StatementDebit  (گرفته‌شده) = چیزی که شرکت از طرف مقابل گرفته است (بار، خدمت، مصرف و …)
    // فقط نمایش است؛ هیچ محاسبهٔ Ledger/Payment/FX اینجا انجام نمی‌شود.
    public decimal? StatementCreditUsd { get; init; }
    public decimal? StatementDebitUsd { get; init; }

    // مانده تجمعی USD + برچسب جهت مانده مخصوص همان طرف حساب (مانده = Σ(داده − گرفته))
    public decimal RunningBalanceUsd { get; init; }
    public string BalanceLabel { get; init; } = string.Empty;
    public string BalanceClass { get; init; } = string.Empty;
}

// مدل جدول صورت‌حساب مشترک: ردیف‌ها + متن حالت خالی مخصوص هر صفحه.
public sealed class PartyStatementTableViewModel
{
    public System.Collections.Generic.IReadOnlyList<PartyStatementRowViewModel> Rows { get; init; } = [];
    public string EmptyTitle { get; init; } = "صورت‌حساب خالی است";
    public string EmptyMessage { get; init; } = "هنوز سند مالی برای این طرف حساب ثبت نشده است.";

    // عنوان ستون‌های Credit/Debit؛ به‌صورت پیش‌فرض «داده‌شده/گرفته‌شده». طرف‌هایی با
    // معناشناسی متفاوت (مثلاً سهم شریک) می‌توانند آن را بازنویسی کنند.
    public string CreditHeader { get; init; } = "داده‌شده";
    public string DebitHeader { get; init; } = "گرفته‌شده";
    public string CreditTooltip { get; init; } = "چیزی که شرکت به این طرف داده است (پرداخت/فروش/انتقال)";
    public string DebitTooltip { get; init; } = "چیزی که شرکت از این طرف گرفته است (بار/خدمت/مصرف/دموراژ)";
}
