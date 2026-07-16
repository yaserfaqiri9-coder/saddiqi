namespace PTGOilSystem.Web.Services.Accounting;

/// <summary>
/// وضعیت آمادگیِ Cutover.
///
/// تفاوت Blocked و OperationalDataValidationRequired عمدی است: Blocked یعنی چیزی در همین repo
/// اثبات‌پذیر خراب است (تنظیمات نیست، حساب متعلق به شرکت دیگری است، سند نامتوازن است).
/// OperationalDataValidationRequired یعنی کد آماده است ولی قضاوت به داده‌ی عملیاتی نیاز دارد که
/// در repo وجود ندارد — این را نباید با حدس‌زدن به Ready تبدیل کرد.
/// </summary>
public enum AccountingReadinessStatus
{
    Ready = 0,
    Warning = 1,
    OperationalDataValidationRequired = 2,
    Blocked = 3
}

public enum AccountingReadinessSeverity
{
    Info = 0,
    Warning = 1,
    OperationalDataValidationRequired = 2,
    Blocker = 3
}

/// <summary>
/// یک یافتهٔ Readiness. <paramref name="SampleRecords"/> عمداً محدود است — گزارش برای تصمیم‌گیری
/// است، نه استخراج داده.
/// </summary>
public sealed record AccountingReadinessFinding(
    string Code,
    string Title,
    string Description,
    AccountingReadinessSeverity Severity,
    int? CompanyId,
    int RecordCount,
    IReadOnlyList<string> SampleRecords,
    string RequiredAction,
    string? FeatureFlag)
{
    public const int MaxSamples = 10;
}

/// <summary>
/// وضعیت هر Adapter: Flag آن، تعداد رکوردهای نامزدِ ثبت، و — وقتی Flag خاموش است — دلیلی که
/// همهٔ آن رکوردها هم‌اکنون Skip می‌شوند. شمارشِ Skipهای واقعی به تفکیک Reason Code فقط از لاگِ
/// «pilot comparison» یک اجرای واقعی به‌دست می‌آید؛ Skipها در دیتابیس ذخیره نمی‌شوند.
/// </summary>
public sealed record AccountingAdapterReadiness(
    string Adapter,
    string SourceModule,
    string FeatureFlag,
    bool FeatureFlagEnabled,
    int CandidateRecordCount,
    int PostedJournalCount,
    string? ProjectedSkipReason);

public sealed record AccountingCompanyReadiness(
    int CompanyId,
    string CompanyName,
    AccountingReadinessStatus Status,
    IReadOnlyList<AccountingReadinessFinding> Findings);

/// <summary>
/// گزارش فقط‌خواندنی. هیچ چیزی را تغییر نمی‌دهد، هیچ Flag را روشن نمی‌کند و هیچ Migration اجرا
/// نمی‌کند — فقط می‌گوید Cutover چه چیزهایی کم دارد.
/// </summary>
public sealed record AccountingReadinessReport(
    DateTime GeneratedAtUtc,
    bool AccountingEnabled,
    AccountingReadinessStatus OverallStatus,
    IReadOnlyList<string> PendingMigrations,
    IReadOnlyList<AccountingAdapterReadiness> Adapters,
    IReadOnlyList<AccountingReadinessFinding> GlobalFindings,
    IReadOnlyList<AccountingCompanyReadiness> Companies)
{
    public IEnumerable<AccountingReadinessFinding> Blockers
        => GlobalFindings.Concat(Companies.SelectMany(c => c.Findings))
            .Where(f => f.Severity == AccountingReadinessSeverity.Blocker);
}
