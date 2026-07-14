namespace PTGOilSystem.Web.Configuration;

/// <summary>
/// Feature flags برای لایهٔ Inventory Lineage (فاز ۲). از بخش "Lineage" در پیکربندی خوانده می‌شود.
/// همهٔ پیش‌فرض‌ها false هستند تا بدون فعال‌سازی صریح، رفتار سیستم دقیقاً مثل قبل بماند.
/// </summary>
public sealed class LineageOptions
{
    public const string SectionName = "Lineage";

    /// <summary>هنگام بارگیری/رسید/فروش/کسری/مصرف، رکوردهای Lot و allocation نوشته شوند.</summary>
    public bool WriteLots { get; set; }

    /// <summary>پرونده کشتی (ShipmentPnl) محاسبه را از لایهٔ Lineage بخواند (با fallback به منطق فعلی).</summary>
    public bool UseInPnl { get; set; }

    /// <summary>اجازهٔ اجرای backfill دادهٔ قدیمی (فقط دستی توسط Admin؛ هرگز خودکار).</summary>
    public bool BackfillEnabled { get; set; }
}
