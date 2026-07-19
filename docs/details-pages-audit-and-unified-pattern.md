# ممیزی صفحات جزئیات (Details) و الگوی واحد پیشنهادی

تاریخ: 2026-07-19 · وضعیت: **پیشنهاد — فقط مستند، بدون تغییر کد** · دامنه: هر ۴۲ ویوی `Views/*/Details*.cshtml`

---

## ۱. فهرست کامل صفحات و خانواده‌بندی

۴۲ صفحه Details شناسایی شد (مجموع ۹٬۶۶۶ خط Razor). بر اساس ساختار واقعی مارک‌آپ، **۸ خانوادهٔ الگویی متفاوت** وجود دارد — یعنی به‌جای یک الگو، هشت الگو داریم:

| # | خانواده | صفحات | مکانیزم تب | مکانیزم KPI | صفحه‌بندی |
|---|---------|-------|-----------|-------------|-----------|
| A | پروندهٔ محموله | ShipmentPnl (1313 خط) | `ptg-tabs-rail` + `tab-pane` بوت‌استرپ | `vc:stat-card` در هر تب | JS داخلی صفحه (۸ ردیف، locale=fa-AF) |
| B | مرکز عملیات قرارداد | ContractJourney (1274 خط) | تب سروری با query-string + AJAX | `vc:stat-card` per-tab + کارت‌های `ak-cycle` | HTML-Builder داخل خود View (`ListPager`) |
| C | کارت هویتی حمل | Dispatch (514)، InventoryTransportLegs (749) | JS اختصاصی `data-ptcd-tab` | `vc:stat-card` (۴تایی) | JS اختصاصی `data-ptcd-list` (۵ ردیف) |
| D | سند عملیاتی | Loading (317)، LoadingReceipts (346)، Sales (170)، Expenses (147)، CustomsDeclarations (239)، LossEvents (114) | بدون تب | نوار ۴ فیلدی `ak-field` (بدون stat-card) | `Take(5)` بدون pager (**داده قطع می‌شود**) |
| E | پروفایل طرف حساب | Suppliers (312)، Customers (251)، Employees (265)، Partners، Sarrafs، ServiceProviders، Drivers | پارشل `_DetailsTabs` (انکری) | بدون KPI — دو `dl` هویت/حساب | بدون صفحه‌بندی |
| F | دارایی/مخزن | OperationalAssets (754)، StorageTanks (434) | OA: دکمه‌های `data-ptg-tab-target`؛ Tank: `_DetailsTabs` | OA: `ak-list-row`های آماری؛ Tank: `dl` | Tank: pager سروری با PageWindow داخل View |
| G | سند مالی/دفتری | Payments (208)، Ledger (76)، ThreeWaySettlement (188)، SarrafSettlements، ContractBalanceTransfers، CashAccounts، AccountStatements | Payments: `_DetailsTabs`؛ بقیه بدون تب | فقط Payments در حالت تخصیص، stat-card | — |
| H | صفحات مرجع کوچک | Products، Units، Currencies، Terminals، Locations، Trucks/Vessels/Wagons، Roles، Users، Companies، DailyFxRates، ExpenseTypes، FiscalYears، AuditLogs، ExpenseRules | Trucks: تب با reload کامل query-param | بدون KPI | — |

نکته: Contracts اصلاً صفحهٔ Details ندارد — نقش آن را ContractJourney بازی می‌کند (لینک‌ها گاهی به `Contracts/Details` اشاره می‌کنند که وجود ندارد؛ مثل [Suppliers/Details.cshtml:255](src/PTGOilSystem.Web/Views/Suppliers/Details.cshtml#L255) و [Customers/Details.cshtml:236](src/PTGOilSystem.Web/Views/Customers/Details.cshtml#L236)).

---

## ۲. تحلیل هر معیار

### ۲.۱ معماری اطلاعات (IA)
- **قوی:** ShipmentPnl و ContractJourney ترتیب تب‌ها را مطابق جریان واقعی نفت/گاز چیده‌اند: بار → رسید → مصارف/گمرک → کسری → فروش → P&L → موجودی. این mental model درست است.
- **ضعیف:** در خانواده D همان اطلاعات در دو جا تکرار می‌شود (نوار ۴ فیلدی بالا + همان اقلام در `dl` «اطلاعات اصلی» — [Loading/Details.cshtml:75-118](src/PTGOilSystem.Web/Views/Loading/Details.cshtml#L75-L118)). در Payments، بخش «روابط» ۹ ردیف نمایش می‌دهد که ۷تای آن‌ها همیشه «-» است ([Payments/Details.cshtml:76-84](src/PTGOilSystem.Web/Views/Payments/Details.cshtml#L76-L84)) — نویز خالص.
- در پروفایل طرف حساب، تب‌ها «انکری» است ولی همهٔ سکشن‌ها همیشه در DOM رندر می‌شوند؛ ترتیب DOM با ترتیب تب یکسان نیست (هویت/حساب بیرون از تب‌هاست) — کاربر تب عوض می‌کند اما نیمی از صفحه ثابت می‌ماند؛ مرز تب مبهم است.

### ۲.۲ جریان واقعی کاری نفت و گاز
- بهترین صفحه از نظر عملیات: **InventoryTransportLegs** — مودال «عملیات بعدی بار» (رسید به مخزن / فروش مستقیم / ارسال با موتر / گمرک / مصرف / ادامه حمل) دقیقاً تصمیم بعدی اپراتور را مدل می‌کند ([InventoryTransportLegs/Details.cshtml:504-657](src/PTGOilSystem.Web/Views/InventoryTransportLegs/Details.cshtml#L504-L657)). این باید الگوی همهٔ صفحات عملیاتی شود.
- ضعف جدی جریان: صفحهٔ Loading رسیدها/گمرک/کسری را به ۵ ردیف اول `Take(5)` محدود می‌کند بدون هیچ لینک «مشاهده همه» ([Loading/Details.cshtml:205](src/PTGOilSystem.Web/Views/Loading/Details.cshtml#L205)) — در قراردادهای ریلی با ده‌ها واگن، اپراتور رکورد ششم به بعد را نمی‌بیند.
- **منطق تجاری در View:** ShipmentPnl دسته‌بندی مالی مصارف را با جستجوی متنی («کرایه»، «freight»…) داخل Razor انجام می‌دهد و این کار **دو بار با دو فهرست واژهٔ متفاوت** تکرار شده («گدام» و «مجوز» فقط در نسخهٔ دوم) — عدد KPI «سایر مصارف» می‌تواند با جمع گروه‌های تب نخواند ([ShipmentPnl/Details.cshtml:55-59](src/PTGOilSystem.Web/Views/ShipmentPnl/Details.cshtml#L55-L59) در برابر [332-335](src/PTGOilSystem.Web/Views/ShipmentPnl/Details.cshtml#L332-L335)).
- هیچ صفحه‌ای **تایم‌لاین زمانی واقعی** ندارد؛ با اینکه دامنه ذاتاً زنجیره‌ای است (کشتی→مخزن→واگن→موتر→فروش)، روایت زمانی فقط به‌صورت جدول‌های جدا پخش شده.

### ۲.۳ خوانایی و سرعت فهم
- خانواده C خواناترین است: هویت (وضعیت/پلیت/راننده/محصول) + ۴ KPI + سه تب مالی. یک اپراتور در ~۵ ثانیه وضعیت موتر را می‌فهمد.
- ContractJourney سنگین‌ترین: ~۴۸۰ خط محاسبه/آماده‌سازی در `@{}` قبل از اولین تگ HTML؛ ۶ کارت lifecycle با شماره‌گذاری رومی (I/II/III) که برای کاربر افغان/فارسی‌زبان بار شناختی بی‌دلیل است.
- نشت انگلیسی خام در UI فارسی: «relationهای متصل»، «Shipment»، «Movement #»، «بدون Stock»، «Line-based»، «NET PROFIT» ([Ledger/Details.cshtml:34](src/PTGOilSystem.Web/Views/Ledger/Details.cshtml#L34)، [LoadingReceipts/Details.cshtml:318](src/PTGOilSystem.Web/Views/LoadingReceipts/Details.cshtml#L318)، [OperationalAssets/Details.cshtml:155](src/PTGOilSystem.Web/Views/OperationalAssets/Details.cshtml#L155)).
- دو رژیم زبانی: نیمی از صفحات `UiText.T(fa,en)` دوزبانه، نیمی فارسی هاردکد (Loading، Suppliers، Customers، Payments، StorageTanks بخشی).

### ۲.۴ یک‌الگو بودن (Consistency)
شدیدترین مشکل. شواهد:
- **۴ سیستم KPI متفاوت:** `vc:stat-card`، نوار `ak-field` چهارتایی، `ak-list-row`های آماری (OperationalAssets)، `summary-tile` (مودال ShipmentPnl و ThreeWay).
- **۵ مکانیزم تب متفاوت** (جدول بخش ۱) با رفتار ناهمسان: بعضی state را در URL نگه می‌دارند (ContractJourney، Trucks) و بعضی با refresh از بین می‌رود (Dispatch/ITL/ShipmentPnl/OperationalAssets).
- **۴ پیاده‌سازی صفحه‌بندی** + یک حالت «قطع داده» (Take(5)). دو تای آن‌ها JS ~۵۰ خطی است که **کلمه‌به‌کلمه در دو View کپی شده** (Dispatch و ITL).
- **جای اکشن‌ها ناپایدار:** ویرایش گاهی دکمهٔ اصلی هدر (Loading، Sales)، گاهی داخل کباب‌منو (Suppliers)، گاهی نوار پایین صفحه (ITL). «وضعیت» گاهی پیل در `ak-page-actions`، گاهی فیلد داخل سکشن هویت (Dispatch)، گاهی ردیف dl (StorageTanks).
- تودرتویی کلاس‌ها: `ak-form-section ak-detail-section` داخل خودش nest شده ([ShipmentPnl/Details.cshtml:140-141](src/PTGOilSystem.Web/Views/ShipmentPnl/Details.cshtml#L140-L141)) و حتی روی `<summary>` ([374-375](src/PTGOilSystem.Web/Views/ShipmentPnl/Details.cshtml#L374-L375)) — فاصله‌گذاری دوبل و ظاهر ناهمگون.
- کد مرده/ناتمام: `class="@(isNetPositive ?"" : "")"` (شرطی که همیشه رشتهٔ خالی می‌دهد، ۴ مورد در OperationalAssets)، `<div></div>` خالی ([OperationalAssets/Details.cshtml:152](src/PTGOilSystem.Web/Views/OperationalAssets/Details.cshtml#L152))، `fillPercentValue` محاسبه می‌شود ولی هرگز رندر نمی‌شود ([StorageTanks/Details.cshtml:14](src/PTGOilSystem.Web/Views/StorageTanks/Details.cshtml#L14)) — یعنی KPI «درصد پُری مخزن» که مهم‌ترین عدد مخزن است، ساخته شده و گم شده.
- نگاشت وضعیت→رنگ با `Contains` روی متنِ ترجمه‌شده ([ShipmentPnl/Details.cshtml:783-791](src/PTGOilSystem.Web/Views/ShipmentPnl/Details.cshtml#L783-L791)) — با تغییر ترجمه، رنگ می‌شکند؛ در حالی که پارشل `_StatusBadge` موجود است و استفاده نمی‌شود.

### ۲.۵ دسکتاپ و موبایل
CSS مرجع: [11-details.css](src/PTGOilSystem.Web/wwwroot/css/ptg/11-details.css)، [52-stat-card.css](src/PTGOilSystem.Web/wwwroot/css/ptg/52-stat-card.css)، [50-ak-components.css](src/PTGOilSystem.Web/wwwroot/css/ptg/50-ak-components.css).

- **دسکتاپ:** گرید summary تا 1200px چهارستونه، سپس ۲ و ۱ ستون — منطقی. اما selectorها با زنجیرهٔ `body.boltz-shell.app-shell-authenticated.action-details .ak-detail-page ...` نوشته شده‌اند (specificity بسیار بالا) — override موضعی تقریباً ناممکن و علت اصلی کلاس‌های تکراری در Viewها همین است.
- **موبایل:**
  - `<600px` گرید KPI تک‌ستونه می‌شود؛ ۴ کارت ۱۱۰px یعنی ~۴۶۰px فضای بالای صفحه قبل از محتوا. برای اپراتور میدانی که فقط «کسری» را می‌خواهد، بد است. (پیشنهاد: ۲×۲ یا اسکرول افقی snap.)
  - جدول‌ها فقط `overflow-x:auto` دارند ([50-ak-components.css:169-172](src/PTGOilSystem.Web/wwwroot/css/ptg/50-ak-components.css#L169-L172)). جدول ۱۰ ستونی فاکتورهای مشتری یا ۹ ستونی حمل ContractJourney روی موبایل به اسکرول افقی طولانی تبدیل می‌شود؛ هیچ الگوی priority-column یا row-collapse وجود ندارد.
  - نوار اکشن هدر در `<768px` اسکرول افقی می‌شود (خوب)، اما اکشن‌های پایین‌صفحهٔ خانواده C (`ak-page-actions no-print`) sticky نیستند — کاربر باید تا انتهای صفحهٔ بلند اسکرول کند تا «ثبت تخلیه» را بزند.
  - pager دستی ShipmentPnl برای هر صفحه یک دکمه می‌سازد؛ با ۴۰ صفحه، ۴۰ دکمه در یک ردیف موبایل.
  - فرم‌های inline داخل تب‌های OperationalAssets در موبایل بالای جدول قرار می‌گیرند و تاریخچه را به پایین هُل می‌دهند.
  - دارک‌مود: stat-card پشتیبانی دارد؛ بیشتر بخش‌های 11-details فقط روشن طراحی شده‌اند.

---

## ۳. نمره‌دهی

مقیاس ۰–۱۰. معیارها: UX (کارآمدی تعامل)، IA (ساختار اطلاعات)، Flow (تطابق با جریان کاری نفت/گاز)، Read (خوانایی/سرعت فهم)، Cons (هم‌الگویی با بقیه)، Mob (موبایل).

| صفحه | UX | IA | Flow | Read | Cons | Mob | میانگین |
|------|----|----|------|------|------|-----|---------|
| InventoryTransportLegs | 8 | 8 | 9 | 8 | 6 | 6 | **7.5** |
| Dispatch | 7 | 8 | 8 | 8 | 6 | 6 | **7.2** |
| ShipmentPnl | 7 | 8 | 8 | 6 | 5 | 5 | **6.5** |
| Sales | 7 | 7 | 6 | 7 | 6 | 6 | **6.5** |
| Expenses / LossEvents / Customs | 6 | 6 | 6 | 7 | 6 | 6 | **6.2** |
| ContractJourney | 6 | 7 | 8 | 5 | 4 | 5 | **5.8** |
| Customers / Suppliers / Employees | 6 | 5 | 6 | 6 | 5 | 5 | **5.5** |
| LoadingReceipts | 5 | 5 | 6 | 5 | 5 | 5 | **5.2** |
| Loading | 5 | 4 | 4 | 6 | 5 | 5 | **4.8** |
| StorageTanks | 5 | 5 | 5 | 5 | 4 | 4 | **4.7** |
| Payments | 5 | 4 | 5 | 5 | 5 | 5 | **4.8** |
| Ledger / ThreeWay / SarrafSettlements | 5 | 5 | 5 | 4 | 5 | 5 | **4.8** |
| OperationalAssets | 4 | 4 | 5 | 4 | 3 | 4 | **4.0** |
| صفحات مرجع (H) | 6 | 6 | — | 7 | 6 | 7 | **6.4** |

**نمرهٔ کل سیستم صفحات جزئیات: 5.6 / 10.** محتوا و پوشش دامنه قوی است؛ افت نمره تقریباً یکجا از «چند-الگویی» و موبایل می‌آید.

---

## ۴. مشکلات اولویت‌بندی‌شده

### P0 — بحرانی (اثر مستقیم بر داده/تصمیم کاربر)
1. **قطع دادهٔ `Take(5)` بدون pager و بدون لینک «همه»** در Loading (رسیدها، گمرکات، کسری). رکوردهای بعدی عملاً نامرئی‌اند.
2. **دسته‌بندی مالی با string-matching داخل View و دوبار با واژه‌نامهٔ ناهمسان** (ShipmentPnl) → احتمال ناسازگاری عدد KPI با جدول همان صفحه.
3. **لینک‌های مرده به `Contracts/Details`** در Suppliers/Customers/Payments (اکشن وجود ندارد؛ مقصد درست ContractJourney است).
4. **KPI کلیدی گم‌شده:** درصد پُری مخزن محاسبه ولی رندر نمی‌شود (StorageTanks).

### P1 — بالا (شکنندگی و دوباره‌کاری)
5. پنج مکانیزم تب؛ از دست رفتن state تب با refresh در نیمی از صفحات.
6. چهار پیاده‌سازی pagination + JS کپی‌شده کلمه‌به‌کلمه در دو View؛ pager صفحه‌ساز HTML داخل Razor (ContractJourney، StorageTanks).
7. نگاشت وضعیت→رنگ با `Contains` روی متن ترجمه‌شده؛ `_StatusBadge` موجود ولی بلااستفاده.
8. چهار سیستم KPI موازی؛ خانواده D و E اصلاً stat-card ندارند.
9. جای ناپایدار اکشن‌ها (هدر/کباب/پایین صفحه) و پیل وضعیت.

### P2 — متوسط (خوانایی/موبایل)
10. جدول‌های ۷–۱۰ ستونه بدون الگوی موبایل؛ فقط اسکرول افقی.
11. KPIهای تک‌ستونه‌شده در موبایل، ~۴۶۰px فضای مرده.
12. اکشن‌های عملیاتی انتهای صفحه بدون sticky در موبایل.
13. نشت واژه‌های انگلیسی خام + دو رژیم زبانی (هاردکد فارسی در برابر `UiText.T`).
14. تکرار helperهای Razor (T/Q/M/pager/status) در هر View؛ ~۴۸۰ خط محاسبه در بالای ContractJourney.
15. تودرتویی `ak-form-section ak-detail-section` و کلاس‌های شرطی خالی/عناصر مرده (OperationalAssets).

### P3 — پایین
16. شماره‌گذاری رومی lifecycle؛ بخش «روابط» Payments با ردیف‌های همیشه-خالی؛ heading levelهای ناپیوسته؛ فقدان دارک‌مود در بخش‌هایی از details CSS؛ pager دکمه-به-ازای-هر-صفحه در ShipmentPnl.

---

## ۵. الگوی واحد پیشنهادی — «AK Detail v2»

یک قالب برای همهٔ ۴۲ صفحه. هفت ناحیهٔ ثابت با ترتیب ثابت. هر ناحیه یک پارشل/کامپوننت مشترک؛ Viewها فقط داده می‌دهند.

```
┌─────────────────────────────────────────────────────────────┐
│ 1) HEADER: عنوان + شناسه │ پیل وضعیت │ [اکشن اصلی] [⋮] [بازگشت] │
│    زیرخط: امتداد زمینه (قرارداد · جنس · تاریخ)               │
├─────────────────────────────────────────────────────────────┤
│ 2) KPI: دقیقاً ۴ عدد vc:stat-card (دسکتاپ ۴×۱ / موبایل ۲×۲) │
├──────────────────────── تب‌ریل واحد ────────────────────────┤
│ 3) FINANCIAL: خلاصه حساب (dl) + جدول «محاسبهٔ شفاف» یکتا    │
│ 4) OPERATIONAL: جدول(های) استاندارد + pager مشترک           │
│ 5) TIMELINE: رویدادهای زمانی سند (پارشل جدید)               │
│ 6) RELATED: رکوردهای مرتبط به‌صورت چیپ/لیست لینک‌دار         │
├─────────────────────────────────────────────────────────────┤
│ 7) ACTIONS: نوار «عملیات بعدی» (sticky در موبایل)           │
└─────────────────────────────────────────────────────────────┘
```

### 5.1 هدر (Header)
- پایه: همان `_AkPageHeader` با دو افزودنی: **اسلات وضعیت** (پیل `_StatusBadge` کنار عنوان — نه در نوار جدا) و **اسلات کباب** (اکشن‌های ثانویه/مخرب).
- قاعدهٔ عنوان: `«نوع سند» + شناسهٔ تجاری` — مثلاً «قرارداد PC-1404-017»، «حمل TR-0142»، «فاکتور INV-2310». هرگز فقط «#Id».
- زیرعنوان (context line): حداکثر ۳ واقعیت با «·» — قرارداد · جنس · تاریخ.
- بازگشت: همیشه از `returnUrl` با fallback به Index (الگوی فعلی خانواده C/D درست است؛ استاندارد شود).
- اکشن اصلی: **همیشه «قدم بعدی جریان»** است، نه «ویرایش». ویرایش به کباب می‌رود. (الگوی NextRecommendedAction در ContractJourney تعمیم یابد.)

### 5.2 سکشن KPI
- فقط `vc:stat-card`؛ حذف سه سیستم دیگر. **دقیقاً ۴ کارت** در سطح صفحه (تب‌های داخلی می‌توانند ۴ کارت خودشان را داشته باشند مثل الان).
- قرارداد محتوا به تفکیک خانواده:
  - عملیاتی (حمل/بارگیری/ارسال): بارگیری‌شده · تخلیه‌شده · کسری (state=warning اگر >0) · نتیجه/کرایه USD
  - قرارداد/محموله: مقدار کل · تحقق‌یافته (بارگیری/فروش) · کسری/ضایعات · سود-زیان تحقق‌یافته
  - طرف حساب: ماندهٔ حساب · گردش بدهکار · گردش بستانکار · تعداد سند باز
  - مخزن: درصد پُری · موجودی دفتری · ظرفیت خالی · قرارداد فعال
  - مرجع کوچک: بدون KPI (ناحیه حذف می‌شود).
- قاعدهٔ رنگ: فقط از `state`/`trend-direction` خود stat-card؛ رنگ‌گذاری دستی ممنوع.

### 5.3 بخش مالی (Financial)
- دو بلوک استاندارد و فقط دو بلوک:
  1. **خلاصه حساب** — `dl.ak-list` با ردیف `is-total` (الگوی فعلی ShipmentPnl «خلاصه حساب» مرجع است).
  2. **محاسبهٔ شفاف** — بلوک `ak-summary` جمع/تفریق که عدد نهایی‌اش با KPI یکی است (الگوی «محاسبهٔ سود و زیان» ShipmentPnl).
- قاعدهٔ طلایی: **هیچ دسته‌بندی/جمع مالی در View محاسبه نمی‌شود**؛ همه از ViewModel می‌آید (دسته‌بندی مصارف با enum/ExpenseType، نه جستجوی متن).
- چندارزی: نمایش استاندارد `مبلغ سند + معادل USD + نرخ` در یک ردیف؛ الگوی RUB/AFN فعلی در همین قالب.

### 5.4 بخش عملیاتی (Operational)
- جدول استاندارد: `ak-table-wrap > ak-table` + `_StatusBadge` برای وضعیت + کباب `ak-row-menu` برای اکشن ردیف.
- **یک pager واحد:** پارشل موجود `_Pagination` (سروری، state در query-string) جایگزین هر ۴ پیاده‌سازی؛ client-side فقط برای فهرست‌های ≤۵۰ ردیف با همان مارک‌آپ.
- الگوی موبایل جدول: ستون‌ها سه اولویت دارند — P1 همیشه (شناسه، مقدار، وضعیت)، P2 در ≥768px، P3 در ≥1200px؛ سطر expandable جزئیات کامل را باز می‌کند (همان الگوی breakdown-row فعلی ShipmentPnl، استانداردشده).
- ردیف‌های drill-down: الگوی `data-breakdown-row` واحد؛ حذف مودال-به-ازای-هر-سلول StorageTanks (۳ مودال × هر ردیف قرارداد).

### 5.5 تایم‌لاین (Timeline) — پارشل جدید `_DetailTimeline`
- ورودی: `IReadOnlyList<TimelineItem>` (تاریخ، عنوان، آیکن، لینک، متای اختیاری). خروجی: لیست عمودی زمانی.
- رویدادهای نمونه برای یک حمل: ایجاد → تأیید خروج → گمرک → رسید جزئی ۱..n → فروش → تسویه.
- جایگزین روایت‌های پراکنده (کارت‌های lifecycle، جدول‌های «رسیدها» به‌عنوان شبه‌تایم‌لاین). برای صفحات مرجع: فقط «ایجاد/آخرین ویرایش» از Audit.

### 5.6 رکوردهای مرتبط (Related records) — پارشل جدید `_RelatedRecords`
- چیپ/ردیف لینک‌دار به: قرارداد، محموله، سند دفتر کل، فاکتورها، پرداخت‌ها، اسناد ضمیمه — هرکدام «برچسب نوع + شناسه + یک عدد کلیدی».
- جایگزین بخش‌های «روابط» با ردیف‌های همیشه-خالی: **فقط روابط موجود رندر می‌شوند**.
- همهٔ لینک‌ها `returnUrl` صفحهٔ فعلی را حمل می‌کنند (الگوی فعلی حفظ شود).

### 5.7 اکشن‌ها (Actions)
- سه لایه، جای ثابت:
  1. **اکشن اصلی جریان** — دکمهٔ primary هدر.
  2. **نوار عملیات بعدی** — انتهای صفحه، `position:sticky; bottom:0` در `<768px`؛ محتوای آن همان مودال «عملیات بعدی بار» ITL به‌صورت دکمه‌های مستقیم (حداکثر ۴) + سرریز در کباب.
  3. **مخرب/نادر** (حذف، ابطال، لغو) — فقط در کباب هدر با confirm؛ هرگز دکمهٔ مستقیم.
- گیت مجوز مثل الگوی CustomsDeclarations (`canManageDocuments`) در همهٔ صفحات.

### 5.8 تب‌ها — یک مکانیزم
- **استاندارد: تب سروری با پارامتر `?tab=` + ریل `_DetailsTabs`** (state با refresh و share-link حفظ می‌شود). بهبود اختیاری: بارگذاری AJAX مثل ContractJourney، اما مارک‌آپ ریل یکی.
- صفحات ≤۳ سکشن: بدون تب، اسکرول ساده.
- ترتیب استاندارد تب‌ها = ترتیب جریان: خلاصه → عملیات (بارگیری/رسید/حمل) → مالی (مصارف/گمرک/کسری) → فروش → پرداخت/دفتر.

### 5.9 قواعد سراسری
- **زبان:** همه‌چیز از `UiText.T(fa,en)`؛ ممنوعیت رشتهٔ خام انگلیسی در UI (Shipment→محموله، Movement→حرکت موجودی، …).
- **اعداد:** `NumberDisplay.*` منبع واحد؛ الگوی مقدار `#,##0.####` + واحد، پول `N2` + ارز.
- **کلاس‌ها:** هر ناحیه یک `section.ak-detail-section` تخت؛ nesting ممنوع. کاهش specificity در 11-details.css (حذف زنجیرهٔ body-class) پیش‌نیاز CSS.
- **موبایل:** KPI ‌گرید ۲×۲ در `<600px`؛ نوار اکشن sticky؛ مودال‌ها تمام‌صفحه در `<576px`؛ pager فشرده «قبلی · x/y · بعدی».
- **دسترس‌پذیری:** سلسلهٔ heading یکنواخت (h1 هدر، h2 سکشن)، `aria-selected/expanded` که الان بعضی جاها هست همه‌جا اجباری.
- **چاپ:** `no-print` روی نواحی ۲ و ۷ و کباب‌ها به‌صورت پیش‌فرض قالب.

### 5.10 نگاشت خانواده‌ها به قالب

| خانواده | Header | KPI | Financial | Operational | Timeline | Related | Actions | تب |
|---------|--------|-----|-----------|-------------|----------|---------|---------|----|
| A ShipmentPnl | ✔ | ✔ (دارد) | ✔ (مرجع الگو) | ✔ + pager واحد | جدید | جدید | نوار sticky | ریل واحد |
| B ContractJourney | ✔ | ✔ (دارد) | ادغام lifecycle در «محاسبهٔ شفاف» | ✔ | جدید | کشتی‌های مرتبط → Related | ✔ | همین سروری، ریل واحد |
| C Dispatch/ITL | ✔ (هویت→زیرعنوان+Related) | ✔ (دارد) | سه تب مالی → بخش مالی | رسیدها | جدید (قوی‌ترین کاندید) | ✔ | مودال next-op → نوار | ریل واحد |
| D اسناد عملیاتی | ✔ | نوار ak-field → stat-card | ✔ | رفع Take(5) با pager | جدید | ✔ | ✔ | بدون تب |
| E طرف حساب | ✔ | KPI حساب (جدید) | خلاصه حساب موجود | جداول موجود + pager | حرکت‌های اخیر → Timeline | قراردادها → Related | ✔ | ریل واحد |
| F دارایی/مخزن | ✔ | KPI مخزن با درصد پُری | گزارش مالی OA | جداول + حذف مودال-سلولی | جدید | ✔ | فرم‌های inline → صفحهٔ Create جدا | ریل واحد |
| G مالی/دفتری | ✔ | فقط Payments | ✔ | تخصیص‌ها | Audit → Timeline | Trace → Related | ✔ | بدون تب (≤۳ سکشن) |
| H مرجع | ✔ | حذف | حذف | جدول‌های ساده | Audit مختصر | استفاده‌ها | ✔ | فقط Trucks-ها |

---

## ۶. نقشهٔ اجرا (پس از تأیید — الان هیچ کدی تغییر نمی‌کند)

1. **فاز ۰ (زیرساخت):** پارشل‌های `_DetailTimeline`، `_RelatedRecords`، pager واحد، ارتقای `_AkPageHeader` (اسلات وضعیت/کباب)، اصلاح specificity در 11-details.css، KPI موبایل ۲×۲، نوار اکشن sticky.
2. **فاز ۱ (P0):** Loading (حذف Take(5))، انتقال دسته‌بندی مصارف ShipmentPnl به ViewModel، اصلاح لینک‌های Contracts/Details، KPI پُری مخزن.
3. **فاز ۲:** خانواده C و D (کم‌ریسک‌ترین، نزدیک‌ترین به الگو) → سپس E → سپس A/B (بزرگ‌ها) → F/G → H.
4. هر صفحه پس از مهاجرت: build + اسکرین‌شات دسکتاپ/موبایل + چک‌لیست ۷ ناحیه.

---

## ۷. چک‌لیست پذیرش هر صفحهٔ Details (پس از مهاجرت)

- [ ] عنوان = نوع سند + شناسهٔ تجاری؛ پیل وضعیت کنار عنوان
- [ ] ۴ KPI با `vc:stat-card` (یا حذف کامل ناحیه در صفحات مرجع)
- [ ] هیچ محاسبهٔ مالی/دسته‌بندی در Razor
- [ ] هیچ فهرستِ بریده‌شده بدون pager یا لینک «مشاهده همه»
- [ ] یک مکانیزم تب (`?tab=`) یا بدون تب
- [ ] Timeline و Related فقط با پارشل مشترک
- [ ] اکشن مخرب فقط در کباب + confirm؛ اکشن جریان در هدر/نوار sticky
- [ ] صفر رشتهٔ انگلیسی خام؛ همهٔ متن‌ها از `UiText.T`
- [ ] جدول >۶ ستون دارای اولویت ستون موبایل
- [ ] بدون nesting `ak-detail-section`؛ بدون کلاس شرطی خالی
