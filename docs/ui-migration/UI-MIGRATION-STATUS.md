# UI Migration Status — بازطراحی Akaunting

> **این سند تاریخچهٔ مهاجرت است، نه مرجع طراحی.** مرجع زندهٔ Design System: [../UI-DESIGN-SYSTEM.md](../UI-DESIGN-SYSTEM.md).
> تصاویر مرجع Akaunting و فایل `Akaunting_Design_Audit_PTG_FA.docx` پس از پایان مهاجرت حذف شدند؛ ارجاع‌های داخل این سند به آن‌ها تاریخی‌اند و در Git history باقی‌اند.
> مهاجرت کامل شد. آخرین به‌روزرسانی: ۲۰۲۶-۰۷-۱۴.
>
> ⚠️ **ارجاعات Search/Filter در بخش‌های ۱ تا ۲۶ این سند تاریخی‌اند و دیگر معتبر نیستند.**
> هر جا `_AkSearchBar`، `_ManagementFilterBar`، `ak-filter.js`، `.ak-fbar`/`.ak-fpop`/`.ak-token`، `.ak-filterbar` به‌عنوان Search/Filter، یا dropdown فیلتر Bootstrap قدیمی دیده می‌شود = **حذف‌شده**. وضعیت قطعی فعلی فقط در **بخش ۲۷** و در [../UI-DESIGN-SYSTEM.md](../UI-DESIGN-SYSTEM.md) است.

---

## ۰) خلاصه وضعیت

- **فاز ۱ (Skin / Token) — انجام‌شده ولی ناکافی.** کاربر رد کرد: «بیشتر یک Skin و CSS Override روی ساختار قدیمی است.»
- **فاز ۲ (بازسازی واقعی markup) — شروع‌نشده، منتظر تأیید کاربر.**
- **قانون فعال:** حق افزودن CSS سراسری بیشتر نیست. باید Razor View/Partial/Component واقعی بازسازی شود.
- **بدون Commit** تا اطلاع بعدی.
- «انجام‌شد» فقط وقتی برای ۵ صفحه نمونه: screenshot قبل/بعد + گزارش اختلاف دقیق موجود باشد.

---

## ۱) تصمیم‌های قطعی (Design Tokens)

پالت Akaunting (منبع واحد: `wwwroot/css/ptg/01-tokens.css`):

| نقش | مقدار |
|---|---|
| پس‌زمینه بدنه | `#FCFCFC` (flat، near-white) |
| متن | `#424242` |
| بنفش ناوبری/تعاملی | `#55588B` / هاور `#404268` |
| سبز اکشن | `#6EA152` / هاور `#53793E` |
| ریل سایدبار | `#DCE2F9` (۵۶px) |
| پنل سایدبار | `#F2F4FC` (۲۲۴px) |
| موفق | `#F1F6EE` / `#63914A` |
| خطا | `#FAE6E6` / `#B80000` |
| هشدار | `#FEF5E7` / `#B87708` |
| اطلاع | `#E6F1F6` / `#006395` |
| چارت | ورودی `#8BB475` / خروجی `#FB7185` / سود `#7779A2` |

تایپوگرافی: Vazirmatn، RTL کامل، اعداد مالی LTR + tabular.
عنوان صفحه ۳۶px/۳۰۰، عنوان ویجت ۱۶px/۵۰۰، هدر جدول ۱۲px، سلول ۱۴px.
کانتینر lg ۱۰۰۰ / 2xl ۱۱۴۵. شعاع ۶/۸/۱۲. موشن ۱۴۰–۱۹۰ms. Shadow فقط Dropdown/Tooltip/Modal.
Flat، بدون Card/Shadow تزئینی.

**دست‌نزدنی:** منطق تجاری، Controller، Service، ViewModel، Validation، Permission، Route، DB، API، محاسبات مالی. لوگو/برند Akaunting کپی نشود.

---

## ۲) فاز ۱ — کارهای انجام‌شده (Skin)

- `01-tokens.css` بازنویسی کامل؛ همه alias های legacy (`app-*/ptg-*/boltz-*/ds-*/--ul-*`) به پالت Akaunting repoint شد.
- `45-akaunting.css` (جدید، آخرین لود در `_Layout` و `_ModalLayout` بعد از `41-compact`): سایدبار دولایه با `linear-gradient`، flat cards/buttons/forms/tables، تب‌ها (override `--ptg-tabs-*`)، badge/dropdown/modal، کانتینر ۱۰۰۰/۱۱۴۵.
- ۳ دور swap سراسری هگزهای hardcoded در `wwwroot/css` (~۱۲۵۰ جایگزینی، ۳۶+ فایل). مستثنی: `_archive`، `*invoice*`، `01-tokens`، `45-akaunting`.
- سایدبار پیش‌فرض expanded (حذف `is-sidebar-collapsed` از body + منطق localStorage معکوس در `navigation.js`).
- چارت داشبورد: `dashboard.js` + `Views/Home/Index.cshtml` رنگ‌ها → پالت جدید.
- لودر `_Layout` بنفش؛ theme-color `#FCFCFC`.
- تست: ۴۵ fail از قبل baseline (HEAD تمیز همان ۴۵) — skin صفر تست جدید نشکست.

### فایل‌های تغییرکرده فاز ۱
- `wwwroot/css/ptg/01-tokens.css` — بازنویسی
- `wwwroot/css/ptg/45-akaunting.css` — جدید (~۱۰۰۰ خط)
- `wwwroot/css/ptg/12-dashboard.css`، `17-system-forms.css` — edit مستقیم
- `Views/Shared/_Layout.cshtml`، `Views/Shared/_ModalLayout.cshtml`
- `wwwroot/js/navigation.js`، `wwwroot/js/dashboard.js`
- `Views/Home/Index.cshtml`
- ~۳۶ فایل CSS دیگر زیر `wwwroot/css/` (swap هگز)

---

## ۳) Audit — `45-akaunting.css` (Patch در برابر قاعده واقعی)

| بخش | وضعیت | حکم |
|---|---|---|
| A تایپوگرافی، D کانتینر، I فیلدها، K تب‌ها، M Dropdown/Modal | قاعده عمومی واقعی | **می‌ماند** |
| B سایدبار (linear-gradient روی markup تک‌لایه) | شبیه‌سازی | Patch → بازسازی ریل+پنل واقعی |
| C Topbar | رنگ‌آمیزی کنترل‌های ۳D قدیمی | Patch |
| E هدر صفحه (`:is()` روی ۶ خانواده هدر) | Patch | → یک `_PageHeader` |
| F کارت‌ها (`!important` روی ۱۲ کلاس wrapper) | Patch خالص | wrapper از DOM حذف |
| G Summary/KPI | رنگ‌آمیزی | Patch |
| H دکمه‌ها (۱۲ خانواده با `!important`) | Patch | یک سیستم دکمه |
| J جدول‌ها + P (`thead transparent !important`، remap `--ul-*`، ۴ variant `reference-metric-card`) | جنگ specificity | Patch خالص |
| L Badge (سه خانواده) | Patch | یک `_StatusBadge` |
| N جستجو/فیلتر | رنگ‌آمیزی | Patch |

**نتیجه:** ~۶۰٪ فایل Patch است؛ با مهاجرت markup حذف می‌شود. هدف نهایی 45 = فقط A/D/I/K/M + توکن‌ها.

---

## ۴) پنج صفحه نمونه + فایل‌ها

| # | الگوی مرجع | صفحه PTG | فایل‌های Razor/Partial |
|---|---|---|---|
| ۱ | Chart of Accounts (لیست) | تأمین‌کنندگان | `Views/Suppliers/Index.cshtml`، `Views/Shared/_PagedListFooter.cshtml`، `Views/Shared/Partials/_ToggleActiveButton.cshtml`، `Views/Shared/_CreateModalShell.cshtml` |
| ۲ | New Account (فورم ساده) | ویرایش تأمین‌کننده | `Views/Suppliers/Edit.cshtml` |
| ۳ | New Income (فورم چندسکشن) | سند روزنامچه | `Views/Payments/Create.cshtml` (۶۲۸ خط) + `finance-forms.js` |
| ۴ | Contact Show / Receipt (جزئیات دوستونه) | جزئیات فروش | `Views/Sales/Details.cshtml` + `31-sales-details.css` |
| ۵ | Edit User | کاربران | `Views/Users/Edit.cshtml`، `Views/Users/Index.cshtml`، `33-user-account-form.css`، `34-users-list.css` |

### تصاویر مرجع
- لیست: `app.akaunting.com_401927_double-entry_chart-of-accounts.png`
- فورم ساده: `..._chart-of-accounts_create.png`
- فورم چندسکشن: `..._banking_transactions_create_type=income.png` + `Screenshot (776).png`
- جزئیات split: `Screenshot (781).png` (Contact Show)
- Edit User: تصویر پیوست کاربر

---

## ۵) مشکلات ساختاری هر صفحه

**۱. Suppliers/Index**
- کل صفحه داخل `.ulist-card` — مرجع بدون Card، روی Canvas.
- هدر صفحه (عنوان ۳۶px + اکشن سبز کنارش) اصلاً نیست؛ دکمه «جدید» داخل toolbar دفن.
- جستجو = pill + دکمه قیف — مرجع: input تمام‌عرض با border پایین.
- اکشن ردیف = ۳ آیکون همیشه‌نمایان — مرجع: checkbox + داده + مبلغ + منوی ⋯.
- ستون checkbox ندارد؛ pager داخل کارت.

**۲. Suppliers/Edit**
- سه wrapper `.card.app-form-card.ds-form-shell.ptg-card` — مرجع روی Canvas.
- Breadcrumb دائمی — مرجع ندارد؛ عنوان + ستاره + Save مقابل.
- سکشن فقط `h2` — بدون توضیح ۱۴px muted و بدون Divider.
- گرید Bootstrap نامنظم (col-md-3/5/4/2) — مرجع دو ستونه، gap افقی ۳۲ / عمودی ۲۴.
- IsActive checkbox داخل گرید — مرجع Toggle در هدر.

**۳. Payments/Create**
- `.card.journal-form-card.ds-form-shell` — باید Canvas.
- سکشن فقط `ds-form-group-title` — الگوی عنوان+توضیح+Divider نیست.
- ریتم عمودی New Income نیست؛ `journal-flat-grid` فشرده.
- Footer عملیات به سبک مرجع نیست.
- ⚠️ تمام `id`/`name`/`data-*` (direction switch، counterparty، sarraf) به `finance-forms.js` وصل — فقط wrapper/کلاس ظاهری عوض شود، نه منطق.

**۴. Sales/Details**
- دیزاین‌سیستم موازی کامل `sd-*` (دکمه/منو/کارت/badge) — ناقض قاعده کامپوننت مشترک.
- اکشن‌ها نوار جدا زیر breadcrumb — مرجع کنار عنوان.
- `sd-summary` کارت ۴بلوکه — مرجع هویت چپ + ۳ عدد بزرگ ۴۰px راست، بدون کارت.
- بدنه پشته sd-card — مرجع split ۴/۱۲ اطلاعات + Divider عمودی + ۸/۱۲ تب‌ها + جدول تخت.

**۵. Users/Edit + Index**
- یک کارت، سکشن فشرده تک‌ردیفه (`col-md-4`×۴) — مرجع Edit User: Personal Information (نام/ایمیل + Upload خط‌چین) + Change Password + سکشن Assign (Company/Role/Landing/Language ردیف جدولی) + Footer.
- Toggle وضعیت در هدر ندارد؛ توضیح سکشن و Divider ندارد.
- Index: همان مشکلات Suppliers/Index.

**قید تست:** `ShellViewStructureTests` وجود marker (`ulist-card`، `ds-form-shell`) و selectorهای CSS را assert می‌کند. یا marker به‌عنوان کلاس compat روی root جدید بماند، یا تست هم‌زمان با View به‌روز شود (تغییر ساختار مجاز؛ منطق تست نشکند).

---

## ۶) برنامه مرحله بعد (فاز ۲)

### گام ۱ — کامپوننت‌های مشترک
مسیر `Views/Shared/Components/Ak/` + یک `50-ak-components.css` کوچک ساختاری (جایگزین تدریجی Patchها):

- `_AkPageHeader` — عنوان ۳۶/۳۰۰ + PrimaryAction سبز + More
- `_AkSection` — عنوان ۱۶/۵۰۰ + توضیح ۱۲ + Divider
- `ak-grid` — دو ستونه، gap ۳۲/۲۴
- `_AkTable` — flat: checkbox / هدر ۱۲ / سلول ۱۴ / ستون مبلغ LTR / منوی ⋯
- `_AkSearchBar` — border-bottom-only
- `_AkFooterActions` — Cancel خنثی + Save سبز
- `_AkSplit` — ۴/۱۲ + ۸/۱۲ با divider عمودی
- `_AkAssignRow`
- Badge / Tabs / Dropdown موجود بازاستفاده

### گام ۲ — مهاجرت ۵ صفحه (هر کدام: shot قبل → بازسازی markup → shot بعد → لیست اختلاف)
1. Suppliers/Index — ✅ **انجام‌شد** (بازسازی markup واقعی؛ بدون Patch جدید). جزئیات پایین.
2. Suppliers/Edit
3. Users/Edit
4. Payments/Create (پرریسک‌ترین — JS hooks دست‌نخورده)
5. Sales/Details (حذف skin موازی sd-*)

### گام ۳ — بعد از تأیید ۵ نمونه
- تبدیل الگوها به Shared Partial/Component
- مهاجرت همه صفحات هم‌خانواده
- حذف بخش‌های Patch از 45 (E/F/G/H-اضافی/J/L/N/P) + CSSهای صفحه‌ای legacy
- 45-akaunting.css کوچک و فقط قواعد عمومی بماند

### قیود همیشگی
- بدون Commit
- بدون تغییر منطق/محاسبات/DB
- گزارش قبل/بعد هر صفحه اجباری
- CSS صفحه‌ای فقط اگر ضروری و بسیار محدود

---

## ۷) ابزار / محیط

- Dev run: DPAPI-decrypt `DbPassword` از `%LOCALAPPDATA%\PTGOilSystem\local-run-secrets.json`؛ `ASPNETCORE_ENVIRONMENT=Development` (auto sign-in فقط GET text/html)؛ exe detached با `Start-Process`؛ قبل restart process روی port kill.
- Screenshot QA: Playwright skill (`node run.js <script>`)، desktop ۱۴۴۰×۹۰۰ + mobile ۳۹۰×۸۴۴.
- بعد از `dotnet test` بدون `--no-build` حتماً `dotnet build` پروژه Web (وگرنه exe بعدی Layout قدیمی سرو می‌کند).
- Memory مرتبط: `[[akaunting-skin-migration]]`.

---

## ۸) گزارش صفحه ۱ — Suppliers/Index (بازسازی markup)

**مرجع بصری:** `app.akaunting.com_401927_double-entry_chart-of-accounts.png`

### فایل‌های جدید (کامپوننت مشترک فاز ۲)
- `Views/Shared/Components/Ak/_AkPageHeader.cshtml` — عنوان ۳۶/۳۰۰ روی Canvas + ستاره + PrimaryAction سبز (لینک یا باز‌کننده مودال). مدل tuple.
- `Views/Shared/Components/Ak/_AkSearchBar.cshtml` — سرچ تمام‌عرض، فقط border-bottom، آیکون پیشرو، GET.
- `wwwroot/css/ptg/50-ak-components.css` — CSS ساختاری تمیز (کلاس‌های `ak-*` روی markup تازه؛ **بدون `!important`**). لود در `_Layout` و `_ModalLayout` بعد از 45.

### فایل‌های تغییرکرده
- `Views/Suppliers/Index.cshtml` — بازنویسی کامل: حذف `ulist-card`/`ulist-toolbar`؛ header+search+جدول تخت روی Canvas؛ ستون checkbox؛ اکشن ردیف = منوی ⋯ (پروفایل/ویرایش/فعال‌سازی) به‌جای ۳ آیکون همیشه‌نمایان؛ pager روی Canvas.
- `Views/Shared/Partials/_ToggleActiveButton.cshtml` — **بدون تغییر**، به‌عنوان dropdown-item بازاستفاده شد.
- `Views/Shared/_Layout.cshtml` + `_ModalLayout.cshtml` — افزودن لینک `50-ak-components.css`.
- `tests/ShellViewStructureTests.cs` — marker صفحه از `ulist-card` → `ak-list-page` (مجاز طبق §۵).

### اختلاف قبل/بعد
| مشکل §۵.۱ (قبل) | بعد |
|---|---|
| کل صفحه داخل `.ulist-card` | تخت روی Canvas، بدون کارت |
| هدر صفحه نبود؛ «جدید» در toolbar دفن | `_AkPageHeader`: عنوان ۳۶px + ستاره + دکمه سبز مقابل |
| جستجو = pill + دکمه قیف | `_AkSearchBar` تمام‌عرض با border پایین |
| اکشن ردیف = ۳ آیکون همیشه‌نمایان | منوی ⋯ تک‌دکمه |
| ستون checkbox نبود | ستون checkbox اضافه شد |
| pager داخل کارت | pager روی Canvas |

### اعتبارسنجی
- Build: موفق (خطای exe-lock فقط چون سرور در حال اجرا بود؛ کد Razor/C# پاک کامپایل شد).
- تست: `dotnet test` → 851 pass / **45 fail = همان baseline** (بدون شکست جدید). `ShellViewStructureTests` سبز برای Suppliers.
- Screenshot: قبل/بعد desktop ۱۴۴۰×۹۰۰ + mobile ۳۹۰×۸۴۴ گرفته شد.
- بدون Commit. توقف پس از همین صفحه.

### راند ۲ — تطبیق دقیق‌تر با مرجع (تأیید کاربر: family-tabs بماند، Topbar مینیمال شود)
- **Topbar مینیمال** (سراسری، ویرایش کلاس مشترک موجود — بدون فایل/CSS جدید، بدون !important):
  - `_Layout.cshtml`: حذف چیپ میان‌بر `ptg-hsearch-kbd` (Ctrl K) → تریگر سرچ فقط آیکون. JS hook `data-search-open` دست‌نخورده.
  - `ptg/03-layout.css`: `.ptg-hsearch-trigger` از pill خاکستری → آیکون گرد ghost تخت (مثل بقیهٔ `ptg-h3d`). زنگوله/چرخ‌دنده/زبان/آواتار از قبل flat بودند.
- **جدول** (فقط ak-*): ستون spacer نامرئی (`ak-col-spacer` width:100%) → هویت/متا از start فشرده، مبلغ+وضعیت+⋯ در لبهٔ end (ریتم مرجع «packed start, value at end»). `ak-col-num` اعداد راست‌چین. checkbox select-all در هدر. `ak-name` وزن ۵۰۰ (رنگ لینک برند). سرچ بدون آیکون + padding بیشتر.
- **family-tabs**: طبق تأیید کاربر نگه داشته شد (`_SectionTabs` = کامپوننت مشترک `ptg-tabs-rail`؛ ناوبری واقعی بین صفحات هم‌خانواده). Legacy عمدی و توجیه‌شده.

### فایل‌های تغییرکردهٔ راند ۲
- `Views/Shared/_Layout.cshtml` (topbar markup)
- `wwwroot/css/ptg/03-layout.css` (search trigger — کلاس مشترک موجود)
- `wwwroot/css/ptg/50-ak-components.css` (spacer/num/search)
- `Views/Suppliers/Index.cshtml` (spacer col)

### Legacy باقی‌مانده در این صفحه + دلیل
- `_SectionTabs` family-tabs: نگه‌داشته (تأیید کاربر؛ ناوبری مشترک).
- 45-akaunting Patchها (F کارت، N فیلتر، J جدول، …): هنوز لود می‌شوند چون صفحات دیگر مصرف می‌کنند؛ حذف بعد از مهاجرت کامل خانواده (گام D).
- 45-O لینک سراسری بنفش بر `ak-name` غالب است (بدون !important قابل‌غلبه نیست بدون selector طولانی)؛ رنگ لینک برند پذیرفته شد.

**وضعیت:** Stage A تأیید شد.

---

## ۹) مرحله B — مهاجرت Indexهای هم‌خانواده (خانوادهٔ لیست تخت)

### Batch B1 — خانوادهٔ طرف‌حساب (فهرست‌های تخت) ✅
مهاجرت‌شده به الگوی تأییدشدهٔ ak (همان markup مشترک، بدون Variant صفحه‌ای):
- `Views/Customers/Index.cshtml`
- `Views/Partners/Index.cshtml`
- `Views/Companies/Index.cshtml`
- `Views/ServiceProviders/Index.cshtml`
- `Views/Sarrafs/Index.cshtml`
(+ `Suppliers/Index` از قبل)

هر کدام: `ak-list-page` + `_AkPageHeader` + `_AkSearchBar` + `.ak-table` (checkbox/select-all، `ak-col-grow`، `ak-col-spacer`، `ak-num/ak-col-num`، `ak-status`، منوی ⋯ با `_ToggleActiveButton` بازاستفاده‌شده). حذف `ulist-card/ulist-toolbar/oa-*`.

**Dedup JS:** اسکریپت inline «row-click» از تمام این صفحات حذف شد و به `wwwroot/js/tables.js` منتقل شد (`initializeAkRowNavigation` روی `.ak-table tr[data-href]` سراسری). ServiceProviders فقط validation + openCreateModal را نگه داشت.

**کامپوننت مشترک:** `_AkPageHeader` گسترش یافت (اکشن ثانویه اختیاری — برای «پرداخت از طریق صراف» در Sarrafs).

**تست:** `Customers/Index` marker در `ShellViewStructureTests` از `ulist-card` → `ak-list-page` (بقیهٔ خانواده در تست نیستند). `Employees` هنوز `ulist-card`.

**اعتبار Batch B1:** Build موفق؛ `dotnet test` → 851 pass / **45 fail = baseline** (صفر شکست جدید). خالص ۶ فایل لیست: 287+ / 356− .

### Batch B2 — خانوادهٔ resource-card (Trucks, Wagons, Drivers, Vessels) ✅
مهاجرت‌شده به همان الگوی مشترک ak (جدول تخت، مطابق مرجع؛ بدون Variant/Skin جدید):
- `Views/Trucks/Index.cshtml`
- `Views/Wagons/Index.cshtml`
- `Views/Drivers/Index.cshtml`
- `Views/Vessels/Index.cshtml`
(+ `Products/Index` از قبل، همین خانواده)

هر کدام: `ak-list-page` + `_AkPageHeader` (دکمهٔ سبز → همان مودال Create موجود) + `ak-filterbar` (سرچ `q` + dropdown فیلتر `ak-filter` مطابق Products). جدول `ak-table` تخت: checkbox/select-all، `ak-col-grow` (نام→Details)، `ak-num` برای ستون‌های عددی، `ak-status`، `ak-col-spacer`، و منوی ⋯ (جزئیات/ویرایش + `_ToggleActiveButton` بازاستفاده‌شده به‌صورت dropdown-item). ناوبری ردیف با `data-href` روی `tr` → `initializeAkRowNavigation` سراسری در `tables.js`.

**حفظ رفتار:** همهٔ URLها، Permissionها، id/name فیلترها (`q`, `isActive`, `wagonType`, `flag`)، مودال‌های Create (`*CreateModal`/`*CreateForm`)، returnUrl برای ToggleActive، و pager (`_PagedListFooter`) دست‌نخورده. کارت‌های قبلی فقط View/Edit/Toggle داشتند (بدون Delete) → همان سه اکشن حفظ شد، Delete اضافه نشد.

**حذف Legacy:** حذف نشد. CSS خانوادهٔ `resource-card` (در `36-masterdata-family.css` و…) هنوز توسط `StorageTanks/Index` و `OperationalAssets/Index` مصرف می‌شود؛ طبق قاعده «حذف فقط پس از صفر شدن مصرف کل خانواده» باقی ماند. تصاویر `truck-row-display.png`, `vv.webp`, `dd-display.webp`, `ss-display.webp` اکنون بدون مصرف‌اند ولی چون درخواست فقط CSS/JS بلااستفاده بود، دست‌نخورده ماندند. هیچ JS اختصاصی این خانواده وجود نداشت (ناوبری ردیف از قبل مشترک است).

**اعتبار Batch B2:** Build موفق (۰ خطا)؛ `dotnet test` → **853 pass / 44 fail = همان baseline** (صفر شکست جدید). هیچ تست ساختاری markerِ Trucks/Wagons/Drivers/Vessels را assert نمی‌کند. بدون Commit.

### Batch B3 — اتمام خانوادهٔ resource-card + پاک‌سازی Legacy ✅
دو صفحهٔ باقی‌ماندهٔ خانواده به همان ساختار مشترک ak منتقل شد:
- `Views/StorageTanks/Index.cshtml` — گرید کارت گیج (`storage-tank-card`) → `ak-table` تخت. مارکر `storage-list-page` روی root نگه داشته شد (نیاز `ShellViewStructureTests`) کنار `ak-list-page`. ستون‌ها: مخزن(→Details)/ترمینال/جنس/ظرفیت/موجودی دفتری/پر بودن٪/قراردادها/وضعیت + منوی ⋯ (جزئیات/ویرایش + «تسویه مخزن» وقتی `ContractCount>0`). فیلترها (`q`,`terminalId`,`productId`,`isActive`)، مودال `storageTanksCreateModal`، `SettleFinal`، `_Pagination` حفظ شد.
- `Views/OperationalAssets/Index.cshtml` — `resource-card`/`operational-asset-card` → `ak-table`. اکشن‌ها: primary «دارایی جدید» (لینک `Create`) + secondary «ثبت کرایه» (`CreateRent`) در `_AkPageHeader`؛ دکمهٔ «چاپ» در filterbar. فیلتر `Filter.Query/AssetType/IsActive` با `asp-for` (name/id بدون تغییر). منوی ⋯: پرونده/ویرایش/کرایه/مصرف + `_ToggleActiveButton`. رنگ‌بندی `oa-money*` حفظ شد. اسکریپت inline فیلتر (`oaFilterToggle`) حذف و با dropdown بوت‌استرپ ak جایگزین شد.

**مصرف resource-card در Viewها = صفر.** (grep روی `resource-card*`,`resource-metrics`,`resource-status`,`resource-chip`,`resource-action`,`storage-tank-card`)

**حذف Legacy مرده:**
- فایل `wwwroot/css/ptg/36-masterdata-family.css` (۲۴۹ خط، skin خانوادهٔ masterdata/resource-card) کامل حذف + لینک آن از `_Layout` برداشته شد (در `_ModalLayout` نبود؛ هیچ تست به آن وابسته نیست).
- بلاک‌های مردهٔ resource-card از `13-compat.css` (۳۲۶ خط: bridge کارت + گریدهای `resource-card-grid` + `operational-asset-card` + media) و `17-system-forms.css` (۱۷۷ خط: بلاک `.people-parties-page .resource-*` + media) حذف شد. Brace balance تأیید شد (۱۳: 1325/1325، ۱۷: 251/251).
- ۴ تصویر یتیم حذف شد: `vv.webp`, `dd-display.webp`, `ss-display.webp`, `storage_tank.jpg` (بدون هیچ مرجع در source؛ فقط artifactهای bin/obj کهنه). `truck-row-display.png`,`wagon-row.png` نگه‌داشته شد (هنوز در Details ترانسپورت/Dispatch).
- Hook: اسکریپت inline فیلتر OA حذف شد (فایل JS جدا نداشت).

**Legacy عمداً باقی‌مانده (نه مرده):** توکن `.resource-card-actions`/`.storage-tank-card-actions` صرفاً به‌عنوان یک گزینه در `:is()/:where()`های **زنده** (کنار `.oa-icon-btn`, `.ds-master-card`, `.ds-product-card` در `05/13/38`) باقی است؛ حذف کاملشان کلاس‌های زندهٔ دیگر را می‌شکند. `storage-tank-*` برای `StorageTanks/Details` زنده است.

**آمار خطوط B3:**
- ویوها: OA `+117/−163`, StorageTanks `+104/−159` → خالص **−۱۰۱** خط.
- CSS/Asset مرده: `36`=−۲۴۹، `13-compat`=−۳۲۶، `17-system-forms`=−۱۷۷ → **−۷۵۲** خط CSS + ۴ فایل تصویر + ۱ لینک.
- **کاهش خالص B3 ≈ −۸۵۳ خط** (+ ۵ فایل حذف‌شده).

**اعتبار Batch B3:** Build موفق (۰ خطا)؛ `dotnet test` → **853 pass / 44 fail = baseline** (صفر شکست جدید). بدون Commit.

### باقی‌ماندهٔ مرحله B + دلیل (نیاز به Batch بعدی)
- **Employees/Index**: فیلتر پیشرفته دارد → نیازمند کامپوننت `AkFilterBar` (هنوز ساخته نشده). فعلاً `ulist`.
- خانوادهٔ resource-card **کامل شد** (Trucks/Wagons/Drivers/Vessels/Products/StorageTanks/OperationalAssets؛ CSS اختصاصی حذف شد).

---

## ۱۰) مرحله C — معماری مشترک فرم‌های ساده (Batch B4)

### کامپوننت‌های مشترک ساخته‌شده (یک پیاده‌سازی رسمی، بدون Variant صفحه‌ای)
- **قرارداد CSS فرم در `50-ak-components.css`** (همان فایل DS موجود ak — نه Skin/Override جدید؛ بدون `!important`، بدون selector وابسته به Controller، فقط Tokenهای `01-tokens.css`): `.ak-form`, `.ak-form-section`, `.ak-section-head/.ak-section-title/.ak-section-desc`, `.ak-form-grid` (دو ستونه، gap افقی ۳۲ / عمودی ۲۴، `.ak-col-full`), `.ak-field/.ak-label/.ak-req`, `.ak-input` (Input/Select/Textarea با focus ring از `--primary-main`)، `.ak-toggle-field` (سوییچ استاندارد بوت‌استرپ)، `.ak-field-error` + `.input-validation-error`، `.ak-form-alert` (Validation summary که در حالت valid مخفی می‌شود)، `.ak-footer-actions`.
- **`Views/Shared/Components/Ak/_AkSectionHead.cshtml`** — عنوان ۱۶/۵۰۰ + توضیح ۱۲ + Divider. مدل `(Title, Description?)`.
- **`Views/Shared/Components/Ak/_AkFooterActions.cshtml`** — Save (submit، `btn-primary`) + Cancel (لینک `btn-light`). مدل `(SaveLabel, CancelHref, CancelLabel)`.

**تصمیم معماری (AkField/AkSelect/AkTextarea/AkToggle):** به‌جای Partial دور هر فیلد، به‌صورت **قرارداد کلاس مشترک** روی tag-helperهای واقعی `asp-for` پیاده شد. دلیل: Partial با `@model` مقدارِ فیلد، prefix نام/‏id را می‌شکند و Binding/Validation را خراب می‌کند (قید قطعی نشست). با نوشتن مستقیم `asp-for` در بستر مدلِ صفحه، نام/‏id/‏Validation دقیقاً توسط خود فریم‌ورک تولید می‌شود و دست‌نخورده می‌ماند. `AkUpload` ساخته نشد (هیچ فیلد فایل واقعی در فرم‌های طرف‌حساب نبود — طبق «در صورت نیاز واقعی»).

### صفحات مهاجرت‌شده به فرم مشترک (بازسازی markup واقعی روی Canvas)
- `Views/Suppliers/Edit.cshtml` (مرجع New Account)
- `Views/Customers/Edit.cshtml`
- `Views/Partners/Edit.cshtml`
- `Views/Companies/Edit.cshtml`

هر کدام: حذف Breadcrumb دائمی، حذف Wrapperهای تو در تو (`card app-form-card ds-form-shell ptg-card` + `.card-body` + `.row.g-3` + `col-md-*`)؛ فرم مستقیم روی Canvas با `.ak-form`؛ عنوان + بازگشت در `_AkPageHeader`؛ سکشن‌ها با `_AkSectionHead`؛ گرید منظم `.ak-form-grid`؛ `IsActive` → سوییچ استاندارد `.ak-toggle-field`؛ Cancel/Save در `_AkFooterActions`.

**دست‌نخورده (تأییدشده):** تمام `asp-for`/Model Binding، `id`/`name`، `asp-validation-for`، `_ValidationScriptsPartial`، `AntiForgeryToken`، `asp-action="Edit"`/`asp-route-id`، بازگشت/انصراف → `Details`، Controller/Service/منطق. صفر تغییر در فیلدها (Suppliers/Customers: Code/Name/NamePersian/Country/ContactPerson/Phone/Address/Notes/IsActive؛ Partners +Email؛ Companies بدون تماس).

### Legacy حذف‌نشده + دلیل دقیق
- **CSS فرم قدیمی نگه داشته شد** (`app-form-card`, `ds-form-shell`, `ptg-card`, `form-section`, `ptg-master-*`, `ds-form-*`, `.people-parties-page`): مصرف‌کنندهٔ زنده دارند — مودال‌های Create طرف‌حساب (`_CreateForm.cshtml` هنوز `ptg-master-*`)، فرم‌های Edit سایر خانواده‌ها (Sarrafs/ServiceProviders/Employees/…)، و تست `ContractJourneyViewStructureTests` که وجود تعریف `.app-form-card` در CSS را assert می‌کند. حذف فقط پس از صفرشدن کل مصرف.
- **Patchهای `45-akaunting.css`** (H دکمه‌ها، I فیلدها): هنوز فرم‌های Legacy دیگر مصرف می‌کنند → باقی ماند.

### آمار خطوط B4
- ۴ ویو Edit: `+281 / −158` (Partners/Companies قبلاً تک‌خطیِ minified بودند؛ افزایش خطوط عمدتاً un-minify + markup واقعی خواناست، نه بلوت).
- کامپوننت جدید: `_AkSectionHead` (۱۳) + `_AkFooterActions` (۱۱) = ۲۴ خط، **یک‌بار** برای همهٔ فرم‌ها.
- CSS فرم مشترک: **~۱۶۸ خط** به `50-ak-components.css` (فایل DS موجود؛ نه فایل جدید).
- **خالص این نشست ≈ +۳۱۵ خط** — Batch زیرساخت است؛ کاهش خالص در Batchهای بعدی (مهاجرت صفحات بیشتر + حذف Legacy پس از صفرشدن مصرف) محقق می‌شود.

### اعتبار Batch B4
Build موفق (۰ خطا)؛ `dotnet test` → **853 pass / 44 fail = baseline** (صفر شکست جدید). بدون Commit.

### موانع / کار باقی‌مانده (فرم‌ها)
- `ServiceProviders/Edit` و `Sarrafs/Edit`: ViewModel متفاوت و فیلدهای بیشتر → با پارامتر/Slot در Batch بعدی، نه Variant.
- مودال‌های Create طرف‌حساب: بازطراحی جدا (بستر مودال با Canvas فرق دارد).
- پس از مهاجرت کامل فرم‌ها: حذف `ptg-master-*`/`ds-form-*`/`app-form-card` + Patchهای I/H در `45`.
- **خانوادهٔ foundation ساده** (Currencies, Units, ExpenseTypes, ExpenseRules, Locations, Terminals, DailyFxRates): بررسی الگو لازم است (برخی ds-card).
- **خانوادهٔ عملیات/مالی** (Loading, Sales, Payments, Dispatch, Expenses, Ledger…): ساختار bespoke + JS زنده + تست‌های ساختاری اختصاصی → پرریسک، Batch جدا با دقت.

### مراحل C/D (فرم‌ها، جزئیات، پاک‌سازی Legacy، refactor 45) — شروع‌نشده
نیازمند کامپوننت‌های مشترک جدید: `AkFilterBar`, `AkSection`, `AkFormGrid`, `AkField/AkSelect/AkTextarea/AkUpload/AkToggle`, `AkFooterActions`, `AkSplit`. حذف خانواده‌های `ptg-*/ds-*/sd-*/ulist-*/app-*` و Patchهای 45 فقط **بعد از قطع کامل مصرف هر خانواده** انجام می‌شود (وگرنه صدها صفحه و تست می‌شکنند).

**روش ادامه:** Batch‌به‌Batch با Build+تست سبز بعد از هر خانواده؛ حذف Legacy همان خانواده بلافاصله پس از صفر شدن مصرف. مهاجرت کامل کل UI چند Batch/نشست است؛ در این نشست B1 تحویل و تأیید شد.

## ۱۱) Batch B5 — تکمیل Edit خانواده طرف‌حساب (ServiceProviders + Sarrafs)

**مهاجرت‌شده به ak-form مشترک (inline، مرجع New Account):**
- `ServiceProviders/Edit.cshtml` — حذف `ds-page`/`app-form-card`/`ds-form-shell`/`ptg-card` + Breadcrumb؛ `_AkPageHeader` + یک `ak-form-section` (Code, Name*, ProviderType `select.ak-input` با `ViewBag.ProviderTypes`, IsActive toggle) + `_AkFooterActions`. حفظ asp-for/id/name/Validation/antiforgery/Route/returnUrl (Details).
- `Sarrafs/Edit.cshtml` — همان الگو (Name*, PhoneNumber, IsActive)؛ حذف `people-parties-page`/`ds-page`/wrapperها.

**نتیجه:** هر دو Edit از `_Form.cshtml` جدا شدند (دیگر مصرف‌کننده Edit ندارد). `_Form.cshtml` هنوز زنده است چون Create page + Create modal آن را رندر می‌کنند.

**آمار:** ۲ ویو، ≈ +96 / −75 خط؛ بدون CSS/JS جدید.
**Build ✅ · Test 853 pass / 44 fail = baseline (صفر شکست جدید).** بدون Commit.

### نقطه دقیق ادامه (Batch B6 — Create family، بزرگ و متداخل)
Create modalها همگی از shell مشترک `Views/Shared/_CreateModalShell.cshtml` استفاده می‌کنند که به `wwwroot/js/modal-design-system.js` (۶۷۷ خط) به‌شدت گره خورده است:
`.ptg-modal-form-scroll`, `.ptg-modal-workbench`, `[data-modal-preview-card]`, `[data-modal-preview-title]`, `[data-modal-preview-source]`, `[data-ptg-entity-modal-form]`, `[data-entity-modal]/-open/-close`, `.ptg-modal-submit`, `.form-actions`, `.ptg-reference-main-panel`, کلاس‌های `ptg-modal-field-heavy/-dense`.
همچنین `Views/Shared/_CreatePageShell.cshtml` (نسخه full-page) و پارشال‌های `_CreateForm.cshtml`/`_Form.cshtml` هر شش طرف‌حساب همگی هنوز ptg-master-* هستند.
**بنابراین بازطراحی Create = یک Batch واحد و cross-cutting**: بازنویسی `_CreateModalShell` + `_CreatePageShell` به ak-modal/ak-form + مهاجرت همه پارشال‌های فرم + refactor بخش‌های مربوطه `modal-design-system.js` با حفظ کامل همه hookهای JS/Ajax/preview/antiforgery. این کار کل create modalهای برنامه (نه فقط طرف‌حساب) را هم‌زمان تغییر می‌دهد؛ در انتهای این نشست شروع نشد تا Working Tree سالم و Buildable بماند. B6 باید با بازبینی دقیق `modal-design-system.js` آغاز شود.

## ۱۲) Batch B6 — فرم‌های Create خانواده طرف‌حساب → ak-form مشترک

**مهاجرت‌شده (محتوای فرم داخل Modal + صفحه Create، Markup واقعی):**
- `Suppliers/_CreateForm.cshtml`, `Customers/_CreateForm.cshtml`, `Partners/_CreateForm.cshtml`, `Companies/_CreateForm.cshtml`
- `ServiceProviders/_Form.cshtml`, `Sarrafs/_Form.cshtml` (مشترک Create page + Index modal)
- همه از `ptg-master-*`/`ds-form-*`/`row g-3`/`ptg-master-input-shell`/icon-chrome به `ak-form-section` + `_AkSectionHead` + `ak-form-grid` + `ak-field`/`ak-input`/`ak-toggle-field` منتقل شدند. `validation-summary` → `ak-form-alert`.
- `Sarrafs/Create.cshtml` هم به Canvas تخت ak (AkPageHeader + ak-form + AkFooterActions) بازنویسی شد؛ حذف `card/app-form-card/ds-form-shell/ptg-card/ds-page/people-parties-page` + Breadcrumb.

**حفظ‌شده و تأییدشده (بدون تغییر):** همه `asp-for`/id/name، `asp-validation-for`، `@Html.AntiForgeryToken()`، `<input hidden Id>`، `data-modal-preview-source` روی فیلد نام (برای preview title)، مجموعه فیلدها (بدون افزودن/حذف)، `ViewBag.ProviderTypes`. hookهای Modal (`data-ptg-entity-modal-form`, `.ptg-modal-form-scroll`, `data-entity-modal*`, `.ptg-modal-submit`, Ajax submit/redirect/auto-select) دست‌نخورده — این‌ها در Shell مشترک قرار دارند، نه در پارشال فرم.

**آمار:** ۶ پارشال + Sarrafs/Create؛ ≈ +205 / −250 خط (کاهش خالص ≈ −45 در این پارشال‌ها).
**Build ✅ · Test 853 pass / 44 fail = baseline (صفر شکست جدید).** بدون Commit.

### چرا Shell/JS و بقیه موارد این prompt در این Batch اجرا نشدند (تصمیم ایمنی)
1. **`_CreateModalShell` و `_CreatePageShell` سراسری‌اند** — توسط ~۲۰ موجودیت مصرف می‌شوند (Products, Trucks, Wagons, Drivers, Vessels, StorageTanks, CashAccounts, Currencies, Units, Terminals, Locations, ExpenseTypes, PlattsRates, Employees و طرف‌حساب‌ها). بازطراحی Shell = تغییر هم‌زمان همه Create modalهای برنامه در حالی‌که ۱۴ پارشال غیرطرف‌حساب هنوز `ptg-master-*` هستند → mismatch. پس Shell یک **Batch سراسری جدا (B7)** است، نه طرف‌حساب.
2. **پاک‌سازی `modal-design-system.js`** فعلاً ناامن است: hookهای فعلی هنوز توسط ۱۴ موجودیت غیرطرف‌حساب زنده مصرف می‌شوند. حذف hook = شکستن Ajax آن‌ها.
3. **`AkEntityCombobox` (بندهای ۶–۱۱)** به فرم‌های طرف‌حساب مربوط نیست (تنها Select آن‌ها `ProviderType` از نوع enum است، نه موجودیتی). این کامپوننت روی Selectهای موجودیتی فرم‌های **مالی/عملیاتی زنده** (Payments, Sales, Journal, Receipts …) اعمال می‌شود که مقدارشان محرک محاسبات و Ajax زنده است. طبق قاعده «توقف فقط در خطر واقعی Binding/Ajax»، این کامپوننت باید در Batch مستقل خودش با تست دقیق ساخته و به‌صورت progressive-enhancement روی `native select` (حفظ id/name) اعمال شود — نه هم‌بسته با مهاجرت Modal طرف‌حساب.

### هنوز زنده (حذف‌نشده، با دلیل)
`ptg-master-*`, `ds-form-grid/-group`, `people-parties-page`, `app-form-card`, `ds-form-shell`, `ptg-card`, `ds-page`, کل chrome `ptg-modal-*` — همگی مصرف‌کننده زنده دارند (فرم‌های Create ~۱۴ موجودیت غیرطرف‌حساب، Employees، Shellهای سراسری، تست `.app-form-card`). حذف پس از B7 (Shell سراسری) و مهاجرت پارشال‌های باقی‌مانده.

### نقطه دقیق ادامه — B7 (بزرگ، سراسری)
مهاجرت باقی پارشال‌های `_CreateForm`/`_Form` (~۱۴ موجودیت master-data/vehicle/asset) به ak-form → سپس بازطراحی `_CreateModalShell` + `_CreatePageShell` به ak-modal/ak-form تخت (حذف preview-card/visual-art/ptg-reference rail) با حفظ کامل hookهای Ajax، سپس pruning `modal-design-system.js`. سپس Batch مستقل `AkEntityCombobox`.

## ۱۳) اصلاح نمایش Dropdown فیلتر تعاریف پایه

- در `wwwroot/css/ptg/50-ak-components.css`، مقدار `display: grid` از حالت پایه `.ak-filter-pop` حذف و فقط به `.ak-filter-pop.show` منتقل شد.
- نتیجه: Bootstrap در حالت عادی Dropdown را کاملاً مخفی نگه می‌دارد و grid فقط هنگام اضافه‌شدن کلاس `show` فعال می‌شود.
- موقعیت، RTL، عرض `18rem`، padding و فاصله داخلی دست‌نخورده ماند.
- هیچ CSS صفحه‌ای، `!important`، JavaScript یا تغییر View اضافه نشد؛ هر ۱۵ View مصرف‌کننده بدون تغییر باقی ماند.
- check ساختاری: ۱۵ مصرف‌کننده؛ حالت پایه فاقد `display`؛ حالت `.show` دارای `display: grid`؛ عرض و padding محفوظ؛ `!important` صفر.
- رفتار Bootstrap محلی: کلیک بیرون و `Escape` Dropdown را می‌بندد. به‌دلیل قرارداد موجود `data-bs-auto-close="outside"`، تغییر گزینه داخل `select` به‌تنهایی آن را نمی‌بندد؛ دکمه «اعمال» با submit/navigation آن را می‌بندد. برای حفظ ممنوعیت تغییر ۱۵ View، این قرارداد تغییر نکرد.
- Full solution build: موفق، ۰ خطا و ۲ warning موجود مربوط به Npgsql در ابزار `ClearDbExceptUsers`.
- Full Web test: `853 pass / 44 fail / 0 skipped` از ۸۹۷ تست؛ دقیقاً برابر baseline ثبت‌شده و شکست جدید صفر.
- بررسی تعاملی مرورگر انجام نشد چون مرورگر داخل برنامه در دسترس نبود؛ رفتار با قرارداد CSS و Bootstrap محلی بررسی شد.
- Migration و Commit انجام نشد.

## ۱۳) Batch B7 — Shell مشترک Create (Modal + Page) → ak، و مهاجرت فرم‌های master-data

### Shell مشترک بازطراحی‌شد (یک پیاده‌سازی رسمی، بدون Variant صفحه‌ای)
- `Views/Shared/_CreateModalShell.cshtml` → Modal تخت `ak-modal` (`ak-modal-head/-title/-close/-body/-foot`). حذف کامل chrome قدیمی: `ptg-modal-workbench`, `ptg-reference-*` rail، `ptg-modal-preview-card`، `_ModalVisualArt`، و تمام متدهای `Resolve*`/variant/visual-text.
- `Views/Shared/_CreatePageShell.cshtml` → Canvas تخت `ak-form-page` با `_AkPageHeader` + `ak-form` + `_AkFooterActions`. حذف preview card / visual art / fixed-action bar / breadcrumb.
- CSS جدید در `50-ak-components.css`: بخش «AK MODAL + CREATE-PAGE SHELL» (`ak-modal-*`) + `.ak-note` (متن راهنما). فقط Tokens، بدون `!important`.

### hookهای Ajax/Modal کاملاً حفظ شد (بدون تغییر JS)
`data-entity-modal`, `data-ptg-entity-modal-form`, `data-entity-modal-close`, `.ptg-modal-form-scroll` (لنگر swap اعتبارسنجی)، `form="@formId"`، submit خارج از form، controller-extraction، شاخه `formNoSpa`، `enctype`. در نتیجه `modal-design-system.js` (submit/redirect/validation-swap/auto-select/open-close) بدون هیچ تغییری کار می‌کند. `.ak-form-page .ptg-modal-form-scroll` خنثی‌سازی شد تا در صفحه کامل جعبهٔ اسکرول نشود.

### فرم‌های Create مهاجرت‌شده به ak-form (Markup واقعی)
۱۲ پارشال master-data/vehicle/asset: Trucks, Drivers, Vessels, Wagons, Products (۲ سکشن به‌جای details)، Units (۲ سکشن)، Currencies, ExpenseTypes, Locations, Terminals, StorageTanks, CashAccounts (۲ سکشن). به‌همراه ۶ پارشال طرف‌حساب (B6) → مصرف `ptg-master-*` از ~۱۴ به **۲** رسید.
حفظ کامل: asp-for/id/name، `asp-validation-for`، antiforgery، selectها (`asp-items`, `ViewBag.*`, hidden Id)، `data-modal-preview-source`.

### تست‌ها به قرارداد جدید منتقل شد
- `ShellViewStructureTests`: مارکر `_CreateModalShell` از `ptg-modal-shell` → `ak-modal`.
- `ContractJourneyViewStructureTests.Shared_Create_Modals_...` و `Employee_Create_Modal_...`: assertهای chrome قدیمی → قرارداد ak (ak-modal/-head/-title/-close/-foot، ak-form، ak-form-page، `_AkPageHeader`/`_AkFooterActions`، و حفظ hookهای Ajax). این دو تست از قبل هم قرمز بودند چون فایل حذف‌شدهٔ `wwwroot/css/modal-design-system.css` (و `view-structure.css`/`loading-create.css`/`storage-tanks.css`) را می‌خوانند؛ بدهی تست جدا و نامرتبط با این Batch.

**Build ✅ · Test 853 pass / 44 fail = baseline دقیق (صفر شکست جدید).** بدون Commit/Migration.

### هنوز زنده (حذف‌نشده، دلیل)
- `ptg-master-*`: ۲ مصرف باقی (`Employees/_Form`, `Users/_CreateForm`) → حذف CSS پس از صفر شدن.
- `ptg-modal-*`/`ptg-reference-*`/`_ModalVisualArt`/preview JS: هنوز توسط `_EditPageShell`, `_ModalShell`, `StorageTanks/Details`, و فرم‌های عملیاتی مصرف می‌شوند.
- `_CreateModalContent` (Employees، فرم تب‌دار multipart با `employee-modal-*`) و `PlattsRates/_CreateDailyForm|_CreateManualForm` عمداً مهاجرت نشدند (الگوی چند-سکشن/Ajax نرخ)؛ داخل Shell جدید با استایل مستقل خودشان رندر می‌شوند. → Batch فرم‌های چند-سکشن.

### نقطه ادامه
1. مهاجرت `Users/_CreateForm` + `Employees/_Form`/`_CreateModalContent` → صفر شدن `ptg-master-*` → حذف CSS آن.
2. بازطراحی `_EditPageShell` + `_ModalShell` به ak (تا preview/ptg-reference صفر شود، سپس حذف `_ModalVisualArt` + pruning `initializePreviewCard`).

## ۱۴) Batch B8 — Form Foundation قطعی + AkEntityCombobox + خانواده‌های اداری

### قرارداد مشترک فرم تکمیل شد
- `50-ak-components.css` همان فایل Design System موجود باقی ماند؛ Skin، CSS صفحه‌ای یا `!important` جدید ساخته نشد.
- فاصله سکشن‌ها `52px`، label برابر `14/20`، textarea برابر `96px`، ورودی/Select برابر `42px` و Footer Actions مطابق مرجع تثبیت شد.
- `ak-save` برابر `84×36` با Radius `12px` و رنگ‌های `#6EA152/#53793E`؛ `ak-cancel` دکمه متنی بدون background.
- `ak-upload` مشترک با ارتفاع `80px`، border dashed و preview اختیاری ساخته شد.

### AkEntityCombobox مشترک
- `TagHelpers/AkEntityComboboxTagHelper.cs` فقط Selectهای موجودیتی `asp-for` را علامت‌گذاری می‌کند؛ enum/status/modeها دست‌نخورده می‌مانند و opt-out صریح پشتیبانی می‌شود.
- `wwwroot/js/ak-entity-combobox.js` یک progressive enhancement واحد است: search داخل فیلد، dropdown هم‌عرض، avatar/metadata، empty state، quick create در page-modal، keyboard navigation، Escape، click-outside، RTL و animation `180/140ms`.
- native select حذف نمی‌شود و همان `asp-for/id/name/data-*`، validation و Model Binding را نگه می‌دارد. انتخاب custom روی native select رویدادهای `input` و `change` واقعی منتشر می‌کند؛ MutationObserver گزینه‌های Ajax و محتوای SPA/modal را همگام می‌سازد.
- Asset در `_Layout` و `_ModalLayout` لود شد؛ `_ViewImports` TagHelper پروژه را فعال کرد.

### خانواده‌های مهاجرت‌شده در این Batch
- Users: Create + Edit + Role entity select؛ حذف account-card و فرم دوپنلی قدیمی.
- Employees: Create + Edit + Create modal روی `_Form` واحد؛ حذف tab/cardهای مودال و تبدیل PhotoFile به `ak-upload`.
- DailyFxRates: Create/Edit روی `_Form` واحد با currency combobox.
- Roles: Create/Edit و permission grid تخت بدون Card.
- OperationalAssets: Create/Edit/_Form روی PageHeader، section، grid و footer مشترک؛ تمام data-hookهای asset حفظ شد.
- ExpenseRules/Create (Create/Edit route مشترک): فرم تخت، entity selects و توضیح کوتاه محاسبه؛ هیچ محاسبه‌ای تغییر نکرد.

### اعتبار و مرز این Batch
- `node --check wwwroot/js/ak-entity-combobox.js` موفق.
- Web build موفق: ۰ خطا / ۰ warning در اجرای نهایی بدون قفل فایل.
- Full Web test: `854 pass / 43 fail / 0 skipped` از ۸۹۷؛ baseline قبلی `853/44` بود، شکست جدید صفر و یک شکست قدیمی کمتر شد.
- Controller، Service، Route، Permission، Entity، DbContext، Migration و محاسبات تغییر نکرد.
- Commit و Migration انجام نشد.
- برنامه محلی با runner واقعی و دیتابیس PostgreSQL بالا آمد؛ اما مرورگر داخل برنامه در این نشست موجود نبود، بنابراین screenshot comparison در viewport ثابت انجام نشد.
- **درخواست سراسری هنوز کامل نیست:** Payments، Sales، Contracts، Loading/Receipts، Dispatch، Expenses، Inventory Transport، Shipments و فرم‌های مالی/عملیاتی بزرگ هنوز markup/CSS اختصاصی زنده دارند و باید خانواده‌به‌خانواده با مرورگر فعال مهاجرت و legacy هر خانواده بعد از صفر شدن مصرف حذف شود.

## ۱۵) حذف Modal افزودن از تعاریف پایه، حمل‌ونقل و طرف‌حساب‌ها

### دامنه
- تعاریف پایه: Products, Units, Currencies, DailyFxRates, Locations, ExpenseTypes, ExpenseRules, StorageTanks, Terminals.
- حمل‌ونقل: Trucks, Wagons, Drivers, Vessels.
- طرف‌حساب‌ها: Suppliers, Partners, Companies, Customers, ServiceProviders, Sarrafs, Employees.

### تغییر انجام‌شده
- در هر ۲۰ Index، عملیات افزودن اکنون `Url.Action("Create")` است؛ هیچ `ActionModalTarget`، `_CreateModalShell`، `data-entity-modal-open` یا ViewData مربوط به Create Modal باقی نماند.
- تمام Createها با `_CreatePageShell` یا `ak-form-page` مشترک باز می‌شوند؛ فرم‌ها روی Canvas صفحه‌اند، نه داخل Modal.
- wrapper قدیمی `.ptg-modal-form-scroll` از `_CreatePageShell` حذف شد و partial فرم مستقیماً داخل `.ak-form` رندر می‌شود.
- `Employees/_CreateModalContent.cshtml` چون مصرف‌کننده نداشت حذف شد؛ فرم کارمند Create/Edit از `_Form.cshtml` واحد استفاده می‌کند.
- hookهای `data-modal-preview-source` بی‌مصرف از partialهای Currencies و طرف‌حساب‌ها حذف شد.
- بارگذاری `modal-design-system.js` برای این خانواده‌ها قطع شد؛ فقط CashAccounts، PlattsRates و LoadingReceipts که خارج از این دامنه‌اند باقی ماندند.

### مسیرهای خاص حفظ‌شده
- ServiceProviders/Create GET اکنون صفحه Create و lookupهای لازم را برمی‌گرداند؛ validation ناموفق نیز همان صفحه را با ModelState برمی‌گرداند، نه Index+Modal.
- Clone ارز حذف نشد: عملیات clone به `Currencies/Create?cloneFromId=` منتقل شد و GET Create فرم صفحه‌ای را با داده منبع پر می‌کند؛ POST و validation اصلی بدون تغییر ماند.

### اعتبار
- Web build موفق: ۰ خطا، ۲ warning قدیمی و نامرتبط.
- تست هدفمند: ۹/۹ پاس (Products، ServiceProviders و قراردادهای ساختاری page-create/modal-removal).
- Controller business flow، Service، Entity، DbContext، Migration، Permission و محاسبات تغییر نکرد.
- Commit و Migration انجام نشد.
- برنامه محلی روی `http://localhost:5000` اجرا شد؛ مرورگر داخل برنامه در این نشست موجود نبود، پس بررسی تعاملی کلیک انجام نشد.
3. سپس Batch مستقل **AkEntityCombobox** (Selectهای موجودیتی فرم‌های مالی/عملیاتی زنده؛ progressive-enhancement روی native select، حفظ change/Ajax/محاسبات).

## ۱۴) Batch B8 — Shell‌های Edit/Modal → ak، صفرشدن ptg-master، pruning JS

### Shell مشترک Edit بازطراحی شد
- `Views/Shared/_EditPageShell.cshtml` → Canvas تخت `ak-form-page` (`_AkPageHeader` back→Details + `ak-form` + hidden `Id` + partial + `_AkFooterActions`). حذف `ptg-modal-workbench/-shell/-preview-card`, `_ModalVisualArt`, `app-form-card/ptg-card`, breadcrumb، متد `ResolvePageVariant`. حفظ `asp-action="Edit"`, `asp-route-id`, hidden Id، antiforgery/validation (از partial). ۱۲ صفحه Edit مستر‌دیتا (Trucks…CashAccounts) که همان `_CreateForm` ak را رندر می‌کنند اکنون کاملاً تمیز شدند.

### Partialهای مرده حذف شد
- `Views/Shared/_ModalShell.cshtml` و `Views/Shared/_ModalPreviewCard.cshtml` → صفر مصرف‌کننده → حذف کامل. (`_ModalVisualArt` فقط برای `StorageTanks/Details` مانده و نگه داشته شد.)

### JS pruning
- پس از صفرشدن emitter‌های `data-modal-preview-card`/`data-modal-preview-title` (همه Shellها preview را حذف کردند)، تابع `initializePreviewCard` و فراخوانی آن از `modal-design-system.js` حذف شد. سایر hookها (entity-modal submit/triggers/formTabs/receiptCreateForm) دست‌نخورده.

### آخرین فرم‌های ptg-master → ak
- `Users/_CreateForm.cshtml` و `Employees/_Form.cshtml` به ak-form منتقل شدند (حفظ PhotoFile/enctype، selectها، asp-for/id/name، antiforgery). **`ptg-master-*` در Views اکنون صفر است.**

### حذف CSS مردهٔ ptg-master
- `17-system-forms.css`: بلاک اختصاصی `.ptg-master-form-grid` (grid دو‌ستونه) حذف شد؛ توکن‌های `ptg-master-*` از selectorهای مشترک `:is()` (که کلاس‌های زنده مثل `.ds-form-group`, `.modal-form-grid`, `.loading-reference-fields` را هم هدف می‌گیرند) پاک شد بدون شکستن قواعد زنده. **تعادل brace: 247/247.**
- `08-modals.css`: ۳۵ ارجاع باقی‌ماندهٔ `ptg-master-*` همگی درون scope مردهٔ `.app-modal.ptg-reference-modal` هستند (هیچ Modalی دیگر `ptg-reference-modal` ندارد). چون با کلاس‌های زندهٔ `employee-modal-*`/`party-modal-fields` در همان scope مرده در هم تنیده‌اند، حذف امنِ آن‌ها به Batch اختصاصی «Audit CSS مودال/45-akaunting» موکول شد.

### تست‌ها به قرارداد ak منتقل شد (تضعیف/حذف نشد)
- `Shared_Create_Modals_And_Form_Pages_...` و `Employee_Create_Modal_...`: خواندن فایل حذف‌شدهٔ `modal-design-system.css` (و سایر cssهای حذف‌شده) برداشته شد؛ اکنون قرارداد واقعی ak را assert می‌کنند (ak-modal/-head/-foot، ak-form، ak-form-page، `_AkPageHeader`/`_AkFooterActions`، حفظ hookهای Ajax، و pruning `initializePreviewCard`). **هر دو اکنون سبز.**

**Build ✅ · Test 855 pass / 42 fail** (baseline قبلی 853/44 → +۲ pass، −۲ fail، **صفر شکست جدید**). بدون Commit/Migration.

### باقی‌مانده / نقطه ادامه
1. بدهی تست باقی‌مانده که هنوز `modal-design-system.css` حذف‌شده را می‌خواند: `Master_Data_Modals_Use_Reference_Borderless_Field_Treatment`, `Modal_Visual_Art_Uses_Contextual_Real_Images` (جزو ۴۲ قبلی، نامرتبط با Shell؛ در Batch Audit CSS مودال).
2. حذف کامل scope مردهٔ `.app-modal.ptg-reference-modal` از `08-modals.css` + بقایای ptg-modal/ptg-reference.
3. `data-modal-preview-source` روی فیلد نام‌ها اکنون بی‌اثر است (hook مرده) — پاک‌سازی جزئی بعداً.
4. فرم‌های چند-سکشن/PlattsRates.
5. سپس Batch مستقل **AkEntityCombobox**.

## ۱۶) قرارداد returnUrl برای ۲۰ صفحه Create

- هر ۲۰ لینک Index، مسیر کامل همان فهرست را همراه query/filter/page به شکل `Url.Action("Create", new { returnUrl })` می‌فرستد.
- `_CreatePageShell` و چهار Create سفارشی DailyFxRates، ExpenseRules، Sarrafs و Employees فقط `returnUrl` محلی را قبول می‌کنند؛ Back، Cancel و POST همان مسیر را حفظ می‌کنند و مسیر غیرمحلی به Index fallback می‌شود.
- POST Create در هر ۲۰ Controller پارامتر اختیاری `returnUrl` دارد. پس از ثبت موفق فقط در صورت `Url.IsLocalUrl` با `LocalRedirect` به مبدأ برمی‌گردد؛ fallback قبلی هر Controller (Index یا Details) دست‌نخورده است.
- شاخه‌های Validation همان View، ModelState، مدل واردشده و lookupهای قبلی را برمی‌گردانند؛ منطق ثبت، محاسبه، Service و business flow تغییر نکرد.
- Quick Create مربوط به `AkEntityCombobox` حذف یا با دکمه صفحه جایگزین نشد. مسیر Modal کوچک (`akQuickCreateTarget`/`akQuickCreateUrl`)، native select، eventهای `input/change` و MutationObserver حفظ شد تا قرارداد آینده بازگشت نتیجه و auto-select بدون refresh قابل تکمیل بماند.
- `_CreateModalShell`، submit زنده Ajax و مصرف‌کننده‌های واقعی CashAccounts، PlattsRates و LoadingReceipts حفظ شدند و تست ساختاری مستقل دارند.

### اعتبار

- Web build: موفق، ۰ خطا و ۰ warning.
- تست هدفمند: ۱۱/۱۱ پاس (Products، ServiceProviders، قرارداد ۲۰ Create، Quick Create و Modalهای زنده).
- Full test نهایی: `857 pass / 42 fail` از 899؛ baseline پیش از این اصلاح `855/42` بود. دو تست محافظ جدید پاس شده و failure جدید صفر است.
- یک regression ساختاری آشکارشده در Full test اصلاح شد: فیلد backend `Units.NamePersian` به `_CreateForm` مشترک برگشت؛ هیچ فیلدی حذف نماند.
- برنامه محلی روی `http://localhost:5000` با PostgreSQL اجراست. endpoint محافظت‌شده پاسخ 302 ورود می‌دهد؛ مرورگر داخلی این نشست در دسترس نبود، بنابراین تست کلیکی authenticated اجرا نشد.
- Migration و Commit انجام نشد.

## ۱۶) Batch B9 — شروع مهاجرت خانواده عملیات/مالی (form-by-form ایمن)

**روش تأییدشده کاربر:** form-by-form محتاط؛ ابتدا وابستگی JS هر فرم map شود، سپس فقط wrapper/layout به قرارداد ak منتقل شود؛ همه `id/name/asp-for/data-*/validation/JS hook` حفظ. Build + Full test بعد از هر گروه. بدون مرورگر داخلی این نشست.

### وضعیت واقعی کشف‌شده (مهم)
- **Tabها (بند ۲ prompt) از قبل کامل‌اند.** هر سه پارشال (`_SectionTabs`, `_FormTabs`, `_DetailsTabs`) و همه railهای journey/details/form از یک کامپوننت مشترک واحد عبور می‌کنند: کلاس `ptg-tabs-rail`/`ptg-tab-item` → `wwwroot/css/ptg/16-system-tabs.css` + override `45-akaunting.css` (§K، خطوط ۶۷۸–۷۰۰). ظاهر مؤثر = underline **2px**، متن **14px/500**، Scroll موبایل، بدون Pill/Card. هیچ `nav-tabs`/`nav-pills` خام باقی نمانده. تنها ظرافتِ نبود: خط پایه ۱px پیوسته زیر ریل (underline فعال ۲px بدون آن هست). → بند ۲ عملاً satisfied؛ تغییر بیشتری لازم نشد.

### فرم‌های مهاجرت‌شده در این Batch (Markup واقعی روی Canvas)
1. **`LossEvents/Create.cshtml`** ✅ — `card app-form-card ds-form-shell` + `ptg-operation-header` + `contract-form-sections` + `row g-3 col-md-*` + `card-footer ds-form-actions` → `ak-form-page` + `_AkPageHeader` + `ak-form-section`/`_AkSectionHead` + `ak-form-grid`/`ak-field`/`ak-input` + `_AkFooterActions`. حفظ کامل JS: اسکریپت inline مرجع‌گزینی keyed روی `data-loss-reference-type/-item/-backing` + backingهای `asp-for` (Contract/LoadingRegister/LoadingReceipt/TruckDispatch/Sales/Shipment/Terminal/StorageTank). `<details class="ds-advanced">` به‌عنوان disclosure نگه داشته شد (نه wrapper form-shell؛ الگوی مشترک ops، خارج از دامنه این فرم). `ViewData` (LoadingCreateAssets, ContentBodyClass, HideSectionTabs) دست‌نخورده. `ReturnUrl` hidden verbatim.
2. **`Expenses/Create.cshtml`** ✅ — همان تبدیل. حفظ کامل hookها: `data-expense-type-entry/-id/-manual` + datalist، `data-fx-rate-group/-field/-input/-technical` + `data-fx-currency-source="#Currency"` + id `expenseFxDisplay` (وابسته `fx-rate-field.js`)، بلوک `data-expense-operation-link` با `data-selected-type/-value`, `data-expense-source-options`, `data-expense-link-target` و idهای `ExpenseOperationLinkType/-Record` (وابسته expense-operation-link JS از `ExpenseFormAssets`). `Id`/`ReturnUrl` hidden + `asp-route-id` + formAction Edit/Create دست‌نخورده.

### تست‌ها (به‌روزشده طبق قاعده «ساختار مجاز، منطق نشکند»)
- `ShipmentPnlControllerTests.Related_Transport_Forms_Use_Modern_Form_Shells`: assertهای `loss` از `ds-form-shell`/`ptg-operation-header` → `ak-form`/`_AkPageHeader`. (assertهای receipt/sale/expense/customs دست‌نخورده چون هنوز مهاجرت نشده‌اند.)
- سایر تست‌های مرتبط (`Create_Views_Preserve_ReturnUrl_...`, `Operational_Create_Forms_...`) بدون تغییر پاس — چون فقط وجود `ReturnUrl` را assert می‌کنند که حفظ شد.

### اعتبار Batch B9
- Web build: **0 error / 0 warning**.
- Full test: **857 pass / 42 fail / 899 total**؛ تعداد fail = baseline قبلی (۴۲)، **صفر شکست جدید**. هیچ‌کدام از تست‌های LossEvents/Expenses/ShipmentPnl/Related_Transport/ReturnUrl در مجموعه‌ی fail نیستند (۴۲ همان بدهی قدیمی خواندن CSSهای حذف‌شده §۱۴ است).
- بدون Commit/Migration. مرورگر داخلی نبود → QA کلیکی authenticated انجام نشد؛ ایمنی از حفظ کامل hookها + سبزماندن تست تأمین شد.
- **نکته محیط:** یک runner محلی مکرراً app را روی port 5000 بالا می‌آورد و `bin/.../PTGOilSystem.Web.dll` را قفل می‌کند و build را می‌شکند (MSB3027). قبل از هر build باید listener روی 5000 kill شود؛ سپس `dotnet build` سپس `dotnet test --no-build` (اجرای موازی build دو بار → CS0016 sourcelink).

### نقشهٔ ریسک JS فرم‌های باقی‌ماندهٔ عملیات/مالی (برای Batch بعدی)
- **پرریسک‌ترین — `Payments/Create.cshtml`** (روزنامچه): کلاس‌های `journal-*` + دهها `data-*` وصل به ۴ فایل: `roznamcha-form.js`, `finance-forms.js`, `fx-rate-field.js`, `commission-field.js` (direction/method switch، counterparty panels با `data-counterparty-panel`، sarraf با `data-sarraf-*`، commission با `data-commission-*`، party-balance با `data-party-balance-url`). برخی سوییچ‌ها با کلاس toggle می‌شوند؛ هنگام تعویض wrapper باید همان data-* و ساختار panelها بمانند. **بدون مرورگر، پرخطر؛ آخر انجام شود.**
- **متوسط:** `Sales/Create` (`data-sales-*`: contract/stage/shipment/fx/total/stock-balance-url)، `Dispatch/Create` (`data-freight-rate-preview`/`data-shortage-deduction-preview` + fields)، `LoadingReceipts/_ReceiptCreateForm` (`data-receipt-create-form` + `data-scenario-pick` + `initializeReceiptCreateForm` در modal-design-system.js؛ **تست pixel-design سختگیر** §۴ ContractJourneyViewStructureTests — نیازمند بازنویسی گستردهٔ تست).
- **کم‌ریسک (data-*/id keyed، مثل B9):** `Sales/CreateFromShipment`, `InventoryTransportReceipts/Create`, `InventoryTransportLegs/CreateGroupExpense`, `LossEvents/Edit`, `CustomsDeclarations/Create` (این چهارتای اول در تست واحد `Related_Transport_Forms_Use_Modern_Form_Shells` assert شده‌اند → با مهاجرت، همان assertها به قرارداد ak به‌روز شوند؛ customs یک assert جدا `contract-form-sections` §خط ۱۲۱۰ هم دارد).

### نقطهٔ دقیق ادامه (Batch B10)
گروه کم‌ریسک بالا را form-by-form به ak منتقل کن (همان الگوی B9: `_AkPageHeader` + `ak-form` + `ak-form-section`/`_AkSectionHead` + `ak-form-grid`/`ak-field`/`ak-input` + `_AkFooterActions`؛ حفظ همه hook)، تست‌های `Related_Transport_Forms_Use_Modern_Form_Shells` + customs `contract-form-sections` را همگام کن. سپس فرم‌های Details/Edit عملیات، و در پایان Payments/Sales/Dispatch/Receipt پرریسک با مرورگر فعال. حذف CSS/selector هر خانواده فقط پس از صفرشدن مصرف کل آن خانواده.

## ۱۷) Batch B10 — تکمیل گروه کم‌ریسک عملیات/مالی

### فرم‌های مهاجرت‌شده
- `Sales/CreateFromShipment.cshtml` — انتقال کامل به `_AkPageHeader` + `ak-form` + سکشن/گرید/فیلدهای مشترک + جدول `ak-table` + `_AkFooterActions`. همهٔ hookهای `data-sale-*`، `data-contract-row`، summary زنده، `PrintAfterSave`، `ShipmentId` و `ReturnUrl` حفظ شد.
- `InventoryTransportReceipts/Create.cshtml` — انتقال wrapper/card/panel/input/footer به قرارداد ak. همهٔ hookهای مقصد، کرایه، کسری، فروش مستقیم، ارسال مستقیم، FX، preview و `itr-unload-page` (selector زندهٔ JS) حفظ شد.
- `InventoryTransportLegs/CreateGroupExpense.cshtml` — انتقال کامل فرم و جدول تخصیص به ak؛ `GroupKey`/`TransportReference`/`TotalAllocatedQuantityMt`/`ReturnUrl`، hook انتخاب مسئول مصرف و FX دست‌نخورده ماند.
- `LossEvents/Edit.cshtml` — حذف Breadcrumb/Card/summary wrapper قدیمی؛ همه hidden bindingهای منبع و موجودی، route id، antiforgery، validation و ReturnUrl حفظ شد.
- `CustomsDeclarations/Create.cshtml` — هر دو حالت انتخاب منبع (GET) و ثبت/ویرایش (POST) به ak منتقل شد؛ `Items[i]`، template ردیف پویا، id/nameهای محاسبه و selectorهای JS حفظ شد.

### پاک‌سازی Legacy
- `wwwroot/css/ptg/18-shipment-flow-sale.css` پس از صفرشدن مصرف کامل حذف شد.
- selector مردهٔ `.customs-label-cell` و عضویت بی‌مصرف `.customs-create-form` از `06-forms.css`، `13-compat.css` و `17-system-forms.css` حذف شد.
- `30-inventory-transport-receipt-skin.css` حذف نشد: `Dispatch/Unload.cshtml` و `InventoryTransportLegs/CreateGroupReceipt.cshtml` هنوز مصرف‌کنندهٔ زندهٔ خانواده `shipment-receipt-*` هستند.

### تست‌ها و اعتبار
- assertهای `Related_Transport_Forms_Use_Modern_Form_Shells`، Customs returnUrl و markerهای Shell به قرارداد ak منتقل شدند؛ تست‌ها تضعیف نشدند و نبود Legacy را هم assert می‌کنند.
- زیرگروه ۱: Web Build ✅؛ Full test **858 pass / 41 fail / 899**.
- زیرگروه ۲: Web Build ✅؛ Full test **858 pass / 41 fail / 899**.
- baseline ورودی **857 pass / 42 fail** بود؛ نتیجه یک pass بهتر، یک fail کمتر و **صفر شکست جدید** است.
- قبل از هر Build پورت 5000 بررسی/آزاد شد. Build اول بدون خروجی timeout شد؛ retry تک‌پردازشی موفق بود. بدون Commit و بدون Migration؛ Controller/Service/Business Logic/Ajax/محاسبات تغییر نکرد.

### ارزیابی dependency خانواده‌های پرریسک — شروع نشد
- `Payments/Create` (۶۲۸ خط): `roznamcha-form.js` + `commission-field.js` + `fx-rate-field.js`، دو flow نقد/بانک و صراف، چندین id/data hook و summary پویا؛ Batch مستقل با مرورگر لازم است.
- `Sales/Create` (۳۴۳ خط): mapهای قرارداد/محموله/مقصد، stock-balance Ajax و خانوادهٔ `sales-create-*`؛ باید همراه تست رفتاری و مرورگر یک‌جا تمام شود.
- `Dispatch/Create`: hookهای Excel/freight/shortage و flowهای مستقیم وابسته؛ جدا و کامل مهاجرت شود.
- `LoadingReceipts/Create` + `_ReceiptCreateForm` (partial بزرگ چندسناریویی): allocation lineهای تکرارشونده، loss mode و JS/CSS خانواده `receipt-*`؛ به‌هیچ‌وجه نیمه‌کاره شروع نشود.

### نقطهٔ دقیق ادامه (Batch B11)
با مرورگر فعال فقط یک خانواده را انتخاب و کامل کن. کوچک‌ترین گزینهٔ بعدی `Sales/Create` است، اما قبل از تغییر باید dependency کامل JS/Ajax و همهٔ مصرف‌کنندگان `sales-create-*` map شود؛ سپس همان خانواده در یک Batch با Build + Full test پایان یابد. Payments/Dispatch/Receipt هم‌زمان شروع نشوند.

## ۱۸) Batch B11 — مهاجرت کامل Sales/Create

### dependencyهای واقعی بررسی‌شده
- View مشترک Create/Edit: `Views/Sales/Create.cshtml` با `SalesCreateViewModel`، `IsEdit/EditId`، route پویا، `ReturnUrl` محلی، antiforgery و `FormToken`.
- JavaScript فعال: فقط initializer فروش در `wwwroot/js/finance-forms.js` که با `data-sales-create-form="true"` شروع می‌شود.
- محاسبات نمایشی JS: `QuantityMt × UnitPriceInCurrency`، نمایش Total، FX به ارز پایه، summary مقدار/مبلغ/معادل؛ هیچ فرمول تغییر نکرد.
- Ajaxها: `SuggestedPrice` با `SourcePurchaseContractId` و `SourceStockBalance` با product/source contract/terminal/tank/saleDate؛ endpoint، پارامتر، header و AbortController دست‌نخورده.
- mapهای زنده: `ShipmentContractMap` و `SaleContractDestinationMap` برای اتصال Shipment→Sales Contract→Destination؛ Customer/Product/Company/Contract/Shipment و selectهای native حفظ شدند و به Combobox تبدیل نشدند.
- eventها: change مرحله، shipment، contract، product، terminal، tank، source contract، ticket، stock source و currency؛ input مقدار، قیمت و FX؛ submit برای re-enable قرارداد. همه حفظ شدند.
- وابستگی ساختاری کشف‌شده: تابع `fieldWrapper` کلاس‌های Legacy `.contract-form-section/.col-md-*` را selector می‌کرد. فقط selectorهای wrapper به `.ak-form-section/.ak-field/.ak-col-full` منتقل شد؛ منطق visibility، پاک‌کردن فیلدهای مرحله و confirm بدون تغییر ماند.

### Markup مهاجرت‌شده
- root → `ak-form-page`؛ header → `_AkPageHeader`؛ form → `ak-form`؛ همه سکشن‌ها → `ak-form-section` + `_AkSectionHead` + `ak-form-grid`/`ak-field`/`ak-input`؛ footer → `_AkFooterActions`.
- Breadcrumb، outer Card، `ds-form-shell`, `app-form-card`, `contract-form-section`, Bootstrap row/col و تمام کلاس‌های `sales-create-*`/`sales-items-*`/`sales-summary-*` حذف شدند.
- تنظیمات مرحله/منبع در disclosure مشترک `ds-advanced` باقی ماند؛ selectهای زنده native ماندند.
- جدول تک‌قلم فروش به `ak-table-wrap` + `ak-table` تخت منتقل شد؛ Product/Quantity/UnitPrice/Total و validationهایشان حفظ شدند.
- Summary ساده شد ولی همه hookهای `data-sales-summary-*`, `data-sales-save-summary-*`, `data-sales-total-*` حفظ شدند.
- audit token: هیچ `asp-for`, `id`, `name`, `data-*`, `asp-validation-for`, antiforgery, FormToken یا ReturnUrl حذف نشد؛ count `data-*` قبل/بعد = ۳۹/۳۹ و id = ۸/۸.

### پاک‌سازی CSS Legacy
- بلاک اختصاصی و `!important`دار Sales/Create در `17-system-forms.css` (از marker `Sales create/edit` تا پیش از `GroupTransfer`، حدود ۱۴٬۰۴۳ کاراکتر) کامل حذف شد.
- قواعد اختصاصی `.sales-save-summary-list` حذف شد.
- عضویت صفرمصرف `.sales-create-form` از selectorهای مشترک `06-forms.css`, `13-compat.css`, `17-system-forms.css` حذف شد.
- CSS/Skin/Variant/`!important` جدید ساخته نشد.

### تست و اعتبار
- `ShellViewStructureTests`: marker فروش از `ds-form-shell` به `ak-form` منتقل شد.
- `Sales_Create_Exposes_Sales_Contract_Destination_Fx_And_Total_Context`: قرارداد ak، نبود Legacy، حفظ همه hookهای فروش و selectorهای جدید wrapper در JS را assert می‌کند؛ assertهای مالی/Ajax قبلی حفظ شدند.
- `node --check wwwroot/js/finance-forms.js` ✅.
- قبل از Web Build، listener پورت 5000 با PID `19284` متوقف شد. پیش از build ضمنی Full Test نیز پورت آزاد بود.
- Web Build ✅ — صفر error، فقط دو warning قدیمی `Home/TrendClass` و `Maintenance/EF1002`.
- Full Test: **859 pass / 40 fail / 899**؛ baseline ورودی **858 pass / 41 fail** بود؛ یک pass بهتر، یک fail کمتر و **صفر شکست جدید**.
- Controller/Service/Business Logic/محاسبات/Ajax/DB تغییر نکرد. بدون Commit و بدون Migration.

### توقف
طبق دستور، Batch بعدی شروع نشد. `Payments/Create`, `Dispatch/Create` و `LoadingReceipts/Create` دست‌نخورده ماندند.
## ۱۷) Batch B12 — یکپارچه‌سازی واقعی تب‌های عملیات

- railهای `ContractJourney`، `ShipmentPnl/Details`، `Dispatch/Details`، `InventoryTransportLegs/Details` و `InventoryTransportLegs/Journey` فقط با `ptg-tabs-rail` و itemهای `ptg-tab-item` رندر می‌شوند؛ wrapper/card و کلاس‌های نمایشی موازی `cj-*`، `ptcd-*`، `shipment-file-*` و `journey-tabs*` از خود rail حذف شدند.
- hookهای زنده حفظ شدند: `data-contract-journey-*` برای Ajax/cache/history، `data-ptcd-*` برای panel switching، `data-bs-toggle/target` و id/hrefهای Bootstrap، و `data-shipment-file-tabs` برای handler پرونده محموله.
- partialهای مشترک `_FormTabs` و `_DetailsTabs` نیز rail/item واحد را emit می‌کنند؛ `details-tabs.js` روی قرارداد مشترک کار می‌کند و bridgeهای زنده خارج از این دامنه را حفظ کرده است.
- قرارداد canonical در `16-system-tabs.css`: baseline پیوسته 1px، underline فعال 2px، رنگ فعال `#55588B`، متن 14px/500 و scroll افقی موبایل. Skin/variant/فایل CSS جدید ساخته نشد.
- CSS مرده railهای قدیمی از `contract-journey*.css`، `20-shipment-file.css`، `24-inventory-transport-details.css` و `13-compat.css` حذف شد؛ CSS محتوای panelها و محاسبات نمایشی دست‌نخورده ماند.
- تست‌های ساختاری canonical با markup جدید هم‌راستا شدند. تست هدفمند canonical tabs پاس است.
- Web build: موفق، 0 error و 2 warning قدیمی/نامرتبط.
- Full test: `859 pass / 40 fail / 0 skipped` از 899؛ برابر baseline واقعی B11 و بهتر از baseline اعلامی `858/41`، شکست جدید صفر.
- Controller، Service، Entity، DbContext، Migration، Ajax و منطق مالی/عملیاتی تغییر نکرد. Commit و Migration انجام نشد.

### نقطه ادامه
- خانواده بعدی: `Loading` + `LoadingReceipts`. ممیزی dependency شروع شد؛ `Loading/Create` محاسبات quantity/price/RUB/freight/import و row editor زنده دارد و مهاجرت آن باید همراه با حفظ تمام `data-loading-*` hookها و بدون تغییر فرمول‌ها انجام شود.
## ۱۸) Batch B13 — مهاجرت کامل خانواده LoadingReceipts به ak

- `LoadingReceipts/Create` در هر سه حالت انتخاب منبع، صفحه کامل و iframe modal به `ak-form-page`/`ak-form` منتقل شد. فرم اصلی از `ak-form-section`، `ak-form-grid`، `ak-field`، `ak-input`، جدول `ak-table` و footer مشترک استفاده می‌کند.
- همه مسیرهای زنده حفظ شدند: `data-receipt-create-form`، تمام `data-scenario-*`، `data-loss-*`، `data-mixed-*`، idهای Receipt/Allocation/Quantity/Terminal/Tank/Customer/Truck/Driver، `asp-for`های allocation و sale، antiforgery، modal flag و `ReturnUrl`.
- `LoadingReceipts/Edit` به `_AkPageHeader` + `ak-form` + `_AkSectionHead` + `_AkFooterActions` منتقل شد؛ hiddenهای `Id`، `LoadingRegisterId` و `ReturnUrl` و همه پنج فیلد قابل ویرایش حفظ شدند.
- `LoadingReceipts/Details` بدون breadcrumb/card و با header/action/section/grid/status مشترک رندر می‌شود؛ محاسبات allocation، loss، progress و لینک‌های عملیات تغییر نکرد.
- `LoadingReceipts/Index` به `ak-list-page`، `_AkPageHeader`، `ak-filterbar`، `ak-list` و `ak-table` منتقل شد؛ query/date filters، idهای popup، row navigation و pagination حفظ شدند.
- پس از صفرشدن مصرف، block کامل CSS اختصاصی Loading Receipt از `17-system-forms.css` حذف شد. فایل CSS یا override جدید ساخته نشد.
- Web build: موفق، 0 error و 2 warning قدیمی/نامرتبط.
- Full test: `861 pass / 38 fail / 0 skipped` از 899؛ نسبت به B12 دو pass بیشتر و دو fail کمتر، شکست جدید صفر.
- Controller، Service، Entity، DbContext، Migration، Ajax و منطق موجودی/فروش/dispatch/مالی تغییر نکرد. Commit و Migration انجام نشد.

### نقطه ادامه
- زیرخانواده بعدی `Loading` است. dependencyهای `Loading/Create` ثبت شده‌اند: همه محاسبات quantity/price/Platts/RUB/freight، import Excel، contract checklist، row template و eventها روی `data-loading-*` حفظ شوند؛ چند selector ساختاری باقیمانده باید پیش از حذف کلاس‌های `loading-*` به data hook منتقل شوند.

## ۱۹) Batch B14 — مهاجرت کامل خانواده Loading به ak

### dependencyهای واقعی بررسی‌شده
- routeهای زنده `Index`، `Create`، `Details`، `Edit`، `EditPrice`، `EditExpenses`، `ImportWorkbook`، `SuggestedPricing` و `SetRubleRate` در `LoadingController` map شدند؛ Controller یا قرارداد endpoint تغییر نکرد.
- `Loading/Create` و `_LoadingRowEditor` تمام محاسبات quantity/Platts/loading price/RUB/freight، import Excel، contract checklist، transport mode، Jalali date picker، row template و eventهای موجود را حفظ کردند.
- modal/page مشترک مصارف از `_LoadingExpenseEditor`، `_LoadingExpenseLineRow` و `loading-expense-editor.js` استفاده می‌کند؛ parser ردیف فقط برای پشتیبانی از `<tr>` مشترک اصلاح شد و فرمول یا payload تغییر نکرد.

### Markup مهاجرت‌شده
- `Create`، `Edit`، `EditPrice` و `EditExpenses` به `ak-form-page`، `_AkPageHeader`، `ak-form`، `_AkSectionHead`، `ak-form-grid`/`ak-field`/`ak-input` و actionهای مشترک منتقل شدند.
- `Index` به `ak-list-page`، `ak-filterbar`، `ak-table`، `ak-status`، `ak-row-menu` و `ak-pager` منتقل شد؛ query/date/contract filters، row navigation و modal مصارف حفظ شدند.
- `Details` به header/action/section/grid/status/list مشترک منتقل شد؛ receipt modal، expense modal، RUB editor، customs/loss links، ReturnUrl و همه محاسبات نمایشی حفظ شدند.
- partialهای ردیف Loading و مصارف به ردیف واقعی جدول تبدیل شدند. audit روی `asp-for`، `name/id` و hookهای `data-loading-*`/`data-row-*` حذف binding زنده‌ای نشان نداد؛ لینک‌های breadcrumb قدیمی با URLهای همان مقصد در `_AkPageHeader` جایگزین شدند.
- Viewهای خانواده Loading از مجموع ۳۱۰۱ خط به ۲۸۱۳ خط رسیدند؛ ۲۸۸ خط کاهش خالص بدون حذف قابلیت.

### پاک‌سازی Legacy
- `ViewData["LoadingCreateAssets"]` مرده از چهار View حذف شد؛ مصرف‌کننده‌ای در Layout نداشت.
- selectorهای ساختاری JS از classهای نمایشی به data hook منتقل و styling تزریقی/inline حذف شد. CSS کوچک مشترک برای key/value list، contract checklist و Jalali dialog در `50-ak-components.css` با tokenهای موجود اضافه شد؛ skin/variant یا فایل CSS جدید ساخته نشد.
- blockهای `loading-create-*` و expense editor در `13-compat.css` حذف نشدند، چون `Dispatch/Create`، `Dispatch/CreateDirectFromReceipt`، `Expenses/CustomsBatch`، `Expenses/CreateWagonRent` و چند فرم `InventoryTransportLegs` هنوز مصرف‌کننده زنده دارند.

### تست و اعتبارسنجی
- `node --check wwwroot/js/loading-expense-editor.js`: پاس.
- Web build: موفق، 0 error و 2 warning قدیمی/نامرتبط (`Home/TrendClass` و `Maintenance/EF1002`).
- تست ساختاری Loading: `12 pass / 0 fail`؛ تست `LoadingReceiptControllerTests`: `22 pass / 0 fail`.
- کل `LoadingControllerTests`: `51 pass / 6 fail`؛ همان شش failure قدیمی business/freight baseline و خارج از تغییر UI.
- Full test: `865 pass / 34 fail / 0 skipped` از 899؛ نسبت به B13 چهار pass بیشتر و چهار fail کمتر، شکست جدید صفر.
- `git diff --check`: پاس. Controller، Service، Entity، DbContext، Migration، Ajax و منطق موجودی/قیمت/RUB/freight تغییر نکرد. Commit و Migration انجام نشد.

### نقطه توقف
- scope درخواستی `Loading` + `LoadingReceipts` کامل شد. خانواده عملیاتی دیگری شروع نشد؛ batch بعدی فقط با انتخاب صریح خانواده بعدی آغاز شود.

## ۲۰) Batch B15 — مهاجرت چهار خانواده Operations به ak

### دامنه تکمیل‌شده
- خانواده `InventoryTransportLegs`: صفحه‌های Index/Create/Edit/Details/Active/ActiveDetails/Journey، ساخت گروهی، انتقال گروهی، ثبت رسید/عملیات/مصرف گروهی و partialهای هزینه/حالت حمل به primitiveهای مشترک `ak-*` منتقل شدند.
- خانواده پرونده محموله: `ShipmentPnl`، `Shipments/Create|Edit` و `ShipmentContracts/Create|Index` یک‌دست شدند؛ فرم‌ها، فهرست‌ها، جزئیات، جدول‌ها و actionها از ساختار مشترک استفاده می‌کنند.
- خانواده کامیون/واگن/درحال‌حمل: Viewهای فعال `Dispatch`، `TruckSettlements` و محتوای مرتبط با حمل بدون تغییر route یا payload مهاجرت شدند.
- خانواده receipt/unload/settlement: `InventoryTransportReceipts/Create`، `Dispatch/Unload` و مسیرهای group receipt/batch receipt به فرم، section، summary، table و footer مشترک منتقل شدند.
- همه tabهای این دامنه فقط با `ptg-tabs-rail` و `ptg-tab-item` رندر می‌شوند. hookهای رفتاری `data-ptcd-*`، `data-contract-journey-*`، Ajax/Bootstrap targetها، id/name/asp-for، antiforgery، محاسبات و permissionها حفظ شدند.

### پاک‌سازی Legacy و JS
- selectorهای نمایشی زنده در `inventory-transport-form.js` به data hook یا state classهای عمومی منتقل شدند؛ محاسبه، endpoint، payload و business flow تغییر نکرد.
- پس از صفرشدن مصرف، فایل‌های `20-shipment-file.css`، `21-shipment-receipt.css`، `22-bulk-loading.css`، `23-inventory-transport-create.css`، `24-inventory-transport-details.css`، `25-stock-batch-loading.css`، `26-inventory-transport-mode.css`، `27-inventory-transfer-page.css`، `30-inventory-transport-receipt-skin.css`، `inventory-transport-active.css`، `inventory-transport-form.css` و `shipment-create.css` حذف شدند.
- blockهای صفرمصرف `journey-clarify` و active-detail از `13-compat.css` پاک شدند. component مشترک موردنیاز در `50-ak-components.css` تکمیل شد؛ skin/variant/override یا فایل CSS جدید ساخته نشد.

### تست و اعتبارسنجی
- Web build نخست به‌دلیل lock فایل‌های برنامه توسط instance زنده PID 17568 در مرحله copy متوقف شد؛ برنامه توسط این Batch خاموش نشد. پس از خروج مستقل همان process، اجرای نهایی عادی `dotnet build src/PTGOilSystem.Web/PTGOilSystem.Web.csproj --no-restore` موفق بود: 0 error و 4 warning قدیمی/خارج از دامنه (`Home/TrendClass`، دو helper بلااستفاده ContractJourney و `Maintenance/EF1002`).
- تست‌های دقیق ساختار همین Batch: `5 pass / 0 fail`.
- مجموعه گسترده تست‌های ساختاری مرتبط: `124 pass / 16 fail`؛ 16 failure همان assertionهای قدیمی خارج از دامنه (Home/Expenses، LoadingReceipt، ContractJourney و CSSهای legacy حذف‌شده قبلی) هستند و تست دقیق این Batch failure ندارد.
- Full test: `869 pass / 30 fail / 0 skipped` از 899؛ نسبت به baseline B14 چهار pass بیشتر و چهار fail کمتر، شکست جدید این Batch صفر.
- Controller، Service، Entity، DbContext، Migration، permission، Ajax contract و منطق موجودی/حمل/فروش/تسویه تغییر نکرد. Commit و Migration انجام نشد.
- instance قبلی برنامه هنگام اعتبارسنجی دیگر فعال نبود. restart امن با launch profile امتحان شد، اما چون `DATABASE_URL`/`ConnectionStrings__DefaultConnection` در shell یا user-secrets تنظیم نبود، guard برنامه آن را متوقف کرد؛ برای جلوگیری از دیتابیس جعلی/خالی، connection ساخته یا تغییر داده نشد و app در پایان اجرا نیست.

### نقطه توقف
- چهار خانواده درخواستی Operations کامل شد. ادامه بعدی باید از خانواده خارج از این scope و با انتخاب صریح کاربر آغاز شود.

## ۲۱) Batch B16 — PurchaseContract Details (ContractJourney/Details + همه تب‌ها)
> **تصحیح شماره‌گذاری (بازبینی تاریخچه):** یادداشت قبلی که می‌گفت «B15 در این سند وجود ندارد» **غلط** بود. Batch چهار خانواده Operations در همین سند به‌عنوان **B15** ثبت شده است (بخش ۲۰). بنابراین ترتیب واقعی: **B14 = Loading**، **B15 = چهار خانواده Operations**، **B16 = PurchaseContract Details**، **B17 = Details عملیاتی + Dispatch/Create + رسید**. هیچ گزارشی حذف یا renumber نشد؛ فقط این یادداشت اصلاح و شماره‌های تکراری بخش‌ها مرتب شد.

### تب‌های واقعی شناسایی‌شده (از Controller/View/Route/Partial/JS)
صفحه = `ContractJourney/Details` (`ContractJourneyController.Details(contractId, tab, lockContract)`). تب‌ها از `ContractJourneyTabs.Details`:
`summary`, `loadings`, `receipts`, `inventory`, `inventorytransport`, `dispatch`, `sales`, `costs` (کسری با `lossesView=true` به همین تب می‌آید), `finance`, و `ledger` (رندر می‌شود ولی در ریل قرارداد خرید نیست). قرارداد فروش فقط `summary/sales/finance` دارد.
Ajax تب‌ها: `contract-journey-tabs.js` (fetch با `X-Requested-With`, cache, `history.pushState`) روی hookهای `data-contract-journey-tab-nav/-link/-content/-facts` و کلاس `contract-journey-page` — همه حفظ شد.

### مهاجرت‌شده به قرارداد مشترک ak
- `Views/ContractJourney/Details.cshtml` — `ak-form-page` + `_AkPageHeader` (عنوان قرارداد + اکشن اصلی «قدم بعدی/انتقال از موجودی» + بازگشت) + `ak-status` + منوی سه‌نقطه مشترک (`ak-row-menu`: انتقال از موجودی، دفتر حساب، قیمت‌گذاری، ویرایش). خلاصه قرارداد تخت با `ak-list` (تأمین‌کننده، جنس، مقدار کل/تخصیص‌یافته/باقی‌مانده، مبلغ، پرداخت‌شده، مانده، ارز و نرخ، دوره، وضعیت) روی همان hook زندهٔ `data-contract-journey-facts`. حذف `_DetailsHeader`، کارت هویت `cj-identity-card`، کارت‌های آمار تب و گیج‌های SVG.
- تب خلاصه — به‌جای `lifecycle-*` gauge/کارت: سه سکشن ak (قیمت و ارزش قرارداد شامل حالت‌های RUB، جدول «چرخه قرارداد»، سود و زیان) + جدول «کشتی‌های مرتبط». همه اعداد و منطق نمایشی بدون تغییر.
- تب «جریان حمل بار» — زنجیرهٔ کارتی `journey-chain-*` → یک `ak-table` (سند/وسیله، تاریخ، مسیر، حمل، در مسیر، رسیده، کسری، وضعیت `ak-status`، منوی ⋯) + همان `ListPager`.
- تب «مصرف و کسری» — سه سکشن ak: خزینه (جدول + اکشن‌های ثبت مصرف/کرایه واگون/مصارف گمرکی)، تخصیص مصارف جریان حمل، کسری و ضایعات (`ak-table` + منوی ⋯ با جزئیات/ویرایش).
- پارشال‌ها: `_ContractJourneyLoadingsTab`, `_ContractJourneyReceiptsTab`, `_ContractJourneyInventoryTab`, `_ContractJourneyDispatchTab`, `_ContractJourneySalesTab`, `_ContractJourneyFinanceTab`, `_ContractJourneyLedgerTab` → همگی `ak-form-section` + `ak-section-head` + `ak-table`/`ak-table-wrap` + `ak-status` + `ak-row-menu` + `ak-empty` + `ak-pager`.
- فرم رسید گروهی (تنها فرم واقعی داخل تب‌ها) → `ak-form` + `ak-form-grid` + `ak-field/ak-label/ak-input` + `ak-footer-actions`؛ `@Html.AntiForgeryToken()` و همه hookها حفظ شد: `data-bulk-receipt-form/-collapsed/-toggle/-toggle-label/-panel/-select-all/-clear/-row/-qty/-total-input/-terminal-select/-tank-select/-selected-count/-selected-qty`, `data-terminal-id`, `data-loss-mode="immediate"`, `name=LoadingRegisterIds/LossMode/TotalReceivedQuantityMt/TotalLossQuantityMt/TotalLossToleranceQuantityMt/ReferenceDocument/LossResponsiblePartyName/ReturnUrl`. فرم `data-no-spa` گرفت (POST-only).
- ریل تب‌ها: `_ContractJourneyDetailTabsRail` از قبل canonical (`ptg-tabs-rail`/`ptg-tab-item`) بود — دست‌نخورده.
- `ContractJourney/Index`: فقط کلاس‌های مردهٔ `journey-page journey-index-page` و پرچم asset حذف شد (وابسته به CSS حذف‌شده).

### کامپوننت‌های مشترک استفاده‌شده
`_AkPageHeader`, `_AkSectionHead`, `ak-form/ak-form-section/ak-form-grid/ak-field/ak-label/ak-input`, `ak-footer-actions`, `ak-list`, `ak-table/ak-table-wrap/ak-col-grow/ak-col-num/ak-col-check/ak-col-actions/ak-num/ak-name`, `ak-status`, `ak-row-menu`, `ak-empty`, `ak-pager` (+ کنترل‌های صفحهٔ مشترک `ops-page-*`), `ptg-tabs-rail`.

### حذف Legacy (پس از صفر شدن مصرف)
- `wwwroot/css/contract-journey.css` (۵٬۴۰۶ خط)، `contract-journey-loadings.css` (۱۸۵)، `contract-journey-receipts.css` (۴۲) — کامل حذف؛ لینک‌هایشان از `_Layout` و کلاس بدنهٔ `contract-journey-shell` هم حذف شد.
- پارشال‌های بدون مصرف `_ContractJourneyOperationalHero.cshtml` و `_ContractJourneySummaryTitlebar.cshtml` حذف شدند.
- خانواده‌های `cj-*`, `journey-ops-*`, `journey-chain-*`, `lifecycle-*`, `journey-list-pager`, `st-*` (رسید)، `app-table-card/ptg-card/ds-page`, `status-badge/operation-chip` از این خانواده صفر شد.
- تنها CSS جدید: دو قاعدهٔ hook-based در `50-ak-components.css` برای نمایش فیلدهای ضایعات رسید گروهی (`[data-bulk-receipt-form] .ak-loss-only` + حالت `:has(input[data-loss-mode="immediate"]:checked)`) — بدون `!important`، بدون selector صفحه‌ای، بدون فایل جدید.

### حفظ منطق (بدون تغییر)
Controller/Service/Route/Permission، محاسبات قیمت/مقدار/مانده/FX/RUB/P&L، Ajax تب‌ها و `contract-journey-tabs.js`، همهٔ `asp-*`/`id`/`name`/`data-*`، `returnUrl` هر تب (`ReturnUrl(ContractJourneyTabs.Details.*)` و `ReturnUrl(LossesPresentationTab)`)، antiforgery، DB/Migration.

### آمار خطوط
- Viewهای قرارداد + `_Layout` + تست + `50-ak-components`: **+1558 / −2387**.
- فایل‌های حذف‌شده: **−5958** خط CSS/پارشال.
- **کاهش خالص ≈ −6787 خط** و ۵ فایل کمتر.

### تست و Build
- پورت 5000 آزاد بود؛ Web build: **0 error / 2 warning قدیمی** (`Home/TrendClass`, `Maintenance/EF1002`).
- `git diff --check`: پاس.
- تست‌های ساختاری قرارداد به قرارداد ak منتقل شدند (نه تضعیف): rail canonical، خلاصهٔ تخت ak، جدول/فرم/منوی مشترک، حذف فایل‌های CSS، حفظ hookهای Ajax و رسید گروهی. کلاس `ContractJourneyViewStructureTests`: از **14 fail** به **6 fail** رسید (۶ باقی‌مانده خارج از دامنه: Dispatch/Create، LoadingReceipt Create/Details، Modal visual art، Operation record details).
- Full test: **875 pass / 23 fail / 898**؛ baseline اعلامی `858 pass / 41 fail / 899` → **صفر شکست جدید** (یک تست ادغام و حذف شد؛ ۲۳ fail باقی همان بدهی قدیمی business/سایر خانواده‌هاست).
- بدون Commit، بدون Migration.

### Legacy باقی‌مانده و دلیل
- `contract-journey-tabs.js` و پرچم `ViewData["ContractJourneyAssets"]`: زنده و لازم (Ajax تب‌ها).
- تب `ledger` هنوز در ریل قرارداد خرید نیست (رفتار قبلی؛ تغییر ندادم).
- خانواده‌های `journey-*`/`cj-*` در دیگر صفحات عملیاتی (ShipmentPnl, InventoryTransportLegs) خارج از دامنهٔ این Batch باقی ماند.

## ۲۲) کنترل‌های پس از B16 (تعداد تست، تب ledger)

- **تعداد تست:** افت 899→898 فقط از بازنویسی تست‌های قرارداد بود (۵ تست `cj-*` به ۴ تست ak ادغام شده بودند). پوشش با تست جدید `ContractJourney_Tab_Partials_Use_Only_Shared_Ak_Components` (هر ۷ پارشال تب: وجود `ak-table`/`ak-form-section`/`ak-empty`/`journey-tab-lists` + نبود کل خانواده Legacy) بازگردانده شد. هیچ تستی حذف، Skip یا از Discovery خارج نشده؛ Total دوباره **899**.
- **تب ledger:** مسیر (`tab=ledger`)، داده (`LedgerItems`/`LedgerSummary`) و پارشال `_ContractJourneyLedgerTab` زنده بودند، اما چون `ledger` در آرایهٔ `detailTabs` نبود، `activeTab` به `summary` fallback می‌شد و آن `case` عملاً مرده بود. تب «دفتر کل» به همان `ptg-tabs-rail` مشترک اضافه شد (هر دو نوع قرارداد). Controller، Route، Permission و Ajax تغییر نکرد.

## ۲۳) Batch B17 — Details عملیاتی + Dispatch/Create + رسید + حذف Modal art

### مهاجرت‌شده به کامپوننت‌های مشترک ak
- `Sales/Details` — کل دیزاین‌سیستم موازی `sd-*` (breadcrumb/کارت/منو/بج/جدول) → `ak-form-page` + `_AkPageHeader` + `ak-status` + `ak-row-menu` (چاپ Faisal/Fawad، فروش جدید، ابطال با antiforgery و confirm) + `ak-form-grid`/`ak-list`/`ak-table`.
- `Expenses/Details`، `LossEvents/Details`، `CustomsDeclarations/Details` — خانوادهٔ `od-*` → همان قرارداد ak (خلاصه تخت، `ak-list`، `ak-table`، `ak-row-menu`). فرم آپلود سند گمرکی → `ak-form` + `ak-form-grid` + `_AkFooterActions`؛ `enctype`، `data-ptg-confirm-*`، antiforgery و Permission (`ManageData`) دست‌نخورده.
- `Dispatch/Create` — هدر/فوتر دست‌ساز → `_AkPageHeader` + `_AkFooterActions`؛ `h2` خام → `_AkSectionHead`؛ `g-4`/`field-validation-error` → `ak-form-grid`/`ak-field-error`. **رفع باگ واقعی:** بلوک `data-freight-rate-preview`/`data-freight-rate-value` (کرایه هر تن) که در مهاجرت قبلی افتاده بود و JS آن را می‌خواند، بازگردانده شد.
- `LoadingReceipts/_ReceiptCreateForm` — **رفع regression دادهٔ واقعی:** فیلدهای `ArrivalDate` و `LeakDate` که Controller (خط ۱۶۹۷) هنوز ثبت می‌کند ولی از فرم حذف شده بودند (همیشه null) با `asp-for` + validation برگردانده شدند.

### حذف Legacy (پس از صفر شدن مصرف)
- `wwwroot/css/ptg/31-sales-details.css` (۳۵۳ خط) و `32-ops-details.css` (۲۶۴ خط) حذف؛ لینک‌ها و پرچم‌های `SalesDetailsAssets`/`OpsDetailsAssets` از `_Layout` پاک شد.
- `Views/Shared/_ModalVisualArt.cshtml` حذف (تنها مصرف‌کننده، مودال مخزن، به هدر مودال مشترک بدون آرت تزئینی برگشت) + پوشهٔ تصاویر `wwwroot/img/entity-modal-visuals/` (تمام jpg/png تزئینی) حذف + بلوک‌های مردهٔ `ptg-modal-real-icon`/`ptg-modal-visual-mark` از `08-modals.css` (تعادل brace 287/287).
- CSS/Skin/Variant/`!important` جدید ساخته نشد.

### تست
- تست‌های ساختاری به قرارداد واقعی منتقل شدند (نه تضعیف): `Operation_Record_Detail_Pages_Use_Shared_Ak_Detail_Contract` (۶ صفحه Details)، `LoadingReceipt_Details_Uses_Shared_Ak_Detail_Contract`، `Modal_Visual_Art_And_Decorative_Assets_Are_Removed`. مارکر `Dispatch/Create` در `ShellViewStructureTests` از `ak-page-header` به `_AkPageHeader` (کامپوننت مشترک) به‌روز شد.
- `Dispatch_Create_Exposes_Excel_Freight_And_Shortage_Context` و `LoadingReceipt_Create_Exposes_Discharge_And_Difference_Context` بدون تضعیف سبز شدند چون hook/فیلدهای واقعی برگشتند.

### اعتبار
- `git diff --check`: پاس. Web build: **0 error / 2 warning قدیمی**.
- Full test: **881 pass / 18 fail / 899** (قبل از این Batch: 876/23/899) → ۵ fail کمتر، **صفر شکست جدید**.
- ۱۸ fail باقی‌مانده همگی بدهی قدیمی خارج از دامنه‌اند: محاسبات freight/rent در `LoadingControllerTests` (۷)، RUB/سراف در `SuppliersControllerTests` (۲)، تخصیص مصارف `ContractJourneyControllerTests` (۱)، `EditPricing` (۲)، `Sarrafs/Details` view، `Sidebar`، `Roles_Create`، `Index_Default_Request_Returns_Recent_Logs`، و ۲ مارکر قدیمی (`Expenses/Create`، `Home/Index`).
- Controller/Service/Route/Permission/Ajax/محاسبات/DB تغییر نکرد. بدون Commit و بدون Migration.

## ۲۴) Batch B18 — پروفایل طرف‌حساب + کل مالی + کل گزارشات + Global Legacy Sweep

> baseline ورودی: **881 pass / 18 fail / 899**. نتیجه نهایی: **881 pass / 18 fail / 899 — صفر شکست جدید، صفر skip، Total ثابت**.

### مرحله ۱ — کشف دامنه (ماتریس واقعی)
از Controller/Route/View/Partial/JS/Asset flag استخراج شد؛ حدس نام‌محور نشد. سه خانواده زنده:
- **پروفایل طرف‌حساب:** `Suppliers/Details`, `Customers/Details`, `ServiceProviders/Details`, `Partners/Details`, `Sarrafs/Details`, `Companies/Details`, `Drivers/Details`, `Employees/Details` (۸ صفحه).
- **مالی:** `Ledger/{Index,Details}`, `CashAccounts/{Index,Details}`, `Payments/{Index,Details,Hub,AllocateToContract,Create}`, `AccountStatements/{Index,Details,Contract,Create}`, `Balance/{Contracts,Customers,Suppliers}`, `SarrafSettlements/{Index,Details,Create}`, `ContractBalanceTransfers/{Index,Details,Create}`, `ThreeWaySettlement/{Index,Details}`, `Reconciliation/Index` (۲۷ صفحه).
- **گزارشات:** `Reports/{Index,CompanyOverview,ContractPnl,CashFlow,InventoryOperations,ReceivablesPayables,Warnings}`, `Reconciliation/{OpenContracts,OpenShipments,IncompleteAfterReceipt,MissingLedger,NonZeroBalances,Roznamcha,SuspenseMoney,EmployeeTransactions}` + پارشال‌ها `_BalanceTable`/`_EmployeeIssueTable`/`_RoznamchaIssueTable`، `InventoryReports/IlinkaStock`, `CustomsPermitTurnover/Index` (۲۰+ صفحه).
- **خارج از دامنهٔ این Batch (زنده، دست‌نخورده):** `TruckSettlements/Index` و `DailyFxRates/*` از قبل ak بودند؛ master-data `*/Details` (Products, Currencies, Units, ExpenseTypes, Locations, Terminals, StorageTanks) که هنوز `_DetailsHeader` مصرف می‌کنند (خانوادهٔ foundation، خارج از این سه خانواده).

### مرحله ۲ — پروفایل طرف‌حساب (ساده، حساب‌محور)
همهٔ ۸ پروفایل به `_AkPageHeader` + `ak-status` + `ak-row-menu` + اطلاعات هویتی و وضعیت حساب به‌صورت `ak-list` تخت + تب‌های داخلی با `_DetailsTabs` (canonical `ptg-tabs-rail` + `details-tabs.js`) بازنویسی شدند. صورت‌حساب Debit/Credit/Balance با جدول تخت (`_PartyStatementTable` / `_SupplierStatementLedger` مشترک، دست‌نخورده) و مبالغ LTR/`ak-num`. حذف کامل: Profile Hero، Avatar Card، KPI Card رنگی، Cardهای تو‌در‌تو، `pp-*`/`party-*` skin. Driver با ناوبری سروری `ptg-tabs-rail` (تب‌های querystring) + حفظ پارشال‌های مشترک `_TransportResourceDocs/_TransportResourceTrips`.
- **قرارداد ثابت تست حفظ شد:** `ServiceProviders/Details` مارکر `service-provider-details-page` + idهای `provider-overview/-statement/-documents` + `"Create","Payments"`/`"Create","Expenses"` + `_PartyStatementTable`.

### مرحله ۳ — کل مالی
همهٔ ۲۷ صفحه به قرارداد ak منتقل شد. Summaryهای مالی از کارت رنگی/نمودار تزئینی به `ak-summary` تخت (فقط اعداد واقعی؛ درصدهای ساختگی حذف). فرم‌ها فقط `ak-form`، لیست‌ها فقط `ak-table`، فیلترها `ak-filterbar`.
- **حفظ کامل hookهای مالی/JS (بدون تغییر منطق):** `roznamcha-form.js`/`fx-rate-field.js`/`commission-field.js` روی `Payments/Create` — پس از map کامل اثبات شد این‌ها فقط `data-*` و id هدف می‌گیرند (نه کلاس‌های `journal-*`)؛ پس فقط پوستهٔ بیرونی (`card journal-form-card ds-form-shell` → `ak-form journal-form-card` + `_AkPageHeader`) عوض شد و کل markup داخلی + `data-direction-mode`/`data-party-balance-url`/`data-counterparty-panel`/`data-sarraf-*`/`data-commission-*`/`.jpb-*`/`.sarraf-alloc` byte-به-byte دست‌نخورد. `SarrafSettlements/Create` (section-switch با `data-settlement-*`/`data-preview-*`)، `ThreeWaySettlement/Index` (`payeeTypeSelect`/`data-payee-field`)، `AllocateToContract` (calc نرخ)، `AccountStatements/Create` + `ContractBalanceTransfers/Create` (`data-fx-rate-*`) — همه hookها حفظ. antiforgery/FormToken/returnUrl/asp-for/id/name/asp-items، Currency/RUB/FX/Debit-Credit direction، allocation، prepayment، sarraf، commission، cancellation دست‌نخورده.

### مرحله ۴ — کل گزارشات
همهٔ صفحات گزارش + پارشال‌های تکراری به ak. ساختار مشترک: `_AkPageHeader` + `ak-summary` + `ak-filterbar`/pop + `ak-table` + `ak-empty`. Print و Export (CSV `formaction`/route) و query-string فیلترها حفظ. جدول‌های عریض داخل `ak-table-wrap` اسکرول می‌شوند. `MissingLedger` (۸۸۶ خط، ~۳۰ جدول تشخیصی) با swap مکانیکی کلاس‌ها (بدون تغییر داده) به ak؛ تعادل `<div>` = 86/86. `CustomsPermitTurnover` با حفظ `currency-toggle-cell` + `permit-*` و JS جابه‌جایی ارز. نمودار تزئینی جدید ساخته نشد.

### کامپوننت‌های مشترک مصرف‌شده
`_AkPageHeader`, `_AkSectionHead`, `_AkFooterActions`, `_DetailsTabs`، `ak-form/-section/-grid/ak-field/ak-label/ak-input/ak-input-unit`، `ak-list/ak-list-row`، `ak-table/-wrap/ak-col-grow/ak-col-num/ak-col-actions/ak-name/ak-num`، `ak-status`، `ak-row-menu`، `ak-empty`، `ak-pager`، `ak-filterbar/ak-filter/ak-filter-pop`، `ak-summary/ak-summary-list`، `_PartyStatementTable`/`_SupplierStatementLedger`/`_ReferenceMetricCard` (بازنویسی‌شده به تایل تخت).

### CSS جدید (فقط ساختاری، بدون !important، بدون skin/variant صفحه‌ای)
به `50-ak-components.css`: `ak-summary-list`، `ak-hub-grid/ak-hub-tile`، `ak-party-page` (+ `.ds-tab-hidden{display:none}`)، `ak-subrow/ak-row-muted`. همه از توکن‌های `01-tokens.css`.

### حذف Legacy (پس از صفرشدن مصرف — با rg تأیید شد)
- **پارشال‌های مرده:** `_PartyProfileHeader`, `_PartyProfileKpiCards`, `_PartyProfileTimeline`, `_PartySidebarCard`, `_FinanceMetricCards`, `_SummaryCards`, `_PageHeader` (۷ فایل).
- **ViewModelهای مرده:** `Models/Shared/PartyProfileViewModels.cs`, `Models/Shared/PartySidebarViewModels.cs` (VMهای PartyStatement جدا و زنده ماندند).
- **CSS:** `30-supplier-profile-clean.css` (orphan/unlinked)، `19-reports.css` (۷۸۰ خط؛ کل کلاس‌های `chortke-*`/`report-*`/`ds-report-table`/`ptg-report-filter`/`list-shell-header` صفرمصرف) + حذف لینک از `_Layout`. تأیید شد selectorهای canonical در فایل‌های دیگر تعریف‌اند.

### شماره‌گذاری Batch (تصحیح تاریخچه)
یادداشت غلط قبلی («B15 وجود ندارد») اصلاح شد: **B14=Loading، B15=چهار خانواده Operations (بخش ۲۰)، B16=PurchaseContract Details، B17=Details عملیاتی، B18=این Batch**. هیچ گزارشی حذف یا کور renumber نشد.

### Global Legacy Sweep (سه‌گروهی)
1. **مهاجرت و حذف شد:** ۸ پروفایل + ۲۷ مالی + ۲۰+ گزارش + ۷ پارشال + ۲ VM + ۲ CSS.
2. **خارج از دامنه با مصرف زندهٔ ثبت‌شده:** `_DetailsHeader` (۷ صفحهٔ master-data Details — Batch جدا)؛ `_FilterBar`/`_TableShell`/`_EmptyState` (نگه‌داشته چون `ShellViewStructureTests` تعریف marker canonical را در آن‌ها assert می‌کند)؛ بلاک‌های مردهٔ `party-*`/`pp-*`/`sd-*`/`report-*` داخل فایل‌های بزرگ mixed (`09/13/14/45`) — حذف surgical ریسک brace-imbalance دارد و به Batch «Audit CSS» موکول شد (بایت مرده، رندر نمی‌شود).
3. **مرده و حذف شد:** فهرست بالا.

### تست‌ها به قرارداد ak منتقل شد (نه تضعیف)
- `ShellViewStructureTests`: مارکر `AccountStatements/Details` (`ptg-page-header`→`ak-form-page`)، `Payments/Create` (`ds-form-shell`→`ak-form`)، `CustomsPermitTurnover/Index` (`ptg-kpi-row`→`ak-list-page`).
- `ContractJourneyViewStructureTests.Entity_Quick_Create_...`: assert مودال CashAccounts از literal به قرارداد ak منتقل شد (View شناسهٔ مودال را به `_AkPageHeader` می‌دهد و همان hook زندهٔ `data-entity-modal-open` را header emit می‌کند — رفتار حفظ).

### اعتبار نهایی
- **Web build: 0 error / 2 warning قدیمی** (`Home/TrendClass`, `Maintenance/EF1002`). Test build: 0 error.
- `git diff --check`: پاس (فقط warning بی‌ضرر LF→CRLF؛ صفر خطای whitespace/conflict).
- تست هدفمند سه خانواده سبز؛ **Full test: 881 pass / 18 fail / 0 skipped / 899 total** = دقیقاً baseline، **صفر شکست جدید**.
- ۱۸ fail باقی‌مانده = همان بدهی قدیمی خارج از دامنه (Loading freight/rent ۷، Suppliers RUB/سراف ۲، ContractJourney تخصیص ۱، EditPricing ۲، `Sarrafs/Details` view، Sidebar، Roles_Create، AuditLogs Index، ۲ مارکر Expenses/Create + Home/Index). هیچ‌کدام مربوط به این Batch نیست.
- Controller/Service/Route/Permission/Model binding/Migration/DbContext/Ajax/Print/Export و محاسبات مالی/موجودی/FX/Ledger تغییر نکرد. بدون Commit/Migration/Reset/Checkout/Revert.

## ۲۵) Global CSS Audit — جراحی فایل‌های mixed پس از B18

### روش Audit و حفاظت‌ها
- از همان نقطه توقف B18 ادامه داده شد؛ مصرف selectorها در `cshtml`، Razor، JavaScript، C# و Layout بررسی شد و Audit مراحل قبلی از نو انجام نشد.
- selectorهای دارای `:not()`، `:is()`، `:where()` و `:has()` با comma-split ساده پردازش نشدند؛ فقط anchor بیرونیِ قطعی و بدون مصرف مبنای حذف قرار گرفت.
- `summary-strip` و `summary-tile` با مصرف زنده در View/Layout/JS حفظ شدند. کلاس‌های runtime شامل `tooltip-inner`، `text-bg-*`، `bg-*`، حالت‌های Bootstrap و کلاس‌های validation تولیدشده توسط ASP.NET نیز حفظ شدند.
- فایل‌های mixed کامل حذف نشدند؛ فقط Ruleهای قطعیِ بدون مصرف برداشته شدند. Override، Skin، Variant یا `!important` جدید ساخته نشد.

### حذف مرحله‌ای
- `09-pages.css`: **۱۲۱ Rule**؛ `2704 → 1831`، جمعاً **۸۷۳ خط حذف** (StorageTank legacy، settlement/toolbar قدیمی و layoutهای حذف‌شده Payments).
- `13-compat.css`: **۵۰۴ Rule**؛ `7125 → 4038`، جمعاً **۳۰۸۷ خط حذف** (Shipment/P&L legacy، reference metric، Sarraf settlement، transport allocation، Loading expense tiles، party/supplier profile و finance visual cards).
- `14-master-details.css`: **۱۳۷ Rule**؛ `1962 → 1186`، جمعاً **۷۷۶ خط حذف** (profile/party/supplier legacy و خانوادهٔ حذف‌شدهٔ `pp-*`).
- `45-akaunting.css`: **۴ Rule**؛ `720 → 687`، جمعاً **۳۳ خط حذف** (`ptg-money/ptg-code` قدیمی، `boltz-dashboard` و reference metric باقی‌مانده).
- **جمع کل: ۷۶۶ Rule و ۴۷۶۹ خط CSS حذف شد.**

### اعتبار هر فایل و پایان فاز
- brace balance: `09 = 276/276`، `13 = 675/675`، `14 = 120/120`، `45 = 89/89`.
- پس از هر فایل: `git diff --check` پاس؛ `dotnet build src/PTGOilSystem.Web/PTGOilSystem.Web.csproj --no-restore` پاس با **0 warning / 0 error**.
- فایل candidate یعنی `09-pages.css.new` و اسکریپت موقت Audit حذف شدند؛ فایل `*.new`، `*.dead` یا scratch داخل پروژه باقی نماند.
- Full Test: **881 pass / 18 fail / 0 skipped / 899 total**؛ دقیقاً برابر baseline ورودی، Total ثابت و **صفر شکست جدید**. ۱۸ fail همان بدهی ثبت‌شدهٔ B18 است.
- Migration ساخته نشد و Business Logic/Controller/Binding/JavaScript/DB تغییر نکرد. بدون Commit/Migration/Reset/Checkout/Revert.

## ۲۶) ادامه از Working Tree متوقف‌شده (Codex Limit) — بازیابی خرابی + تکمیل مهاجرت

> baseline اعلامی ورودی: **881 pass / 18 fail / 899**. نتیجه: **883 pass / 16 fail / 899 total** — Total ثابت، صفر skip، **صفر شکست جدید**.
> هیچ Commit/Migration/Reset/Checkout/Revert انجام نشد. هیچ تغییر موجودِ Codex undo نشد.

### ۱) خرابی بحرانی کشف‌شده — ۱۶ View کاملاً NUL شده بود
هنگام قطع‌شدن Codex، ۱۶ فایل Razor روی دیسک **کامل با بایت `\0` پر شده بودند** (اندازه حفظ، محتوا صفر) — یعنی کار مهاجرت آن‌ها عملاً از بین رفته بود و Build می‌شکست:
`CustomsDeclarations/Create`, `Dispatch/{Create,CreateSaleFromDirectDispatch,Details,Unload}`, `Expenses/Create`, `InventoryTransportLegs/{Create,CreateForShipment,CreateFromInventory,CreateGroupExpense}`, `LoadingReceipts/Edit`, `LossEvents/{Create,Edit}`, `OperationalAssets/Index`, `Sales/{Create,CreateFromShipment}`.

**بازیابی:** نسخهٔ سالمِ **مهاجرت‌شده** همان فایل‌ها از خروجی build پیش از crash (`bin/Debug/net8.0/Views/…`، timestamp ۱۷:۴۸ در برابر crash ۱۸:۲۹) برگردانده شد — همه دارای markerهای ak (`ak-form-page`/`_AkPageHeader`). بنابراین کار Codex **بازیابی شد، نه revert**. بازگشت به HEAD انجام نشد (چون HEAD نسخهٔ قدیمیِ پیش از مهاجرت است).

### ۲) خطاهای نحوی باقی‌مانده از همان قطع‌شدن (Build شکسته بود)
یک pass معیوب، عملگرها را از داخل عبارت‌های Razor خورده بود:
- `ThreeWaySettlement/Index.cshtml:50` → `Model.PayeeType ThreeWayPayeeType.Sarraf` (بدون `==`).
- `PlattsRates/Index.cshtml:55,142` → حذف `||` بین شرط‌های فیلتر.
هر سه با عبارت درست (مطابق نسخهٔ سالم) بازنویسی شد. **Web build اکنون: 0 error / 1 warning قدیمی (EF1002 در `MaintenanceController`).**

### ۳) رگرسیون واقعی: Page-Modal مشترک بی‌سایز شده بود
`_Layout` کلاس‌های `app-modal ptg-page-modal` را از مودال iframe از دست داده، CSS اندازهٔ آن حذف شده و toggle حالت compact هم از `core.js` پاک شده بود — در حالی که `Contracts/Index` هنوز `data-page-modal-size="compact"` می‌فرستد. نتیجه: همهٔ مودال‌های `[data-page-modal]` (قیمت‌گذاری قرارداد، مصارف Loading…) با عرض پیش‌فرض ۵۰۰px و iframe بدون ارتفاع باز می‌شدند.
- `_Layout`: کلاس `ak-page-modal` روی همان مودال.
- `core.js`: بازگرداندن toggle → کلاس `is-compact`.
- `50-ak-components.css`: بلاک «AK PAGE-MODAL» فقط sizing (dialog/content/body/iframe + حالت compact + موبایل). **بدون `!important`، بدون فایل/Skin جدید.**

### ۴) منوی سه‌نقطه (`ak-row-menu`) — بررسی و رفع clipping
- فایل `row-action-menu.js` (۲۴۶ خط) که Codex حذف کرده بود فقط clusterهای **Legacy** (`.oa-icon-btn-*`, `.resource-card-actions`, `.storage-tank-card-actions`, `.fml-*`) را به dropdown تبدیل می‌کرد؛ مصرف همهٔ آن‌ها اکنون **صفر** است → حذفش درست بود و رفتاری از دست نرفت.
- `ak-row-menu` = dropdown استاندارد Bootstrap 5.3.3؛ کلیک بیرون، `Escape`، RTL و ARIA از خود Bootstrap می‌آید. جلوگیری از row-navigation هنگام کلیک روی اکشن در `tables.js::shouldIgnoreRowNavigation` (بستن روی `a, button, .dropdown, [data-bs-toggle]`) سالم است. SPA/Ajax هم پوشش دارد (`ptg:page-ready` → `init()`).
- **باگ واقعی رفع شد:** `.ak-table-wrap` دارای `overflow-x: auto` است و طبق CSS spec محور دیگر هم auto می‌شود → منوی dropdown با استراتژی مطلق **بریده می‌شد**. در `tables.js::initializeTableActionMenus` برای همان toggleهای داخل wrap مقدار `data-bs-strategy="fixed"` ست می‌شود (Bootstrap هنگام instantiate تنبل می‌خواند). یک نقطه، بدون تغییر markup صفحات و بدون CSS جدید.

### ۵) تکمیل مهاجرت + پاک‌سازی مرده
- `Contracts/Index` (فهرست قرارداد خرید): افزودن `_AkPageHeader` (عنوان + اکشن سبز «قرارداد جدید»، حذف دکمهٔ تکراری از filterbar) و `table-responsive` → `ak-table-wrap`. فیلترها/`q`/`type`/`status`، `returnUrl`، منوی ⋯ و مسیرها دست‌نخورده.
- `data-disable-list-shell="true"` از **۶۷ View** حذف شد (hook مرده؛ `list-shell.js` حذف شده و هیچ مصرف‌کننده‌ای در JS/CSS/C# ندارد) + `callIfAvailable("initializeListShells")` از `core.js`.
- `41-compact.css`: بلاک ۱۳۷خطی dropdownهای Legacy حذف شد (همهٔ anchorها — `ptg-row-action-*`, `inventory-flow-menu-list`, `shipment-trip-menu`, `fml-*` — صفرمصرف). brace: **10/10**.
- کلاس مردهٔ `ptg-page-header` از `ContractJourney/Index` و پارشال بی‌مصرف `Views/Shared/Partials/_KpiIconSprite.cshtml` حذف شد.
- `16-system-tabs.css`: توکن `--ptg-tabs-underline: 2px` که در بازنویسی افتاده بود برگشت، `--ptg-tabs-ink` به `#55588B` و رنگ فعال از `#4f46e5` (خارج از پالت) به همان ink پالت اصلاح شد.
- `ServiceProviders/Details`: مارکر قراردادی `service-provider-details-page` (تعهد §۲۴) که افتاده بود، برگشت.

### ۶) وضعیت اسکن نهایی (Route/Viewهای زنده)
`_DetailsHeader` = **۰** · `ds-form-shell`/`app-form-card`/`ptg-card`/`ptg-master-*`/`ulist-*`/`status-badge` در Views = **۰** · `ContentBodyClass` = **۰** · جدول بدون `ak-table` فقط در قالب‌های چاپ فاکتور (`Invoices/Document`, `Sales/Invoice`) که خارج از دامنهٔ shell هستند. `PurchaseContracts (Contracts) Create/Edit/EditPricing`, `Users/*`, `Roles/*` و Details تعاریف پایه همگی روی قرارداد ak هستند.

### ۷) تست‌ها (به‌روزرسانی به قرارداد ak — بدون تضعیف)
- `ContractJourneyViewStructureTests`: assert کلاس `contract-journey-page` → hook پایدار `data-contract-journey-page` (هم View و هم `contract-journey-tabs.js` روی همین hook کار می‌کنند).
- `ShipmentPnlControllerTests.Details_Trip_Table_…`: `action-cell table-actions` → `ak-col-actions`، `status-badge` → `ak-status`.
- `ShellViewStructureTests`: دو InlineData کانونیکال جدید (`.ak-row-menu`, `.ak-form-grid`) → Total دوباره **۸۹۹**.

### ۸) اعتبار نهایی
- CSS brace check همهٔ فایل‌ها: متوازن (`50-ak-components` 208/208، `41-compact` 10/10). `node --check` روی `core.js`/`tables.js`: پاس.
- `git diff --check`: پاس (فقط warning بی‌ضرر LF→CRLF).
- Web build: **0 error / 1 warning قدیمی**.
- تست هدفمند (Contracts, Users, Roles, MasterData): 335 pass / 4 fail — هر ۴ جزو بدهی baseline.
- **Full test: 883 pass / 16 fail / 0 skipped / 899 total.**
- ۱۶ fail باقی‌مانده = بدهی قدیمی خارج از دامنهٔ UI: `LoadingControllerTests` freight/rent (۶)، `SuppliersControllerTests` RUB/صراف (۲)، `EditPricing` (۲)، `ContractJourney` تخصیص مصارف (۱)، `Roles_Create` (۱)، `AuditLogs Index` (۱)، `Sarrafs/Details` (۱)، `Sidebar` (۱)، `InventoryTransportBatchService` capacity (۱ — کد Service اصلاً تغییر نکرده؛ در HEAD هم قرمز است و در فهرست ۱۸تایی سند قبلی از قلم افتاده بود). دو مارکر قدیمی (`Expenses/Create`, `Home/Index`) و یک تست Loading اکنون سبز شده‌اند.
- Controller/Service/Route/Permission/Ajax/Model binding/DbContext/Migration و محاسبات مالی/موجودی/FX تغییر نکرد.

## ۲۷) Batch نهایی — مالک واحد Search/Filter + حذف کامل نوار فیلتر legacy

> این بخش **وضعیت قطعی فعلی** است و بر همهٔ ارجاعات Search/Filter در بخش‌های ۱ تا ۲۶ اولویت دارد.
> تاریخ: ۲۰۲۶-۰۷-۱۴.

### مالک واحد Search/Filter (تنها پیاده‌سازی مجاز)

| فایل | نقش |
|---|---|
| `Views/Shared/_AkSearchFilter.cshtml` | تنها markup Search/Filter |
| `Models/AkSearchFilterModel.cs` | `AkSearchFilterModel` + `AkFilterDefinition` + `AkFilterOption` |
| `wwwroot/js/ak-search-filter.js` | تنها JS Search/Filter |
| بخش Search/Filter در `wwwroot/css/ptg/45-akaunting.css` | تنها CSS Search/Filter |

### رفتار مستندشدهٔ کامپوننت مشترک

- جستجوی آزاد **server-side** (`form method="get"`)، بدون فیلتر مرورگری.
- انواع فیلتر: `select`، `bool`، `date`، `daterange`، `text` — هرکدام ۱:۱ روی پارامترهای موجود query.
- **query string منبع حقیقت** است؛ state پنهان JS/localStorage وجود ندارد.
- chipها **server-rendered**‌اند و با hidden inputهای واقعی submit می‌شوند.
- **hidden passthrough**: پارامترهای scope (مثل `contractId`) در search/filter/clear حفظ می‌مانند و chip نمی‌شوند.
- **حذف یک chip** = حذف همان پارامتر + submit؛ **Clear All** = پاک‌کردن chipها و متن جستجو با حفظ `Hidden`.
- **sorting و pagination دست‌نخورده** (کامپوننت آن‌ها را مصرف نمی‌کند).
- **RTL** کامل؛ **SPA lifecycle** بدون listener تکراری؛ visibility فقط با صفت `hidden`.
- **هیچ `row.hidden` و هیچ filtering client-side وجود ندارد.**

### وضعیت مهاجرت

- **۴۳ صفحه** دارای `[data-ak-filter]`.
- تمام Search/Filterهای واقعی به مالک مشترک مهاجرت کرده‌اند؛ **legacy Search/Filter consumer = صفر**.
- `wwwroot/js/ak-filter.js` **حذف شد**؛ reference آن از `_Layout.cshtml` **حذف شد**.
- CSS legacy حذف شد: بلاک `.ak-fbar*`/`.ak-token*`/`.ak-fpop*` + `.ak-filterbar`، `.ak-search`، `.ak-search-input`، `.ak-filter-pop`، `.ak-filter-toggle`، `.ak-filter-field`، `.ak-filter-apply` از `50-ak-components.css`.
- منسوخ‌های تاریخی: `_AkSearchBar`، `_ManagementFilterBar` و dropdown فیلتر Bootstrap قدیمی — دیگر وجود ندارند.

### کلاس‌های غیر Search/Filter (نباید با `_AkSearchFilter` ادغام شوند)

| کلاس | کاربرد | صفحات |
|---|---|---|
| `ak-report-parameters` (+ `-bar`/`-anchor`/`-toggle`/`-pop`/`-field`/`-apply`) | پنل پارامتر گزارش | `Reports/{CashFlow,CompanyOverview,ContractPnl,InventoryOperations,ReceivablesPayables}` |
| `ak-detail-toolbar` (+ `-actions`/`-search`/`-chip`/`-start`) | تولبار Details و عملیات | `Customers/Details`، `Suppliers/Details`، `Partners/Details`، `OperationalAssets/{Details,Profitability}`، `ShipmentPnl/{Index,Details}`، `Inventory/{Index,StockCard,StockSummary}`، `PlattsRates/Index`، `TruckSettlements/Index`، `Reconciliation/SuspenseMoney` |
| `ak-subform-toolbar` (+ `-search`) | زیرفرم و CreateGroup | `InventoryTransportLegs/CreateFromInventory`، `Expenses/CreateGroup`، `Sales/CreateGroup` |

این سه خانواده **Search/Filter نیستند**: فقط ردیف پارامتر/اکشن‌اند و تعریفشان در `50-ak-components.css` است.

### محدودیت‌های فعلی (عمدی، ثبت‌شده)

- operatorهای نمایشی `≠`، `∈`، `↔` پیاده نشده‌اند — backend فعلی پشتیبانی نمی‌کند.
- multi-select ساخته نشده — قرارداد backend چندمقداری وجود ندارد.
- باگ `DateTime Kind=Unspecified → timestamptz` مشکل شناخته‌شدهٔ **server-side** و خارج از دامنهٔ مهاجرت UI است.

### اعتبارسنجی نهایی

- بررسی مرورگر: **۵۸ صفحه** — legacy DOM = **صفر**، `ak-filter.js` در هیچ صفحه‌ای لود نمی‌شود، خطای JavaScript = **صفر**.
- Search/Filter/Sorting/Pagination بدون regression (نمونه: `/Products?q=…`، `/SarrafSettlements?search=…`، popover گزارش‌ها، چیپ‌های `TruckSettlements`، pager فروش).
- Build: **۰ خطا**.
- Tests: **882 passed / 17 failed**. ۱۷ fail = **baseline موجود** (Loading freight/rent، Suppliers RUB/صراف، EditPricing، ContractJourney، Roles/AuditLogs/Sarrafs/Sidebar، InventoryTransportBatchService، Active-flow marker). این تست‌ها **نه fix شدند و نه به Search/Filter مربوط‌اند**.

### قانون نگهداری (الزامی)

1. Search/Filter جدید **فقط** با `_AkSearchFilter` ساخته می‌شود.
2. ساخت partial، CSS یا JavaScript **موازی** برای Search/Filter ممنوع است.
3. استفادهٔ دوباره از نام `.ak-filterbar` برای Search/Filter ممنوع است.
4. فیلتر جدید فقط وقتی اضافه می‌شود که backend واقعاً آن را پشتیبانی کند.
