namespace PTGOilSystem.Web.Models.ShipmentPnl;

/// <summary>
/// دسته‌های نمایشی مصارف پروندهٔ محموله. کارت‌های KPI، گروه‌های تب «مصارف و گمرک»
/// و جدول «جزئیات هزینه‌ها بر اساس دسته» همگی باید از همین enum و همین
/// <see cref="ShipmentExpenseCategorizer"/> تغذیه شوند تا اعداد یک منبع داشته باشند.
/// </summary>
public enum ShipmentExpenseCategory
{
    Freight,
    Customs,
    Terminal,
    Documents,
    Other
}

public sealed class ShipmentExpenseCategoryGroup
{
    public ShipmentExpenseCategory Category { get; init; }
    public IReadOnlyList<ShipmentExpenseDisplayRow> Rows { get; init; } = [];
    public decimal TotalUsd { get; init; }
}

/// <summary>
/// تنها محل دسته‌بندی مصارف محموله.
/// اولویت: ۱) پرچم گمرک، ۲) <c>ExpenseType.Category</c> از دیتابیس (داده‌ی ساخت‌یافته)،
/// ۳) واژه‌نامهٔ واحد روی نام نوع مصرف و شرح — فقط برای انواع قدیمی/دستهٔ نامشخص.
/// پیش‌تر این منطق دو بار و با دو فهرست واژهٔ ناهمسان داخل Razor تکرار شده بود.
/// </summary>
public static class ShipmentExpenseCategorizer
{
    private static readonly string[] FreightTerms = ["freight", "کرایه", "حمل"];
    private static readonly string[] TerminalTerms = ["terminal", "warehouse", "ترمینال", "انبار", "مخزن", "گدام"];
    private static readonly string[] DocumentTerms = ["document", "permit", "abzardiya", "سند", "اسناد", "ابزاردیه", "مجوز"];

    public static ShipmentExpenseCategory Categorize(ShipmentExpenseDisplayRow row)
    {
        if (row.IsCustoms)
        {
            return ShipmentExpenseCategory.Customs;
        }

        var byDbCategory = FromExpenseTypeCategory(row.ExpenseTypeCategory);
        if (byDbCategory.HasValue)
        {
            return byDbCategory.Value;
        }

        var text = $"{row.ExpenseTypeName} {row.Description}".ToLowerInvariant();
        if (Matches(text, FreightTerms))
        {
            return ShipmentExpenseCategory.Freight;
        }

        if (Matches(text, TerminalTerms))
        {
            return ShipmentExpenseCategory.Terminal;
        }

        if (Matches(text, DocumentTerms))
        {
            return ShipmentExpenseCategory.Documents;
        }

        return ShipmentExpenseCategory.Other;
    }

    /// <summary>
    /// گروه‌بندی ردیف‌های نمایشی به ترتیب ثابت UI
    /// (کرایه، گمرک، ترمینال، اسناد، سایر) — گروه‌های خالی حذف می‌شوند.
    /// </summary>
    public static IReadOnlyList<ShipmentExpenseCategoryGroup> Group(
        IReadOnlyList<ShipmentExpenseDisplayRow> rows)
        => new[]
            {
                ShipmentExpenseCategory.Freight,
                ShipmentExpenseCategory.Customs,
                ShipmentExpenseCategory.Terminal,
                ShipmentExpenseCategory.Documents,
                ShipmentExpenseCategory.Other
            }
            .Select(category =>
            {
                var categoryRows = rows.Where(row => row.Category == category).ToList();
                return new ShipmentExpenseCategoryGroup
                {
                    Category = category,
                    Rows = categoryRows,
                    TotalUsd = categoryRows.Sum(row => row.AmountUsd)
                };
            })
            .Where(group => group.Rows.Count > 0)
            .ToList();

    public static decimal TotalFor(
        IReadOnlyList<ShipmentExpenseCategoryGroup> groups,
        ShipmentExpenseCategory category)
        => groups.FirstOrDefault(group => group.Category == category)?.TotalUsd ?? 0m;

    private static ShipmentExpenseCategory? FromExpenseTypeCategory(string? dbCategory)
    {
        if (string.IsNullOrWhiteSpace(dbCategory))
        {
            return null;
        }

        // مقادیر شناخته‌شدهٔ ستون ExpenseType.Category (MasterData): Transport، Storage،
        // Commission، Other و … . «Other» عمداً به fallback واژه‌نامه می‌رود چون مقدار
        // پیش‌فرض ستون است و دربارهٔ ماهیت مصرف چیزی نمی‌گوید.
        return dbCategory.Trim().ToLowerInvariant() switch
        {
            "transport" or "trucking" or "freight" => ShipmentExpenseCategory.Freight,
            "storage" or "terminal" or "warehouse" => ShipmentExpenseCategory.Terminal,
            "documents" or "document" or "permit" => ShipmentExpenseCategory.Documents,
            "customs" => ShipmentExpenseCategory.Customs,
            "commission" => ShipmentExpenseCategory.Other,
            _ => null
        };
    }

    private static bool Matches(string text, string[] terms)
        => terms.Any(term => text.Contains(term, StringComparison.Ordinal));
}
