namespace PTGOilSystem.Web.Models.Reconciliation;

// D2 — «پول‌های معلق / Suspense Money».
// همهٔ این ViewModelها فقط برای نمایش read-only هستند. هیچ پول/Ledger/Payment
// از این مسیر ساخته یا تغییر داده نمی‌شود؛ فقط مواردی را که از قبل ثبت شده‌اند
// و لینک تجاری‌شان ناقص است، برای بررسی دستی نشان می‌دهد.

public enum SuspenseSeverity
{
    Warning = 1,
    Critical = 2
}

public sealed class SuspenseMoneyItemViewModel
{
    public DateTime Date { get; init; }

    /// <summary>نوع سند به زبان فارسی (مثلاً «رزنامچه — پرداخت تأمین‌کننده»).</summary>
    public string DocumentType { get; init; } = "";

    /// <summary>مبلغ سند به ارز اصلی سند.</summary>
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public decimal AmountUsd { get; init; }

    /// <summary>طرف حساب اگر مشخص باشد، در غیر این صورت null.</summary>
    public string? CounterpartyName { get; init; }

    /// <summary>شماره قرارداد اگر مشخص باشد.</summary>
    public string? ContractNumber { get; init; }

    /// <summary>منبع/عنوان کوتاه مشکل (مثلاً «طرف حساب نامشخص»).</summary>
    public string IssueSource { get; init; } = "";

    /// <summary>توضیح فارسی ساده برای کاربر.</summary>
    public string PlainExplanation { get; init; } = "";

    public SuspenseSeverity Severity { get; init; } = SuspenseSeverity.Warning;

    // لینک «جزئیات» به سند اصلی (فقط نمایش).
    public string? DetailsController { get; init; }
    public string? DetailsAction { get; init; }
    public int? DetailsRouteId { get; init; }

    // لینک «وصل کن» فقط اگر مسیر اصلاح دستی موجود باشد (مثلاً ویرایش رزنامچه).
    // هیچ auto-fix انجام نمی‌شود؛ این فقط کاربر را به فرم موجود می‌برد.
    public string? ConnectController { get; init; }
    public string? ConnectAction { get; init; }
    public int? ConnectRouteId { get; init; }

    public bool HasConnectPath =>
        !string.IsNullOrEmpty(ConnectController) && !string.IsNullOrEmpty(ConnectAction);
}

public sealed class SuspenseMoneyViewModel
{
    public IReadOnlyList<SuspenseMoneyItemViewModel> Items { get; init; } = [];

    // فیلتر فعلی (فقط برای حفظ وضعیت فرم؛ read-only).
    public string? SelectedSeverity { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }

    public int CriticalCount => Items.Count(i => i.Severity == SuspenseSeverity.Critical);
    public int WarningCount => Items.Count(i => i.Severity == SuspenseSeverity.Warning);
    public int TotalCount => Items.Count;
    public decimal TotalAmountUsd => Items.Sum(i => i.AmountUsd);
}
