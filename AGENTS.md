# AGENTS.md — PTG Oil System Working Rules

## زبان پاسخ

همیشه با من فارسی/دری صحبت کن، مگر اینکه خودم انگلیسی بخواهم.

جواب‌ها باید کوتاه، واضح، مستقیم و عملی باشد.

---

## معرفی پروژه

این پروژه یک سیستم نفت و گاز / Oil & Gas ERP است.

تکنالوژی پروژه:

* ASP.NET Core MVC / .NET 8
* C#
* EF Core
* PostgreSQL
* Razor Views
* Bootstrap RTL
* Persian/Dari UI

هدف اصلی پروژه:

سیستم باید ساده، واضح، کاربرفهم، سریع، امن و مناسب کارمندان دفتر باشد.

---

## قانون اصلی

سیستم را پیچیده‌تر نکن.

هر تغییری باید این هدف را داشته باشد:

* صفحه واضح‌تر شود
* فیلدها قابل فهم‌تر شود
* کاربر کمتر گیج شود
* منطق فعلی خراب نشود
* سرعت و سادگی حفظ شود
* تغییر کوچک، امن و قابل برگشت باشد

Do not over-engineer. Keep it simple, clear, safe, fast, and user-friendly.

---

## چیزهایی که بدون اجازه من نباید تغییر کند

بدون اجازه واضح من این بخش‌ها را تغییر نده:

* Database
* Entity
* DbContext
* Migration
* StockService
* PricingService
* Ledger logic
* P&L logic
* InventoryMovement logic
* Allocation logic
* ContractJourney business logic
* Validation های مهم
* محاسبات پول، وزن، نرخ، موجودی و نرخ ارز
* Payment posting
* CashAccount movement
* Sales/Dispatch/Receipt business flow

اگر تغییر در این بخش‌ها لازم بود، اول کوتاه توضیح بده و اجازه بگیر.

---

## روش کار برای هر درخواست

برای هر کاری که می‌دهم، این روش را انجام بده:

1. اول کد فعلی همان بخش را بررسی کن.
2. فقط همان بخش را تغییر بده.
3. تغییر را کوچک و امن نگه دار.
4. اگر ممکن بود فقط View/CSS/UI را تغییر بده.
5. منطق تجاری را تغییر نده مگر اینکه واضح خواسته باشم.
6. Migration نساز مگر اینکه واضح خواسته باشم.
7. بعد از هر تغییر کوچک build/test نگیر.
8. تغییرات را batch کن و فقط وقتی لازم بود check بگیر.
9. خلاصه نتیجه را کوتاه بگو.

---

## قانون Build/Test سریع

هدف: سرعت کار بالا برود و بعد از هر تغییر کوچک ۵ دقیقه وقت ضایع نشود.

### اگر تغییر فقط UI باشد

شامل:

* CSS
* JS نمایشی
* Razor View / `.cshtml`
* متن‌ها
* Layout
* کارت‌ها
* مودال‌ها
* آیکون‌ها
* رنگ
* spacing
* typography
* helper text
* button text
* table layout
* filter layout
* visual polish

در این حالت:

* Full solution build نگیر.
* Full test نگیر.
* EF pending model check نگیر.
* Migration نساز.
* Entity / DbContext / Service / Controller logic را تغییر نده.
* App را اجرا نگه دار و فقط صفحه را refresh کن.
* فقط در پایان فاز یک check سبک بگیر.

اگر check لازم شد، فقط این را اجرا کن:

```bash
dotnet build src/PTGOilSystem.Web/PTGOilSystem.Web.csproj --no-restore
```

---

### اگر تغییر فقط View/CSS/JS بود

این commandها ممنوع است، مگر اینکه کاربر واضح بگوید:

```bash
dotnet build ptg-oil-system.sln
dotnet test
dotnet ef migrations has-pending-model-changes
```

برای UI-only تغییرات، full build و full test بعد از هر save ممنوع است.

---

### اگر تغییر Controller یا ViewModel محدود بود

اول فقط Web project build بگیر:

```bash
dotnet build src/PTGOilSystem.Web/PTGOilSystem.Web.csproj --no-restore
```

بعد فقط تست همان بخش را اجرا کن، نه full test.

مثال:

```bash
dotnet test tests/PTGOilSystem.Web.Tests/PTGOilSystem.Web.Tests.csproj --no-build --filter "FullyQualifiedName~PaymentsControllerTests"
```

---

### اگر تغییر Business Logic / Service / Ledger / Stock / P&L / Pricing بود

در این حالت test لازم است، اما باز هم اول targeted test بگیر.

قانون:

* اول Web project build
* بعد تست همان module
* بعد تست بخش‌های مرتبط
* فقط در پایان فاز full test بگیر

Full test بعد از هر تغییر کوچک ممنوع است.

---

### اگر تغییر Entity / DbContext / Migration بود

فقط در این حالت full verification لازم است:

```bash
dotnet build ptg-oil-system.sln
dotnet test tests/PTGOilSystem.Web.Tests/PTGOilSystem.Web.Tests.csproj
dotnet ef migrations has-pending-model-changes --project src/PTGOilSystem.Web/PTGOilSystem.Web.csproj
```

این فقط زمانی اجرا شود که مدل دیتابیس واقعاً تغییر کرده باشد.

---

## قانون پایان فاز

در پایان هر فاز، نه بعد از هر تغییر کوچک، نتیجه را بررسی کن.

### برای UI-only phase معمولاً همین کافی است:

```bash
dotnet build src/PTGOilSystem.Web/PTGOilSystem.Web.csproj --no-restore
```

### اگر تست ساختاری لازم بود:

```bash
dotnet test tests/PTGOilSystem.Web.Tests/PTGOilSystem.Web.Tests.csproj --no-build --filter "FullyQualifiedName~ViewStructureTests"
```

Full test فقط وقتی اجرا شود که:

* Controller logic تغییر کرده باشد
* Service یا business logic تغییر کرده باشد
* Entity/DbContext تغییر کرده باشد
* کاربر صریحاً full test بخواهد
* قبل از commit نهایی یا release باشیم

---

## قانون جلوگیری از اتلاف وقت

هیچ‌وقت بعد از هر save یا هر اصلاح کوچک build/test تکراری نگیر.

برای UI:

1. چند فایل مرتبط را اصلاح کن.
2. App را اجرا نگه دار.
3. صفحه را refresh کن.
4. فقط در پایان فاز Web build بگیر.
5. اگر لازم بود فقط targeted test بگیر.
6. Full test را فقط برای پایان فاز بزرگ یا قبل از commit نهایی اجرا کن.

---

## سبک جواب دادن

جواب طولانی نده.

در پایان هر کار فقط این موارد را بگو:

* چه چیزی تغییر کرد؟
* کدام فایل‌ها تغییر کرد؟
* Migration ساخته شد؟ بلی/نخیر
* Business logic تغییر کرد؟ بلی/نخیر
* چه commandهایی اجرا شد؟
* Build/Test پاس شد؟ بلی/نخیر/اجرا نشد چون لازم نبود

اگر full test اجرا نشد، صادقانه بگو چرا لازم نبود.

---

## قانون UI/UX

ظاهر سیستم باید این‌طور باشد:

* ساده و اداری
* فارسی/دری RTL
* رنگ اصلی آبی/خاکستری
* بدون گرادینت سنگین
* بدون آیکون‌های بزرگ و تزئینی
* بدون طراحی کودکانه یا AI-looking
* بدون کارت‌های زیاد و شلوغ
* فورم‌ها کوتاه، واضح و قابل فهم
* متن‌های کمکی کوتاه
* لیبل‌ها واضح و انسانی
* فیلدهای کم‌استفاده داخل Advanced برود
* فیلدهای مهم همیشه قابل دید باشد
* طراحی باید واقعی، تمیز، سریع و مناسب ERP شرکتی باشد

---

## قانون فیلدها

هیچ فیلد backend را بدون اجازه حذف نکن.

برای فورم‌ها:

* فیلدهای ضروری باید واضح بماند.
* فیلدهای کم‌استفاده به Advanced برود.
* فیلدهای گیج‌کننده باید label/helper بهتر بگیرد.
* فیلدهای تکراری باید مشخص شود، اما حذف نشود مگر با اجازه من.
* اگر فیلدی برای منطق سیستم مهم است، مخفی یا حذف نشود.
* اگر فیلدی از نظر UI کم‌اهمیت است، فقط به Advanced منتقل شود.

---

## قانون منطق نفت و گاز

این منطق‌ها بسیار مهم است:

* Stock فقط از طریق StockService و InventoryMovement کنترل شود.
* Receipt به ToInventory باید InventoryMovement In بسازد.
* DirectSale نباید InventoryMovement جعلی بسازد.
* DirectDispatchFromReceipt نباید StockService را صدا بزند.
* Direct flow ها باید allocation/trace-based بماند.
* Ledger منبع رسمی balance است.
* P&L باید دقیق و read-only باشد، مگر اینکه تغییر واضح خواسته شود.
* ContractJourney فقط مرکز نمایش و navigation است، نباید خودش stock یا ledger بسازد.
* پول، وزن، نرخ و FX باید decimal/numeric بماند.
* PaymentTransaction و LedgerEntry بدون درخواست واضح تغییر نکند.
* CashAccount movement بدون درخواست واضح ساخته نشود.
* InventoryMovement جعلی برای ساده‌سازی UI ساخته نشود.

---

## قانون گزارشات

گزارشات باید ساده و قابل فهم باشد.

برای گزارشات:

* نام گزارش واضح باشد.
* هدف گزارش مشخص باشد.
* گزارش‌های تکراری یا گیج‌کننده را فقط مشخص کن.
* بدون اجازه من گزارش را حذف نکن.
* اول UI و توضیحات را واضح کن.
* محاسبات گزارش را بدون اجازه تغییر نده.
* گزارش باید برای مدیر قابل فهم باشد، نه فقط برای برنامه‌نویس.

---

## قانون نقش‌ها و دسترسی‌ها

برای Roles/Permissions:

* دسترسی‌ها باید قابل مدیریت توسط Admin باشد.
* نقش‌های ثابت کافی نیست.
* تغییرات permission باید امن باشد.
* بدون اجازه من مسیرهای حساس را باز نکن.
* قبل از تغییر permission، بگو کدام بخش‌ها affected می‌شود.
* permission را بدون تست امنیتی ساده تغییر نده.

---

## قانون Master Data و Modal ها

همه مودال‌ها و فورم‌های داده‌های پایه باید یک‌دست باشد:

* input سفید و واضح
* label ساده
* button آبی
* cancel سفید/outline
* layout یک‌دست
* بدون آیکون متفاوت و تزئینی
* بدون طراحی متفاوت در هر ماژول
* مودال‌ها باید سبک، سریع و کاربرفهم باشند
* فیلدهای غیرضروری در فرم اصلی نیاید
* form footer واضح و ثابت باشد

---

## قانون طراحی صفحات عملیاتی

برای صفحات عملیاتی مثل Loading, Receipt, Dispatch, Sales, Expenses, Payments, Customs:

* کار اصلی صفحه باید در نگاه اول معلوم باشد.
* کاربر باید بفهمد با ذخیره کردن چه چیزی ساخته می‌شود.
* Save Summary باید کوتاه و واضح باشد.
* Next Action باید مسیر بعدی را نشان دهد.
* Advanced فقط برای فیلدهای کم‌استفاده باشد.
* متن‌ها زیاد و توضیحی نباشد.
* هیچ منطق تجاری برای زیباسازی UI تغییر نکند.

---

## قانون ContractJourney

ContractJourney مرکز نمایش و navigation است.

در ContractJourney:

* Stock ساخته نشود.
* Ledger ساخته نشود.
* Payment ساخته نشود.
* InventoryMovement ساخته نشود.
* فقط اطلاعات موجود خوانده و واضح نمایش داده شود.
* تب‌ها باید ساده، واضح و سریع باشند.
* Summary باید برای مدیر قابل فهم باشد.
* آیکون و کارت زیاد باعث شلوغی نشود.

---

## قانون سرعت و Performance

در تغییرات Performance:

* اول مشکل را اندازه‌گیری یا از کد فعلی پیدا کن.
* فقط queryهای واضحاً سنگین را اصلاح کن.
* منطق محاسبات را تغییر نده.
* از projection و AsNoTracking برای read-only استفاده کن.
* includeهای سنگین را فقط با دلیل تغییر بده.
* cache فقط برای lookupهای stable و کوتاه‌مدت استفاده شود.
* بعد از Performance change فقط تست مرتبط بگیر.

---

## دستور عمومی برای شروع هر کار

اگر من فقط گفتم «این بخش را درست کن»، منظورم این است:

* اول همان بخش را بررسی کن.
* سیستم را پیچیده‌تر نکن.
* فقط تغییر کوچک و امن انجام بده.
* ترجیحاً UI-only باشد.
* migration نساز.
* business logic تغییر نده.
* بعد از هر تغییر کوچک build/test نگیر.
* در پایان فقط check لازم را اجرا کن.
* خلاصه کوتاه بده.

---

## اولویت‌های فعلی پروژه

ترتیب کار پیشنهادی:

1. Reports ساده و واضح شود.
2. LoadingReceipts/Create ساده شود.
3. Payments/Roznamcha ساده شود.
4. Sales/Create واضح شود.
5. Roles/Permissions قابل مدیریت شود.
6. Master Data Modals یک‌دست شود.
7. ContractJourney Summary حرفه‌ای‌تر و واضح‌تر شود.
8. Performance صفحات سنگین بهتر شود.

هر بار فقط روی یک بخش کار کن.

---

## جمله مهم نهایی

Do not over-engineer.

Keep it:

* simple
* clear
* safe
* fast
* user-friendly
* RTL
* business-focused
* suitable for a real Oil & Gas ERP

بعد از هر تغییر کوچک build/test نگیر. تغییرات را batch کن و فقط در پایان فاز verification مناسب انجام بده.
