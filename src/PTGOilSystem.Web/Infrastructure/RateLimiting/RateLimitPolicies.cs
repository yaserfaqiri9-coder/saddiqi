namespace PTGOilSystem.Web.Infrastructure.RateLimiting;

/// <summary>
/// نام سیاست‌های محدودسازی نرخ درخواست. روی مسیرهای سنگین (خروجی CSV و گزارش‌های مدیریتی)
/// اعمال می‌شود تا یک کاربر با کلیک‌های پی‌درپی، Connection Pool را تمام نکند.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>خروجی‌های CSV — سنگین‌ترین مسیرها از نظر حافظه و زمان.</summary>
    public const string CsvExport = "csv-export";

    /// <summary>گزارش‌های مدیریتی — چندین کوئری سنگین در هر درخواست.</summary>
    public const string HeavyReport = "heavy-report";
}
