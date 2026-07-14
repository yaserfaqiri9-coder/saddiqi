# ARCHITECTURE — PTG Oil System

> مرجع معماری فعلی. هر عددی که اینجا آمده از خود کد استخراج شده است.

## پشتهٔ فنی

| لایه | فناوری |
|---|---|
| Backend | ASP.NET Core MVC — .NET 8 |
| زبان | C# |
| ORM | Entity Framework Core 8 + Npgsql |
| Database | PostgreSQL |
| Frontend | Razor Views + Bootstrap 5 RTL + Vazirmatn |
| سبک معماری | Modular Monolith |
| زبان UI | فارسی / دری، RTL کامل |

## ساختار Solution

```text
ptg-oil-system/
├─ src/PTGOilSystem.Web/     پروژهٔ اصلی ASP.NET Core MVC
│  ├─ Controllers/           ۶۸ فایل کنترلر (بعضی به partial تقسیم شده‌اند)
│  ├─ Models/
│  │  ├─ Entities/           ۱۳ فایل entity، ۶۹ DbSet
│  │  └─ <Feature>/          ViewModelها به تفکیک دامنه
│  ├─ Services/              ۴۷ فایل سرویس/interface
│  ├─ Data/                  ApplicationDbContext
│  ├─ Migrations/            ۷۴ migration
│  ├─ Security/              AuthBootstrapper، AuthPolicies
│  ├─ TagHelpers/            AkEntityComboboxTagHelper
│  ├─ Views/                 Razor Views + Shared/Components/Ak
│  └─ wwwroot/               CSS (۲۹ فایل)، JS (۲۹ فایل)، تصاویر، قالب فاکتور
├─ tests/PTGOilSystem.Web.Tests/   ۶۸ فایل تست
├─ scripts/                  اسکریپت اجرا/نگهداری محلی
├─ tools/db-cleaner/         ابزار پاک‌سازی دیتابیس
├─ db/                       یادداشت‌های schema و migrationهای دستی SQL
└─ docs/                     مستندات مرجع (همین پوشه)
```

## الگوی لایه‌ها

جریان عادی یک درخواست:

```text
Controller → Service (اگر منطق تجاری دارد) → ApplicationDbContext → PostgreSQL
                                           ↘ AuditService (لاگ تغییرات حساس)
```

- **Controller** مسئول binding، permission، ساخت ViewModel و redirect است.
- **Service** منطق تجاری چندمرحله‌ای، محاسبات مالی و نوشتن Ledger را نگه می‌دارد.
- محاسبات ساده و queryهای خواندنی مستقیم داخل Controller انجام می‌شوند — کنترلرهای بزرگ (Loading، ContractJourney) بدهی شناخته‌شده‌اند و در [CURRENT-SYSTEM-STATE.md](CURRENT-SYSTEM-STATE.md) ثبت شده‌اند.

## سرویس‌های کلیدی

| سرویس | نقش |
|---|---|
| `PricingService` | قیمت‌گذاری بر اساس تاریخ تراکنش با fallback به آخرین نرخ معتبر |
| `StockService` | تنها منبع حقیقت موجودی؛ stock را از روی `InventoryMovement` محاسبه می‌کند |
| `UnitConversionService` | تبدیل واحد (MT / لیتر / بشکه) |
| `CurrencyConversionService` | تبدیل ارز و FX |
| `ExpenseRuleEngine` | اعمال قواعد مصرف روی عملیات |
| `SaleLedgerFactory` | ساخت `LedgerEntry` فروش |
| `InventoryLineageWriter` / `InventoryLineagePnlService` | زنجیرهٔ lot و P&L مبتنی بر lineage |
| `InventoryTransportBatchService` / `…LegLoadService` / `…ReceiptService` / `…PnlService` | چرخهٔ حمل از موجودی |
| `SarrafSettlementService` | تسویهٔ صراف |
| `SupplierPaymentAllocationService` | تخصیص پرداخت تأمین‌کننده |
| `ContractBalanceTransferService` | انتقال مانده بین قراردادها |
| `ContractAmendmentService` | متمم قرارداد به‌صورت immutable |
| `DispatchFreightExpenseSync` | همگام‌سازی کرایه دیسپچ با Expense/Ledger |
| `EmployeeSalaryService` | معاش کارمند |
| `AutoCodeService` | تولید کد خودکار موجودیت‌ها |
| `MasterDataDeleteSafetyService` | جلوگیری از حذف master data دارای وابستگی |
| `AuditService` + `AuditDiffFormatter` | ثبت diff تغییرات حساس |
| `FormTokenGuard` + `ProcessedFormToken` | جلوگیری از double-submit |
| `DashboardService` / `FinanceMetricCardsQuery` | read-model داشبورد و متریک‌های مالی |

## احراز هویت و دسترسی

- Cookie Authentication داخلی. صفحهٔ ورود: `/Auth/Login`. خروج فقط با `POST`.
- کاربران در جدول `Users`، نقش‌ها در `Roles`. نقش‌ها: `Admin`, `Manager`, `Operator`, `Viewer`.
- سیاست‌های دسترسی در `Security/AuthPolicies` (مثل `ManageData`) و روی اکشن‌ها با `[Authorize(Policy = …)]` اعمال می‌شوند.
- `AuthBootstrapper` در startup نقش‌ها را idempotent seed می‌کند و فقط اگر هیچ کاربری نبود یک admin اولیه می‌سازد.
- محافظت آخرین admin: حذف/غیرفعال‌سازی آخرین حساب admin مسدود است.

## لایهٔ Frontend

- Razor + Bootstrap 5.3 RTL؛ بدون SPA framework.
- ناوبری شبه-SPA با `spa-nav.js`: لینک‌ها با `fetch` + هدر `X-PTG-SPA` گرفته می‌شوند و `_Layout` در آن حالت chrome (سایدبار/تاپ‌بار/CSS پایه) را دوباره نمی‌فرستد. prefetch روی hover/mousedown.
- Design System فعلی `ak-*` است — به [UI-DESIGN-SYSTEM.md](UI-DESIGN-SYSTEM.md) مراجعه کنید.

## قواعد ثابت

- بدون InMemory fallback؛ نبودِ connection string معتبر = خطای startup.
- همهٔ تاریخ‌ها UTC (`timestamp with time zone`).
- مبالغ `numeric(18,4)`، نرخ ارز `numeric(18,6)`.
- هر فرم POST باید `@Html.AntiForgeryToken()` صریح داشته باشد (auto-inject خاموش است).
