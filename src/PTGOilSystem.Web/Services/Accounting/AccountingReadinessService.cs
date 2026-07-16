using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public interface IAccountingReadinessService
{
    Task<AccountingReadinessReport> BuildAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// مرحله ۹ — آیا هر شرکت آمادهٔ Cutover هست یا نه.
///
/// این سرویس فقط می‌خواند. هیچ Flag را روشن نمی‌کند، هیچ Migration اجرا نمی‌کند، هیچ Backfill
/// نمی‌نویسد و هیچ داده‌ی legacy را دست نمی‌زند. جواب هر بررسی یا از دیتابیس اثبات می‌شود یا
/// صریحاً OperationalDataValidationRequired علامت می‌خورد — چیزی حدس زده نمی‌شود.
///
/// آمادگی «هر شرکت جداگانه» است چون تنظیمات، حساب‌ها، سال مالی و مالکیت رکوردها همه per-company
/// هستند: یک شرکت می‌تواند آمادهٔ Cutover باشد در حالی که شرکت دیگر هنوز حساب فعال ندارد.
/// </summary>
public sealed class AccountingReadinessService(
    ApplicationDbContext db,
    IOptions<AccountingOptions> options) : IAccountingReadinessService
{
    private readonly AccountingOptions _options = options.Value;

    public async Task<AccountingReadinessReport> BuildAsync(CancellationToken cancellationToken = default)
    {
        var globalFindings = new List<AccountingReadinessFinding>();

        var pendingMigrations = await GetPendingMigrationsAsync(globalFindings, cancellationToken);
        AddAccountingEnabledFinding(globalFindings);
        await AddDuplicateSourceEventFindingsAsync(globalFindings, cancellationToken);
        AddFullSuiteFinding(globalFindings);
        AddSkipHarvestFinding(globalFindings);

        var adapters = await BuildAdapterReadinessAsync(cancellationToken);

        var companies = await db.Companies.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);

        var companyReports = new List<AccountingCompanyReadiness>();
        foreach (var company in companies)
        {
            var findings = await BuildCompanyFindingsAsync(company.Id, cancellationToken);
            companyReports.Add(new AccountingCompanyReadiness(
                company.Id,
                company.Name,
                Aggregate(findings),
                findings));
        }

        var overall = Aggregate(globalFindings.Concat(companyReports.SelectMany(c => c.Findings)).ToList());

        return new AccountingReadinessReport(
            DateTime.UtcNow,
            _options.Enabled,
            overall,
            pendingMigrations,
            adapters,
            globalFindings,
            companyReports);
    }

    // بدترین severity وضعیت را تعیین می‌کند: یک Blocker کل شرکت را Blocked می‌کند.
    private static AccountingReadinessStatus Aggregate(IReadOnlyList<AccountingReadinessFinding> findings)
    {
        if (findings.Any(f => f.Severity == AccountingReadinessSeverity.Blocker))
            return AccountingReadinessStatus.Blocked;
        if (findings.Any(f => f.Severity == AccountingReadinessSeverity.OperationalDataValidationRequired))
            return AccountingReadinessStatus.OperationalDataValidationRequired;
        if (findings.Any(f => f.Severity == AccountingReadinessSeverity.Warning))
            return AccountingReadinessStatus.Warning;
        return AccountingReadinessStatus.Ready;
    }

    // ۱۵ — Migrationهای اجرانشده. فقط گزارش؛ اجرای آن‌ها تصمیم صریح کاربر است.
    private async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
        List<AccountingReadinessFinding> findings,
        CancellationToken cancellationToken)
    {
        List<string> pending;
        try
        {
            pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        }
        catch (Exception ex)
        {
            // Provider درون‌حافظه‌ای Migration ندارد؛ نبودِ جواب نباید گزارش را از کار بیندازد.
            findings.Add(new AccountingReadinessFinding(
                "MIGRATIONS_UNKNOWN",
                "وضعیت Migrationها قابل خواندن نیست",
                $"پرس‌وجوی Migrationهای اجرانشده ممکن نشد: {ex.GetType().Name}.",
                AccountingReadinessSeverity.OperationalDataValidationRequired,
                CompanyId: null,
                RecordCount: 0,
                Array.Empty<string>(),
                "این گزارش را روی همان دیتابیسِ هدف (یا Backup آن) اجرا کنید.",
                FeatureFlag: null));
            return Array.Empty<string>();
        }

        if (pending.Count > 0)
        {
            findings.Add(new AccountingReadinessFinding(
                "MIGRATIONS_PENDING",
                "Migration اجرانشده وجود دارد",
                $"{pending.Count} Migration روی این دیتابیس اجرا نشده است. تا وقتی اجرا نشوند، "
                    + "جدول‌های حسابداری با کد هم‌خوان نیستند.",
                AccountingReadinessSeverity.Blocker,
                CompanyId: null,
                pending.Count,
                Sample(pending),
                "با اجازهٔ صریح و روی Backup تأییدشده اجرا شوند. هرگز خودکار اجرا نشوند.",
                FeatureFlag: null));
        }

        return pending;
    }

    // ۱ — کلید اصلی. تا وقتی خاموش است، همهٔ Adapterها Skip می‌کنند و دفتر کل جدید خالی می‌ماند.
    private void AddAccountingEnabledFinding(List<AccountingReadinessFinding> findings)
    {
        if (_options.Enabled)
            return;

        findings.Add(new AccountingReadinessFinding(
            "ACCOUNTING_DISABLED",
            "حسابداری دوطرفه خاموش است",
            "Accounting.Enabled خاموش است، پس هیچ Adapter چیزی ثبت نمی‌کند (Skip با ACCOUNTING_DISABLED). "
                + "این وضعیت عمدی است و تا پیش از Cutover باید خاموش بماند.",
            AccountingReadinessSeverity.Info,
            CompanyId: null,
            RecordCount: 0,
            Array.Empty<string>(),
            "در زمان Cutover و فقط با اجازهٔ صریح روشن شود.",
            "Accounting.Enabled"));
    }

    // ۱۴ — SourceEventId تکراری. کلیدِ Idempotency است: تکراری یعنی یک رویداد دو سند گرفته.
    private async Task AddDuplicateSourceEventFindingsAsync(
        List<AccountingReadinessFinding> findings,
        CancellationToken cancellationToken)
    {
        var duplicates = await db.JournalEntries.AsNoTracking()
            .Where(j => j.SourceEventId != null)
            .GroupBy(j => new { j.CompanyId, j.SourceEventId })
            .Where(g => g.Count() > 1)
            .Select(g => new { g.Key.CompanyId, g.Key.SourceEventId, Count = g.Count() })
            .ToListAsync(cancellationToken);

        if (duplicates.Count == 0)
            return;

        findings.Add(new AccountingReadinessFinding(
            "DUPLICATE_SOURCE_EVENT_ID",
            "SourceEventId تکراری در دفتر کل جدید",
            "هر رویداد باید دقیقاً یک سند داشته باشد. تکراری‌بودن یعنی Idempotency شکسته و مبلغ دوبار ثبت شده.",
            AccountingReadinessSeverity.Blocker,
            CompanyId: null,
            duplicates.Count,
            Sample(duplicates.Select(d => $"CompanyId={d.CompanyId}, SourceEventId={d.SourceEventId}, Count={d.Count}")),
            "اسناد تکراری بررسی و با Reversal اصلاح شوند؛ رکورد Posted ویرایش نمی‌شود.",
            FeatureFlag: null));
    }

    // ۱۶ — Full Suite. نتیجهٔ تست از داخل برنامه قابل اثبات نیست؛ ادعای اجرانشده نمی‌کنیم.
    private static void AddFullSuiteFinding(List<AccountingReadinessFinding> findings)
        => findings.Add(new AccountingReadinessFinding(
            "FULL_SUITE_EXTERNAL_EVIDENCE",
            "وضعیت Full Suite از بیرونِ برنامه می‌آید",
            "نتیجهٔ تست‌ها حالتِ Runtime نیست و از دیتابیس خوانده نمی‌شود. معیار عبورِ ثبت‌شده: "
                + "Build بدون خطا و همان ۱۸ شکست baseline بدون شکست جدید، روی یک Worktree تمیز از "
                + "آخرین Commit حسابداری.",
            AccountingReadinessSeverity.OperationalDataValidationRequired,
            CompanyId: null,
            RecordCount: 0,
            Array.Empty<string>(),
            "پیش از Cutover، Full Suite روی Worktree تمیز اجرا و نتیجه‌اش ضمیمه شود.",
            FeatureFlag: null));

    // ۱۷ — Skipها لاگ می‌شوند، نه ذخیره. شمارشِ واقعی به تفکیک Reason Code فقط از لاگ می‌آید.
    private static void AddSkipHarvestFinding(List<AccountingReadinessFinding> findings)
        => findings.Add(new AccountingReadinessFinding(
            "SKIP_COUNTS_REQUIRE_LOG_HARVEST",
            "شمارش Skipها به تفکیک Reason Code از لاگ به‌دست می‌آید",
            "هیچ Adapter دلیل Skip را در دیتابیس ذخیره نمی‌کند؛ هر کدام یک خط «pilot comparison» با "
                + "SkipOrFailureReason لاگ می‌کنند. پس شمارش دقیق فقط از لاگِ یک اجرای واقعی با Flag روشن "
                + "به‌دست می‌آید. آنچه اینجا از دیتابیس اثبات می‌شود، در فهرست Adapters آمده: وضعیت Flag، "
                + "تعداد رکورد نامزد، تعداد سند پست‌شده، و دلیلِ Skipِ قابل‌اثبات وقتی Flag خاموش است.",
            AccountingReadinessSeverity.OperationalDataValidationRequired,
            CompanyId: null,
            RecordCount: 0,
            Array.Empty<string>(),
            "روی Backup دیتابیس عملیاتی با Flag روشن اجرا شود و SkipOrFailureReason از لاگ گروه‌بندی شود.",
            FeatureFlag: null));

    /// <summary>
    /// وضعیت هر Adapter از روی دیتابیس. وقتی Flag خاموش است، دلیلِ Skipِ همهٔ رکوردهای نامزد
    /// قابل‌اثبات است (ACCOUNTING_DISABLED یا PILOT_DISABLED) — این حدس نیست، خودِ گاردِ Adapter است.
    /// </summary>
    private async Task<IReadOnlyList<AccountingAdapterReadiness>> BuildAdapterReadinessAsync(
        CancellationToken cancellationToken)
    {
        var postedByModule = await db.JournalEntries.AsNoTracking()
            .GroupBy(j => j.SourceModule)
            .Select(g => new { Module = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var candidates = new (string Adapter, string Module, string Flag, bool Enabled, int Count)[]
        {
            ("ContractBalanceTransferAccountingAdapter", ContractBalanceTransferAccountingAdapter.SourceModule,
                "Accounting:Pilots:ContractBalanceTransfer", _options.Pilots.ContractBalanceTransfer,
                await db.ContractBalanceTransfers.AsNoTracking().CountAsync(cancellationToken)),
            ("SupplierPaymentAllocationAccountingAdapter", SupplierPaymentAllocationAccountingAdapter.SourceModule,
                "Accounting:Pilots:SupplierPaymentAllocation", _options.Pilots.SupplierPaymentAllocation,
                await db.SupplierPaymentAllocations.AsNoTracking().CountAsync(cancellationToken)),
            ("PaymentAccountingAdapter", PaymentAccountingAdapter.SourceModule,
                "Accounting:Pilots:CustomerReceipt/SupplierPayment/…", PaymentPilotsAnyEnabled(),
                await db.PaymentTransactions.AsNoTracking().CountAsync(cancellationToken)),
            ("ExpenseAccountingAdapter", ExpenseAccountingAdapter.SourceModule,
                "Accounting:Pilots:Expense", _options.Pilots.Expense,
                await db.ExpenseTransactions.AsNoTracking().CountAsync(cancellationToken)),
            ("PurchaseAccountingAdapter", PurchaseAccountingAdapter.SourceModule,
                "Accounting:Pilots:Purchase", _options.Pilots.Purchase,
                await db.LoadingRegisters.AsNoTracking().CountAsync(cancellationToken)),
            ("SalesAccountingAdapter", SalesAccountingAdapter.SourceModule,
                "Accounting:Pilots:Sale + Cogs", _options.Pilots.Sale || _options.Pilots.Cogs,
                await db.SalesTransactions.AsNoTracking().CountAsync(cancellationToken)),
            ("InventoryLossAccountingAdapter", InventoryLossAccountingAdapter.SourceModule,
                "Accounting:Pilots:InventoryLoss", _options.Pilots.InventoryLoss,
                await db.LossEvents.AsNoTracking().CountAsync(cancellationToken)),
            ("ShortageChargeAccountingAdapter", ShortageChargeAccountingAdapter.SourceModule,
                "Accounting:Pilots:ShortageCharge", _options.Pilots.ShortageCharge,
                await db.InventoryTransportReceipts.AsNoTracking().CountAsync(cancellationToken)),
            ("SarrafSettlementAccountingAdapter", SarrafSettlementAccountingAdapter.SourceModule,
                "Accounting:Pilots:SarrafSettlement", _options.Pilots.SarrafSettlement,
                await db.SarrafSettlements.AsNoTracking().CountAsync(cancellationToken)),
            ("ThreeWaySettlementAccountingAdapter", ThreeWaySettlementAccountingAdapter.SourceModule,
                "Accounting:Pilots:ThreeWaySettlement", _options.Pilots.ThreeWaySettlement,
                await db.ThreeWaySettlements.AsNoTracking().CountAsync(cancellationToken)),
            ("InventoryTransferAccountingAdapter", InventoryTransferAccountingAdapter.SourceModule,
                "Accounting:Pilots:InventoryTransfer", _options.Pilots.InventoryTransfer,
                await db.InventoryTransportLegs.AsNoTracking().CountAsync(cancellationToken))
        };

        return candidates
            .Select(c => new AccountingAdapterReadiness(
                c.Adapter,
                c.Module,
                c.Flag,
                c.Enabled,
                c.Count,
                postedByModule.SingleOrDefault(p => p.Module == c.Module)?.Count ?? 0,
                ProjectedSkipReason(c.Enabled)))
            .ToList();
    }

    private bool PaymentPilotsAnyEnabled()
        => _options.Pilots.CustomerReceipt
            || _options.Pilots.CustomerAdvance
            || _options.Pilots.SupplierPayment
            || _options.Pilots.SupplierPrepayment
            || _options.Pilots.SarrafPayment
            || _options.Pilots.ExpensePayment
            || _options.Pilots.CommissionPayment;

    private string? ProjectedSkipReason(bool pilotEnabled)
        => !_options.Enabled ? "ACCOUNTING_DISABLED"
            : !pilotEnabled ? "PILOT_DISABLED"
            : null;

    private async Task<IReadOnlyList<AccountingReadinessFinding>> BuildCompanyFindingsAsync(
        int companyId,
        CancellationToken cancellationToken)
    {
        var findings = new List<AccountingReadinessFinding>();

        // ۲ و ۳ — بدون تنظیمات معتبر هیچ Mapping حسابی وجود ندارد، پس بقیهٔ بررسی‌ها بی‌معنی‌اند.
        var settings = await db.AccountingSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);
        if (settings is null)
        {
            findings.Add(new AccountingReadinessFinding(
                "ACCOUNTING_SETTINGS_MISSING",
                "AccountingSettings برای این شرکت وجود ندارد",
                "هر Adapter بدون تنظیمات با ACCOUNTING_SETTINGS_MISSING رد می‌شود.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                RecordCount: 1,
                Array.Empty<string>(),
                "AccountingSettings این شرکت با Seeder ساخته و حساب‌هایش بازبینی شود.",
                FeatureFlag: null));
            return findings;
        }

        AddFunctionalCurrencyFinding(findings, companyId, settings);
        await AddAccountFindingsAsync(findings, companyId, settings, cancellationToken);
        await AddFiscalFindingsAsync(findings, companyId, cancellationToken);
        await AddOwnershipFindingsAsync(findings, companyId, cancellationToken);
        await AddExpenseTypeFindingsAsync(findings, companyId, cancellationToken);
        await AddCustomerAdvanceFindingsAsync(findings, companyId, cancellationToken);
        await AddInventoryValuationFindingsAsync(findings, companyId, cancellationToken);
        await AddSalesCostFindingsAsync(findings, companyId, cancellationToken);
        await AddTransferFindingsAsync(findings, companyId, cancellationToken);
        await AddJournalIntegrityFindingsAsync(findings, companyId, cancellationToken);
        AddPurchaseComparisonFinding(findings, companyId);
        await AddSarrafComparisonFindingAsync(findings, companyId, cancellationToken);

        return findings;
    }

    private static void AddFunctionalCurrencyFinding(
        List<AccountingReadinessFinding> findings,
        int companyId,
        AccountingSettings settings)
    {
        if (string.Equals(settings.FunctionalCurrencyCode?.Trim(), "USD", StringComparison.OrdinalIgnoreCase))
            return;

        findings.Add(new AccountingReadinessFinding(
            "UNSUPPORTED_FUNCTIONAL_CURRENCY",
            "ارز عملیاتی شرکت USD نیست",
            $"ارز عملیاتی «{settings.FunctionalCurrencyCode}» است. همهٔ Adapterها فقط USD را می‌شناسند و "
                + "بقیه را با UNSUPPORTED_FUNCTIONAL_CURRENCY رد می‌کنند.",
            AccountingReadinessSeverity.Blocker,
            companyId,
            RecordCount: 1,
            Array.Empty<string>(),
            "ارز عملیاتی به USD اصلاح شود، یا پشتیبانی ارز دیگر با تصمیم صریح تعریف شود.",
            FeatureFlag: null));
    }

    // ۳ — حساب‌های لازم موجود، فعال، و متعلق به همین شرکت.
    // مالکیت مهم‌تر از وجود است: حسابِ فعالِ متعلق به شرکت دیگر یعنی سند به دفتر شرکت اشتباه می‌رود.
    private async Task AddAccountFindingsAsync(
        List<AccountingReadinessFinding> findings,
        int companyId,
        AccountingSettings settings,
        CancellationToken cancellationToken)
    {
        var required = RequiredAccountIds(settings);
        var requiredIds = required.Select(r => r.AccountId).Distinct().ToList();
        var accounts = await db.Accounts.AsNoTracking()
            .Where(a => requiredIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Code, a.CompanyId, a.IsActive })
            .ToListAsync(cancellationToken);

        var missing = required.Where(r => accounts.All(a => a.Id != r.AccountId)).ToList();
        if (missing.Count > 0)
        {
            findings.Add(new AccountingReadinessFinding(
                "REQUIRED_ACCOUNT_MISSING",
                "حساب لازم در جدول حساب‌ها نیست",
                "AccountingSettings به حسابی اشاره می‌کند که وجود ندارد.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                missing.Count,
                Sample(missing.Select(m => $"{m.Name} → AccountId={m.AccountId}")),
                "حساب‌های گم‌شده با Seeder ساخته یا ارجاعِ تنظیمات اصلاح شود.",
                FeatureFlag: null));
        }

        var inactive = required
            .Where(r => accounts.Any(a => a.Id == r.AccountId && !a.IsActive))
            .ToList();
        if (inactive.Count > 0)
        {
            findings.Add(new AccountingReadinessFinding(
                "REQUIRED_ACCOUNT_INACTIVE",
                "حساب لازم غیرفعال است",
                "حساب غیرفعال پذیرفته نمی‌شود و Adapter سند را رد می‌کند.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                inactive.Count,
                Sample(inactive.Select(m => $"{m.Name} → AccountId={m.AccountId}")),
                "حساب‌ها فعال شوند.",
                FeatureFlag: null));
        }

        var foreign = required
            .Where(r => accounts.Any(a => a.Id == r.AccountId && a.CompanyId != companyId))
            .ToList();
        if (foreign.Count > 0)
        {
            findings.Add(new AccountingReadinessFinding(
                "REQUIRED_ACCOUNT_WRONG_COMPANY",
                "حساب لازم متعلق به شرکت دیگری است",
                "تنظیماتِ این شرکت به حسابِ شرکت دیگری اشاره می‌کند؛ سند به دفتر اشتباه می‌نشیند.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                foreign.Count,
                Sample(foreign.Select(m =>
                {
                    var account = accounts.Single(a => a.Id == m.AccountId);
                    return $"{m.Name} → AccountId={m.AccountId}, Code={account.Code}, OwnerCompanyId={account.CompanyId}";
                })),
                "ارجاع به حسابِ همین شرکت اصلاح شود.",
                FeatureFlag: null));
        }
    }

    private static IReadOnlyList<(string Name, int AccountId)> RequiredAccountIds(AccountingSettings s)
        => new (string, int)[]
        {
            (nameof(s.CashBankControlAccountId), s.CashBankControlAccountId),
            (nameof(s.AccountsReceivableAccountId), s.AccountsReceivableAccountId),
            (nameof(s.AccountsPayableAccountId), s.AccountsPayableAccountId),
            (nameof(s.InventoryAccountId), s.InventoryAccountId),
            (nameof(s.InventoryInTransitAccountId), s.InventoryInTransitAccountId),
            (nameof(s.SupplierPrepaymentAccountId), s.SupplierPrepaymentAccountId),
            (nameof(s.CustomerAdvanceAccountId), s.CustomerAdvanceAccountId),
            (nameof(s.FreightPayableAccountId), s.FreightPayableAccountId),
            (nameof(s.CommissionPayableAccountId), s.CommissionPayableAccountId),
            (nameof(s.EmployeeAdvanceAccountId), s.EmployeeAdvanceAccountId),
            (nameof(s.EmployeePayableAccountId), s.EmployeePayableAccountId),
            (nameof(s.AccruedExpenseAccountId), s.AccruedExpenseAccountId),
            (nameof(s.SalesRevenueAccountId), s.SalesRevenueAccountId),
            (nameof(s.CostOfGoodsSoldAccountId), s.CostOfGoodsSoldAccountId),
            (nameof(s.GeneralExpenseAccountId), s.GeneralExpenseAccountId),
            (nameof(s.ExchangeGainAccountId), s.ExchangeGainAccountId),
            (nameof(s.ExchangeLossAccountId), s.ExchangeLossAccountId),
            (nameof(s.InventoryLossAccountId), s.InventoryLossAccountId),
            (nameof(s.CurrentYearProfitLossAccountId), s.CurrentYearProfitLossAccountId),
            (nameof(s.RetainedEarningsAccountId), s.RetainedEarningsAccountId)
        };

    // ۴ — سال مالی و دورهٔ باز. بدون آن‌ها PeriodGuard هر سند را رد می‌کند.
    private async Task AddFiscalFindingsAsync(
        List<AccountingReadinessFinding> findings,
        int companyId,
        CancellationToken cancellationToken)
    {
        var openYears = await db.FiscalYears.AsNoTracking()
            .Where(y => y.CompanyId == companyId && y.Status == FiscalYearStatus.Open)
            .Select(y => new { y.Id, y.Name })
            .ToListAsync(cancellationToken);

        if (openYears.Count == 0)
        {
            findings.Add(new AccountingReadinessFinding(
                "NO_OPEN_FISCAL_YEAR",
                "سال مالی باز وجود ندارد",
                "بدون سال مالیِ باز هیچ سندی تاریخ حسابداری معتبر نمی‌گیرد.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                RecordCount: 0,
                Array.Empty<string>(),
                "سال مالی Cutover ساخته و باز شود.",
                FeatureFlag: null));
            return;
        }

        var openYearIds = openYears.Select(y => y.Id).ToList();
        var openPeriods = await db.FiscalPeriods.AsNoTracking()
            .CountAsync(
                p => p.CompanyId == companyId
                    && openYearIds.Contains(p.FiscalYearId)
                    && p.Status == FiscalPeriodStatus.Open,
                cancellationToken);

        if (openPeriods == 0)
        {
            findings.Add(new AccountingReadinessFinding(
                "NO_OPEN_FISCAL_PERIOD",
                "دورهٔ مالی باز وجود ندارد",
                "سال مالی باز است ولی هیچ دورهٔ بازی ندارد؛ PeriodGuard هر سند را رد می‌کند.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                RecordCount: 0,
                Sample(openYears.Select(y => $"FiscalYearId={y.Id}, Name={y.Name}")),
                "دوره‌های سال مالی ساخته و دورهٔ Cutover باز شود.",
                FeatureFlag: null));
        }

        var currentYears = await db.FiscalYears.AsNoTracking()
            .CountAsync(y => y.CompanyId == companyId && y.IsCurrent, cancellationToken);
        if (currentYears > 1)
        {
            findings.Add(new AccountingReadinessFinding(
                "MULTIPLE_CURRENT_FISCAL_YEARS",
                "بیش از یک سال مالی جاری",
                "فقط یک سال مالی می‌تواند جاری باشد.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                currentYears,
                Array.Empty<string>(),
                "فقط سال مالی درست جاری بماند.",
                FeatureFlag: null));
        }
    }

    // ۵ — مالکیت شرکتِ حساب‌های نقدی و پرداخت‌ها. رکورد بدون شرکت را دفتر کل جدید نباید لمس کند.
    private async Task AddOwnershipFindingsAsync(
        List<AccountingReadinessFinding> findings,
        int companyId,
        CancellationToken cancellationToken)
    {
        var cashAccountsWithoutCompany = await db.CashAccounts.AsNoTracking()
            .Where(c => c.CompanyId == null)
            .Select(c => new { c.Id, c.Name })
            .Take(AccountingReadinessFinding.MaxSamples)
            .ToListAsync(cancellationToken);
        var cashAccountsWithoutCompanyCount = await db.CashAccounts.AsNoTracking()
            .CountAsync(c => c.CompanyId == null, cancellationToken);

        if (cashAccountsWithoutCompanyCount > 0)
        {
            findings.Add(new AccountingReadinessFinding(
                "CASH_ACCOUNT_WITHOUT_COMPANY",
                "حساب نقدی بدون شرکت",
                "حساب نقدیِ بی‌شرکت را نمی‌توان به دفتر هیچ شرکتی نسبت داد؛ پرداخت‌های روی آن Skip می‌شوند. "
                    + "این یافته سراسری است و برای همهٔ شرکت‌ها یکسان گزارش می‌شود.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                cashAccountsWithoutCompanyCount,
                Sample(cashAccountsWithoutCompany.Select(c => $"CashAccountId={c.Id}, Name={c.Name}")),
                "مالکیت شرکت هر حساب نقدی با شواهد تعیین شود. Backfill حدسی ممنوع.",
                FeatureFlag: null));
        }

        var paymentsWithoutCompanyCount = await db.PaymentTransactions.AsNoTracking()
            .CountAsync(p => p.CompanyId == null, cancellationToken);
        if (paymentsWithoutCompanyCount > 0)
        {
            var samples = await db.PaymentTransactions.AsNoTracking()
                .Where(p => p.CompanyId == null)
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, p.PaymentKind })
                .Take(AccountingReadinessFinding.MaxSamples)
                .ToListAsync(cancellationToken);

            findings.Add(new AccountingReadinessFinding(
                "PAYMENT_WITHOUT_COMPANY",
                "پرداخت بدون شرکت",
                "پرداختِ بی‌شرکت با COMPANY_UNRESOLVED رد می‌شود و در دفتر کل جدید نمی‌آید. "
                    + "این یافته سراسری است و برای همهٔ شرکت‌ها یکسان گزارش می‌شود.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                paymentsWithoutCompanyCount,
                Sample(samples.Select(p => $"PaymentTransactionId={p.Id}, Kind={p.PaymentKind}")),
                "مالکیت با شواهد تعیین شود (قرارداد، حساب نقدی، شیپمنت). Backfill حدسی ممنوع.",
                FeatureFlag: null));
        }
    }

    // ۶ — ExpenseTypeهای بدون PayableAccountKind. تصمیمِ ثبت‌شده: نوعِ بدهی از فیلد صریح خوانده
    // می‌شود، نه از استنتاج روی متن آزاد. پس نبودِ فیلد یعنی Skip.
    private async Task AddExpenseTypeFindingsAsync(
        List<AccountingReadinessFinding> findings,
        int companyId,
        CancellationToken cancellationToken)
    {
        var count = await db.ExpenseTypes.AsNoTracking()
            .CountAsync(e => e.PayableAccountKind == null, cancellationToken);
        if (count == 0)
            return;

        var samples = await db.ExpenseTypes.AsNoTracking()
            .Where(e => e.PayableAccountKind == null)
            .OrderBy(e => e.Code)
            .Select(e => new { e.Id, e.Code, e.Name })
            .Take(AccountingReadinessFinding.MaxSamples)
            .ToListAsync(cancellationToken);

        findings.Add(new AccountingReadinessFinding(
            "EXPENSE_TYPE_PAYABLE_KIND_MISSING",
            "ExpenseType بدون PayableAccountKind",
            "بدون این فیلد معلوم نیست بدهی روی کدام حساب بنشیند و هزینه با PAYABLE_KIND_UNKNOWN رد می‌شود. "
                + "این یافته سراسری است (ExpenseType مالکیت شرکت ندارد).",
            AccountingReadinessSeverity.Blocker,
            companyId,
            count,
            Sample(samples.Select(e => $"ExpenseTypeId={e.Id}, Code={e.Code}, Name={e.Name}")),
            "نوع بدهی هر ExpenseType با تصمیم صریح کاربر تعیین شود.",
            "Accounting:Pilots:Expense"));
    }

    // ۷ — markerهای نامشخصِ پیش‌پرداخت مشتری. تفاوت «پیش‌پرداخت» و «دریافت بابت فروش» از روی
    // مبلغ قابل حدس نیست؛ فیلد صریح لازم است.
    private async Task AddCustomerAdvanceFindingsAsync(
        List<AccountingReadinessFinding> findings,
        int companyId,
        CancellationToken cancellationToken)
    {
        // همان شرطی که PaymentAccountingAdapter روی آن تصمیم می‌گیرد: دریافت مشتری با
        // IsCustomerAdvance == true به «پیش‌دریافت» می‌رود و در غیر این صورت به تسویهٔ مطالبات.
        // رکوردهای null هرگز حدس زده نمی‌شوند، پس همان‌ها هستند که باید مشخص شوند.
        var count = await db.PaymentTransactions.AsNoTracking()
            .CountAsync(
                p => p.PaymentKind == PaymentKind.CustomerReceipt && p.IsCustomerAdvance == null,
                cancellationToken);
        if (count == 0)
            return;

        var samples = await db.PaymentTransactions.AsNoTracking()
            .Where(p => p.PaymentKind == PaymentKind.CustomerReceipt && p.IsCustomerAdvance == null)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.CustomerId, p.AmountUsd })
            .Take(AccountingReadinessFinding.MaxSamples)
            .ToListAsync(cancellationToken);

        findings.Add(new AccountingReadinessFinding(
            "CUSTOMER_ADVANCE_MARKER_UNKNOWN",
            "دریافت از مشتری بدون تعیین پیش‌پرداخت‌بودن",
            "IsCustomerAdvance مشخص نیست، پس معلوم نیست دریافت به «پیش‌دریافت مشتری» بنشیند یا طلب را کم کند. "
                + "این یافته سراسری است.",
            AccountingReadinessSeverity.Blocker,
            companyId,
            count,
            Sample(samples.Select(p => $"PaymentTransactionId={p.Id}, CustomerId={p.CustomerId}, AmountUsd={p.AmountUsd}")),
            "برای هر دریافت، پیش‌پرداخت‌بودن با تصمیم کاربر مشخص شود.",
            "Accounting:Pilots:CustomerReceipt + CustomerAdvance"));
    }

    // ۸ — Poolهای ارزش‌گذاری. Poolِ منفی یا موجودیِ بدون ارزش یعنی COGS نمی‌تواند درست بردارد.
    private async Task AddInventoryValuationFindingsAsync(
        List<AccountingReadinessFinding> findings,
        int companyId,
        CancellationToken cancellationToken)
    {
        var pools = await db.InventoryAverageCosts.AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .Select(p => new { p.Id, p.ProductId, p.TerminalId, p.QuantityMt, p.TotalValueUsd })
            .ToListAsync(cancellationToken);

        if (pools.Count == 0)
        {
            findings.Add(new AccountingReadinessFinding(
                "INVENTORY_POOL_EMPTY",
                "هیچ Pool ارزش‌گذاری برای این شرکت وجود ندارد",
                "InventoryAverageCost خالی است. تا وقتی InventoryReceipt چیزی نریخته، COGS و زیان موجودی "
                    + "با INVENTORY_NOT_VALUED رد می‌شوند. اگر موجودی واقعی دارید، این یعنی موجودی ابتدای "
                    + "دوره هنوز وارد نشده.",
                AccountingReadinessSeverity.OperationalDataValidationRequired,
                companyId,
                RecordCount: 0,
                Array.Empty<string>(),
                "روی Backup عملیاتی بررسی شود که آیا موجودی واقعی وجود دارد یا نه. "
                    + "ورودِ موجودی ابتدای دوره تصمیم صریح جداگانه می‌خواهد.",
                "Accounting:Pilots:InventoryReceipt"));
            return;
        }

        var unvalued = pools.Where(p => p.QuantityMt > 0m && p.TotalValueUsd <= 0m).ToList();
        if (unvalued.Count > 0)
        {
            findings.Add(new AccountingReadinessFinding(
                "INVENTORY_QUANTITY_WITHOUT_VALUE",
                "موجودیِ ارزش‌گذاری‌نشده",
                "Pool مقدار دارد ولی ارزشش صفر یا منفی است؛ میانگین متحرک بی‌معنی می‌شود و فروش روی آن "
                    + "بهای تمام‌شدهٔ اشتباه می‌گیرد.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                unvalued.Count,
                Sample(unvalued.Select(p =>
                    $"PoolId={p.Id}, ProductId={p.ProductId}, TerminalId={p.TerminalId}, Qty={p.QuantityMt}, Value={p.TotalValueUsd}")),
                "منشأ این Poolها روی داده‌ی عملیاتی بررسی شود. Backfill حدسی ممنوع.",
                "Accounting:Pilots:Cogs"));
        }

        var negative = pools.Where(p => p.QuantityMt < 0m || p.TotalValueUsd < 0m).ToList();
        if (negative.Count > 0)
        {
            findings.Add(new AccountingReadinessFinding(
                "INVENTORY_POOL_NEGATIVE",
                "Pool ارزش‌گذاری منفی",
                "مقدار یا ارزشِ منفی یعنی برداشت بیش از موجودی ثبت شده.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                negative.Count,
                Sample(negative.Select(p =>
                    $"PoolId={p.Id}, ProductId={p.ProductId}, TerminalId={p.TerminalId}, Qty={p.QuantityMt}, Value={p.TotalValueUsd}")),
                "ترتیب رویدادهای موجودی روی داده‌ی عملیاتی بررسی شود.",
                "Accounting:Pilots:Cogs"));
        }
    }

    // ۹ — فروش‌هایی که بهای تمام‌شده نگرفته‌اند. وقتی Flag روشن باشد ولی سند COGS نباشد،
    // یعنی Skip خورده — که تقریباً همیشه INVENTORY_NOT_VALUED است.
    private async Task AddSalesCostFindingsAsync(
        List<AccountingReadinessFinding> findings,
        int companyId,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !_options.Pilots.Cogs)
        {
            var salesCount = await db.SalesTransactions.AsNoTracking()
                .CountAsync(s => s.CompanyId == companyId, cancellationToken);
            if (salesCount > 0)
            {
                findings.Add(new AccountingReadinessFinding(
                    "SALES_COST_NOT_EVALUATED",
                    "بهای تمام‌شدهٔ فروش هنوز ارزیابی نشده",
                    $"{salesCount} فروش برای این شرکت هست ولی Flag مربوط به Cogs خاموش است، پس هیچ‌کدام "
                        + "سند بهای تمام‌شده نگرفته‌اند و وضعیت واقعی PendingCost/INVENTORY_NOT_VALUED معلوم نیست.",
                    AccountingReadinessSeverity.OperationalDataValidationRequired,
                    companyId,
                    salesCount,
                    Array.Empty<string>(),
                    "روی Backup عملیاتی با Flagهای Sale/Cogs/InventoryReceipt/InventoryTransfer روشن اجرا و "
                        + "SkipOrFailureReason از لاگ شمرده شود.",
                    "Accounting:Pilots:Cogs"));
            }
            return;
        }

        var costed = await db.JournalEntries.AsNoTracking()
            .Where(j => j.CompanyId == companyId
                && j.SourceModule == SalesAccountingAdapter.SourceModule
                && j.SourceEventId != null
                && j.SourceEventId.Contains("Cogs"))
            .Select(j => j.SourceEntityId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var uncosted = await db.SalesTransactions.AsNoTracking()
            .Where(s => s.CompanyId == companyId && !costed.Contains(s.Id))
            .OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.ProductId, s.QuantityMt })
            .ToListAsync(cancellationToken);

        if (uncosted.Count > 0)
        {
            findings.Add(new AccountingReadinessFinding(
                "SALE_WITHOUT_COGS_JOURNAL",
                "فروش بدون سند بهای تمام‌شده",
                "Flag روشن است ولی این فروش‌ها سند COGS نگرفته‌اند — یعنی Skip خورده‌اند "
                    + "(معمولاً INVENTORY_NOT_VALUED: Pool مقصد خالی یا ناکافی بوده).",
                AccountingReadinessSeverity.Blocker,
                companyId,
                uncosted.Count,
                Sample(uncosted.Select(s => $"SalesTransactionId={s.Id}, ProductId={s.ProductId}, Qty={s.QuantityMt}")),
                "لاگِ «Sales accounting pilot comparison» برای دلیل دقیق بررسی شود.",
                "Accounting:Pilots:Cogs"));
        }
    }

    // ۱۰ — انتقال ترمینالی بدون انتقال بها. legی که رسید خورده ولی سند انتقال ندارد یعنی
    // تن‌ها جابه‌جا شده و پول نه — همان چیزی که Cogs مقصد را ناامن می‌کند.
    private async Task AddTransferFindingsAsync(
        List<AccountingReadinessFinding> findings,
        int companyId,
        CancellationToken cancellationToken)
    {
        var transferLegCount = await db.InventoryTransportLegs.AsNoTracking()
            .CountAsync(
                l => l.SourcePurchaseContract!.CompanyId == companyId && l.DestinationTerminalId != null,
                cancellationToken);
        if (transferLegCount == 0)
            return;

        if (!_options.Enabled || !_options.Pilots.InventoryTransfer)
        {
            findings.Add(new AccountingReadinessFinding(
                "TRANSFER_COST_NOT_MOVED",
                "انتقال ترمینالی بدون انتقال بها",
                $"{transferLegCount} حملِ بین‌ترمینالی برای این شرکت هست و Flag مربوط به InventoryTransfer "
                    + "خاموش است، پس بهای هیچ‌کدام بین Poolها جابه‌جا نشده. تا وقتی این Flag خاموش است، "
                    + "روشن‌کردن Cogs ناامن است: فروشِ مقصد روی Poolی ارزش‌گذاری می‌شود که کالا با آن نیامده.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                transferLegCount,
                Array.Empty<string>(),
                "Flagهای InventoryTransfer و Cogs باید با هم روشن شوند، و اول روی Backup اعتبارسنجی شوند.",
                "Accounting:Pilots:InventoryTransfer"));
            return;
        }

        var postedLegIds = await db.JournalEntries.AsNoTracking()
            .Where(j => j.CompanyId == companyId
                && j.SourceModule == InventoryTransferAccountingAdapter.SourceModule
                && j.SourceEntityId != null)
            .Select(j => j.SourceEntityId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var unmoved = await db.InventoryTransportLegs.AsNoTracking()
            .Where(l => l.SourcePurchaseContract!.CompanyId == companyId
                && l.DestinationTerminalId != null
                && !postedLegIds.Contains(l.Id))
            .OrderBy(l => l.Id)
            .Select(l => new { l.Id, l.SourceTerminalId, l.DestinationTerminalId })
            .ToListAsync(cancellationToken);

        if (unmoved.Count > 0)
        {
            findings.Add(new AccountingReadinessFinding(
                "TRANSFER_LEG_WITHOUT_COST_JOURNAL",
                "حمل بین‌ترمینالی بدون سند انتقال بها",
                "Flag روشن است ولی این legها سند انتقال بها ندارند — یعنی Skip خورده‌اند.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                unmoved.Count,
                Sample(unmoved.Select(l =>
                    $"LegId={l.Id}, SourceTerminalId={l.SourceTerminalId}, DestinationTerminalId={l.DestinationTerminalId}")),
                "لاگِ «Inventory transfer accounting pilot comparison» برای دلیل دقیق بررسی شود.",
                "Accounting:Pilots:InventoryTransfer"));
        }
    }

    // ۱۳ — سند نامتوازن یا وضعیتِ ناسازگار. این‌ها نباید اصلاً ممکن باشند؛ اگر هستند، یعنی
    // چیزی از کنارِ AccountingPostingService رد شده.
    private async Task AddJournalIntegrityFindingsAsync(
        List<AccountingReadinessFinding> findings,
        int companyId,
        CancellationToken cancellationToken)
    {
        var unbalanced = await db.JournalEntries.AsNoTracking()
            .Where(j => j.CompanyId == companyId)
            .Select(j => new
            {
                j.Id,
                j.JournalNumber,
                j.Status,
                Debit = j.Lines.Sum(l => (decimal?)l.Debit) ?? 0m,
                Credit = j.Lines.Sum(l => (decimal?)l.Credit) ?? 0m,
                LineCount = j.Lines.Count
            })
            .Where(j => j.Debit != j.Credit)
            .ToListAsync(cancellationToken);

        if (unbalanced.Count > 0)
        {
            findings.Add(new AccountingReadinessFinding(
                "UNBALANCED_JOURNAL",
                "سند نامتوازن",
                "جمع بدهکار و بستانکار برابر نیست. هیچ سندی نباید در این وضعیت باشد.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                unbalanced.Count,
                Sample(unbalanced.Select(j =>
                    $"JournalEntryId={j.Id}, Number={j.JournalNumber}, Status={j.Status}, Debit={j.Debit}, Credit={j.Credit}")),
                "مسیر ساختِ این اسناد بررسی و با Reversal اصلاح شوند.",
                FeatureFlag: null));
        }

        var emptyPosted = await db.JournalEntries.AsNoTracking()
            .Where(j => j.CompanyId == companyId
                && j.Status == JournalEntryStatus.Posted
                && j.Lines.Count == 0)
            .Select(j => new { j.Id, j.JournalNumber })
            .ToListAsync(cancellationToken);

        if (emptyPosted.Count > 0)
        {
            findings.Add(new AccountingReadinessFinding(
                "POSTED_JOURNAL_WITHOUT_LINES",
                "سند Posted بدون سطر",
                "سند Posted بدون سطر یعنی ثبت ناقص مانده.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                emptyPosted.Count,
                Sample(emptyPosted.Select(j => $"JournalEntryId={j.Id}, Number={j.JournalNumber}")),
                "این اسناد بررسی شوند.",
                FeatureFlag: null));
        }

        var postedWithoutStamp = await db.JournalEntries.AsNoTracking()
            .Where(j => j.CompanyId == companyId
                && j.Status == JournalEntryStatus.Posted
                && j.PostedAt == null)
            .Select(j => new { j.Id, j.JournalNumber })
            .ToListAsync(cancellationToken);

        if (postedWithoutStamp.Count > 0)
        {
            findings.Add(new AccountingReadinessFinding(
                "POSTED_JOURNAL_WITHOUT_POSTED_AT",
                "سند Posted بدون زمان ثبت",
                "وضعیت Posted است ولی PostedAt خالی — وضعیت و مهرِ زمانی ناسازگارند.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                postedWithoutStamp.Count,
                Sample(postedWithoutStamp.Select(j => $"JournalEntryId={j.Id}, Number={j.JournalNumber}")),
                "مسیر ثبت بررسی شود.",
                FeatureFlag: null));
        }

        var draftWithStamp = await db.JournalEntries.AsNoTracking()
            .Where(j => j.CompanyId == companyId
                && j.Status == JournalEntryStatus.Draft
                && j.PostedAt != null)
            .Select(j => new { j.Id, j.JournalNumber })
            .ToListAsync(cancellationToken);

        if (draftWithStamp.Count > 0)
        {
            findings.Add(new AccountingReadinessFinding(
                "DRAFT_JOURNAL_WITH_POSTED_AT",
                "سند Draft با زمان ثبت",
                "وضعیت Draft است ولی PostedAt پر شده — یعنی سند پس از ثبت به Draft برگردانده شده.",
                AccountingReadinessSeverity.Blocker,
                companyId,
                draftWithStamp.Count,
                Sample(draftWithStamp.Select(j => $"JournalEntryId={j.Id}, Number={j.JournalNumber}")),
                "این اسناد بررسی شوند؛ سند Posted تغییرناپذیر است.",
                FeatureFlag: null));
        }
    }

    // ۱۱ — اختلاف Purchase جدید با legacy. سطر legacyِ کهنه اصلاح شد، ولی برابری واقعی فقط روی
    // داده‌ی عملیاتی و با Flag روشن اثبات می‌شود.
    private static void AddPurchaseComparisonFinding(List<AccountingReadinessFinding> findings, int companyId)
        => findings.Add(new AccountingReadinessFinding(
            "PURCHASE_LEGACY_COMPARISON_REQUIRED",
            "مقایسهٔ خرید جدید با legacy روی داده‌ی واقعی لازم است",
            "علتِ شناخته‌شدهٔ اختلاف (کهنه‌ماندن سطر legacy پس از بازقیمت‌گذاری، و ناهم‌گونیِ گردکردن) "
                + "برطرف شده و هر دو مسیر حالا از یک قانون گردکردن عبور می‌کنند. اما برابریِ واقعی روی "
                + "داده‌ی عملیاتی هنوز اثبات نشده.",
            AccountingReadinessSeverity.OperationalDataValidationRequired,
            companyId,
            RecordCount: 0,
            Array.Empty<string>(),
            "روی Backup با Flagهای Purchase و InventoryReceipt روشن اجرا و لاگِ "
                + "«Purchase accounting pilot comparison» با سطرهای legacy مقایسه شود.",
            "Accounting:Pilots:Purchase"));

    // ۱۲ — صراف. Mapping تغییر نمی‌کند: بدهی واقعی صراف SarrafChargedAmountUsd است، و
    // اختلافِ سند (SarrafCharged − SupplierLedger) با LegacyDifference (Requested − SupplierAccepted)
    // دو مفهوم متفاوت‌اند و نباید به‌زور برابر شوند.
    private async Task AddSarrafComparisonFindingAsync(
        List<AccountingReadinessFinding> findings,
        int companyId,
        CancellationToken cancellationToken)
    {
        var settlementCount = await db.SarrafSettlements.AsNoTracking().CountAsync(cancellationToken);
        if (settlementCount == 0)
            return;

        findings.Add(new AccountingReadinessFinding(
            "SARRAF_OPERATIONAL_VALIDATION_REQUIRED",
            "اعتبارسنجی صراف روی داده‌ی عملیاتی لازم است",
            "Mapping صراف دست‌نخورده می‌ماند و عمداً با legacy برابر نیست: JournalGapUsd یعنی "
                + "SarrafChargedAmountUsd − SupplierLedgerAmountUsd، ولی LegacyDifferenceUsd یعنی "
                + "RequestedAmountUsd − SupplierAcceptedAmountUsd. این دو گاف‌های متفاوتی را می‌سنجند. "
                + "لاگِ مقایسه هر سه را کنار هم چاپ می‌کند (JournalGapUsd، JournalGapAccountKind، "
                + "LegacyDifferenceUsd) تا واگرایی هر تسویه دیده شود. "
                + $"{settlementCount} تسویهٔ صراف در دیتابیس هست. این یافته سراسری است.",
            AccountingReadinessSeverity.OperationalDataValidationRequired,
            companyId,
            settlementCount,
            Array.Empty<string>(),
            "Flag خاموش بماند. روی Backup دیتابیس عملیاتی اجرا و سه عدد لاگ بررسی شوند. "
                + "بدون داده‌ی واقعی Mapping تغییر نکند.",
            "Accounting:Pilots:SarrafSettlement"));
    }

    private static IReadOnlyList<string> Sample(IEnumerable<string> values)
        => values.Take(AccountingReadinessFinding.MaxSamples).ToList();
}
