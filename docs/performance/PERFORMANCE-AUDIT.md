# گزارش بررسی کارایی — PTG Oil System

**تاریخ:** ۱۴۰۵/۰۴/۲۶ (2026-07-17)
**دامنه:** کل پروژه — ۴۵۶ فایل C#، ۹۳۹ ویو Razor
**تکنولوژی:** ASP.NET Core MVC، .NET 8، EF Core، PostgreSQL، Razor
**نوع بررسی:** فقط تحلیل ایستا (Static Analysis). هیچ کد، Query، Migration، Index یا دیتابیسی تغییر نکرد.

---

## خلاصه اجرایی

سیستم از نظر پایه‌های کارایی **وضعیت خوبی دارد**: `AsNoTracking` گسترده استفاده شده (۱۶۳۱ مورد در برابر ۶۶۸ `ToListAsync`)، Lazy Loading کاملاً غیرفعال است، هیچ `sync-over-async` وجود ندارد، هیچ Static Collection بزرگی نگهداری نمی‌شود، هیچ Queryای داخل ویوها نیست، و ۳۶۶ ایندکس تعریف شده — شامل Partial Indexهای حرفه‌ای (`WHERE NOT "IsCancelled"`) در مهاجرت `20260519160000_AddReadPerformanceIndexes`.

بنابراین **مشکل اصلی سیستم کمبود ایندکس نیست.** مشکل اصلی این است:

> **الگوی غالب سیستم این است که «همهٔ ردیف‌ها را بخوان، در حافظهٔ .NET پردازش کن، سپس صفحه‌بندی/خروجی بده».**
> این الگو تا وقتی داده کم است کار می‌کند و با رشد داده به‌صورت خطی (و در بعضی نقاط درجه‌دو) خراب می‌شود.

سنگین‌ترین بخش سیستم: **کارت انبار (Stock Card) و لایه Inventory/Lineage**.

### شمارش مشکلات

| شدت | تعداد | معنی |
|---|---|---|
| **Critical** | ۵ | اکنون یا با داده بیشتر باعث خرابی/OOM/Timeout جدی می‌شود |
| **High** | ۴ | در آینده نزدیک مشکل جدی ایجاد می‌کند |
| **Medium** | ۵ | نیازمند بهینه‌سازی |
| **Low** | ۲ | بهبود اختیاری |
| **جمع** | **۱۶** | |

---

## Critical

### C-1 — کارت انبار: بارگذاری کل جدول InventoryMovements + صفحه‌بندی در حافظه

- **بخش سیستم:** موجودی / کارت انبار
- **مسیر فایل:** [StockService.cs:271-298](src/PTGOilSystem.Web/Services/StockService.cs#L271-L298) و [InventoryController.cs:320-386](src/PTGOilSystem.Web/Controllers/InventoryController.cs#L320-L386)
- **متد/Action:** `StockService.GetStockCardAsync` ← `InventoryController.StockCard`
- **الگوی مشکل‌دار:**
  ```csharp
  var movements = await BuildMovementQuery(...)
      .Include(m => m.Product).Include(m => m.Terminal)
      .Include(m => m.Contract).Include(m => m.StorageTank)
      .Include(m => m.LoadingReceipt)
          .ThenInclude(r => r!.LoadingRegister)
              .ThenInclude(l => l!.Contract)
      .OrderBy(m => m.MovementDate).ThenBy(m => m.Id)
      .ToListAsync(ct);          // ← بدون Skip/Take، بدون AsSplitQuery
  // سپس GroupBy + محاسبه مانده در حافظه
  ```
  و در کنترلر:
  ```csharp
  Rows = rows.Skip((page - 1) * IndexPageSize).Take(IndexPageSize).ToList()  // ← صفحه‌بندی در حافظه
  ```
- **علت کندی:** هیچ فیلتری اجباری نیست. اگر کاربر بدون فیلتر وارد شود، **تمام جدول InventoryMovements** با ۶ سطح Include (زنجیره سه‌طبقه `LoadingReceipt → LoadingRegister → Contract`) خوانده می‌شود، همهٔ ردیف‌ها به آبجکت تبدیل می‌شوند، مانده جاری در حافظه محاسبه می‌شود، و **بعد** صفحه‌بندی انجام می‌شود. صفحه‌بندی هیچ باری را از دیتابیس کم نمی‌کند.
- **وضعیت فعلی:** با چند هزار حرکت کار می‌کند (کند ولی قابل تحمل).
- **خطر با افزایش داده:** با ۱ میلیون حرکت: SQL بسیار بزرگ، انتقال صدها مگابایت، مصرف RAM چند صد مگابایت **در هر درخواست**، Timeout و در چند کاربر همزمان OOM. **این خطرناک‌ترین نقطهٔ سیستم است.**
- **راه‌حل پیشنهادی:**
  1. کوتاه‌مدت کم‌ریسک: اجباری‌کردن حداقل یک فیلتر (محصول یا ترمینال) + بازهٔ تاریخ پیش‌فرض (مثلاً سال مالی جاری).
  2. حذف Includeهای غیرلازم و تبدیل به `Select` پروجکشن (فقط ستون‌های مورد نیاز).
  3. بلندمدت: محاسبهٔ مانده جاری با **Window Function** در PostgreSQL:
     `SUM(signed_qty) OVER (PARTITION BY "ProductId","TerminalId" ORDER BY "MovementDate","Id")` و صفحه‌بندی در SQL.
- **آیا منطق را تغییر می‌دهد؟** گام ۱ و ۲: **خیر**. گام ۳: منطق محاسبهٔ مانده جابه‌جا می‌شود → **نیازمند تست دقیق حسابداری**.
- **ریسک اصلاح:** گام ۱-۲ پایین. گام ۳ بالا.
- **اولویت اجرا:** ۱

---

### C-2 — خروجی CSV بدون هیچ محدودیت تعداد رکورد

- **بخش سیستم:** دفتر کل (Ledger) و روزنامچه (Payments)
- **مسیر فایل:** [LedgerController.cs:87-99](src/PTGOilSystem.Web/Controllers/LedgerController.cs#L87-L99) و [LedgerController.cs:123-133](src/PTGOilSystem.Web/Controllers/LedgerController.cs#L123-L133)؛ [PaymentsController.cs:173-176](src/PTGOilSystem.Web/Controllers/PaymentsController.cs#L173-L176)
- **متد/Action:** `LedgerController.Csv` → `BuildLedgerRowsAsync`؛ `PaymentsController.Csv` → `BuildRowsAsync(filter, page: 0)`
- **الگوی مشکل‌دار:**
  ```csharp
  // Ledger — فیلتر خالی مجاز است، هیچ Take ای وجود ندارد
  var rows = await BuildLedgerRowsAsync(filter ?? new LedgerIndexFilterViewModel());

  // Payments — page:0 یعنی عمداً صفحه‌بندی خاموش
  var (rows, _, _, _) = await BuildRowsAsync(filter ?? new PaymentIndexFilterViewModel(), page: 0);
  ```
- **علت کندی:** درخواست `/Ledger/Csv` بدون پارامتر، **کل جدول LedgerEntries** را می‌خواند. صفحهٔ Index صفحه‌بندی دارد، ولی مسیر CSV آن را دور می‌زند.
- **وضعیت فعلی:** قابل استفاده در حجم فعلی.
- **خطر با افزایش داده:** دفتر کل پرحجم‌ترین جدول سیستم است. با میلیون‌ها سند، یک کلیک روی «خروجی CSV» کل جدول را به حافظهٔ وب‌سرور می‌آورد → OOM و سقوط پروسه. چند کاربر همزمان = قطعی کامل.
- **راه‌حل پیشنهادی:** الزام بازهٔ تاریخ برای خروجی + سقف سخت (مثلاً ۵۰٬۰۰۰ ردیف) با پیام خطای واضح. راه‌حل بهتر: خروجی جریانی (Streaming) — به C-3 مراجعه شود.
- **آیا منطق را تغییر می‌دهد؟** **خیر** — فقط محدودیت خروجی است، محاسبات مالی دست‌نخورده می‌ماند.
- **ریسک اصلاح:** پایین.
- **اولویت اجرا:** ۲

---

### C-3 — CsvExportSupport کل فایل را دو بار در حافظه نگه می‌دارد

- **بخش سیستم:** زیرساخت خروجی (مشترک بین ۱۵ Action خروجی)
- **مسیر فایل:** [CsvExportSupport.cs:9-23](src/PTGOilSystem.Web/Controllers/CsvExportSupport.cs#L9-L23)
- **متد/Action:** `CsvExportSupport.File`
- **الگوی مشکل‌دار:**
  ```csharp
  var builder = new StringBuilder();
  foreach (var row in rows) builder.AppendLine(...);   // کل فایل در StringBuilder
  var content = Encoding.UTF8.GetBytes(builder.ToString());  // کپی دوم
  var bytes = new byte[preamble.Length + content.Length];    // کپی سوم
  return controller.File(bytes, ...);
  ```
- **علت کندی:** کل خروجی سه بار در حافظه تکرار می‌شود: `StringBuilder` (UTF-16، دو برابر حجم) + `string` + `byte[]`. برای خروجی ۲۰۰ مگابایتی یعنی حدود **۸۰۰ مگابایت تخصیص** و رفتن به Large Object Heap.
- **وضعیت فعلی:** بی‌خطر در حجم کم.
- **خطر با افزایش داده:** فشار شدید GC، تکه‌تکه‌شدن LOH، و `OutOfMemoryException`. این ضریب‌کنندهٔ خطر C-2 است.
- **راه‌حل پیشنهادی:** تبدیل به خروجی جریانی با `IAsyncEnumerable` + نوشتن مستقیم روی `Response.Body` (یا `FileCallbackResult`) تا هیچ‌گاه کل فایل در حافظه جمع نشود.
- **آیا منطق را تغییر می‌دهد؟** **خیر** — محتوای CSV یکسان می‌ماند، فقط نحوهٔ ارسال عوض می‌شود.
- **ریسک اصلاح:** پایین تا متوسط (نیازمند تست انکودینگ UTF-8 BOM و فارسی).
- **اولویت اجرا:** ۳

---

### C-4 — InventoryLineageBackfillService: N+1 شدید روی کل تاریخچه

- **بخش سیستم:** موجودی / زنجیرهٔ Lineage
- **مسیر فایل:** [InventoryLineageBackfillService.cs:43-60](src/PTGOilSystem.Web/Services/InventoryLineageBackfillService.cs#L43-L60)، [:272-292](src/PTGOilSystem.Web/Services/InventoryLineageBackfillService.cs#L272-L292)، [:298-315](src/PTGOilSystem.Web/Services/InventoryLineageBackfillService.cs#L298-L315)، [:352](src/PTGOilSystem.Web/Services/InventoryLineageBackfillService.cs#L352)، [:412](src/PTGOilSystem.Web/Services/InventoryLineageBackfillService.cs#L412)
- **متد/Action:** `RunAsync` → `BackfillInboundLotsAsync` / `BackfillSalesAsync` / `BackfillLossesAsync` / `BackfillExpensesAsync`
- **الگوی مشکل‌دار:**
  ```csharp
  var sales = await _db.SalesTransactions.AsNoTracking()
      .Where(x => !x.IsCancelled).OrderBy(x => x.SaleDate).ToListAsync(ct);  // کل فروش‌ها

  foreach (var sale in sales)
  {
      if (await _db.SaleLotAllocations.AnyAsync(a => a.SalesTransactionId == sale.Id, ct)) continue;  // Query 1
      var outMovement = await _db.InventoryMovements.AsNoTracking()
          .Where(m => m.SalesTransactionId == sale.Id ...).FirstOrDefaultAsync(ct);                   // Query 2
      var consume = await _writer.ConsumeFifoAsync(...);                                              // Query 3+
  }
  ```
  همین الگو در `foreach (var receipt in receipts)`، `foreach (var m in freeMovements)`، `foreach (var loss in losses)` و `foreach (var expense in expenses)` تکرار شده.
- **علت کندی:** ابتدا کل جدول در حافظه، سپس **حداقل ۲ تا ۳ رفت‌وبرگشت به دیتابیس به‌ازای هر ردیف**. `SaveChangesAsync` هم داخل حلقه‌ها فراخوانی می‌شود ([:123](src/PTGOilSystem.Web/Services/InventoryLineageBackfillService.cs#L123)، [:253](src/PTGOilSystem.Web/Services/InventoryLineageBackfillService.cs#L253)، [:340](src/PTGOilSystem.Web/Services/InventoryLineageBackfillService.cs#L340)، [:401](src/PTGOilSystem.Web/Services/InventoryLineageBackfillService.cs#L401)، [:450](src/PTGOilSystem.Web/Services/InventoryLineageBackfillService.cs#L450)).
- **وضعیت فعلی:** عملیات Backfill است (نه مسیر روزمرهٔ کاربر)، پس فعلاً درد محسوس ندارد.
- **خطر با افزایش داده:** با ۱ میلیون فروش → **بیش از ۲ میلیون رفت‌وبرگشت** به دیتابیس. زمان اجرا از دقیقه به **ساعت/روز** می‌رسد. اگر داخل Transaction اجرا شود، قفل طولانی و انفجار WAL. عملاً غیرقابل اجرا.
- **راه‌حل پیشنهادی:** پردازش دسته‌ای (Batch): بارگذاری `SaleLotAllocations` و `InventoryMovements` مرتبط به‌صورت یک‌جا در `Dictionary` قبل از حلقه (تبدیل N+1 به ۳ کوئری)، پردازش در دسته‌های ۵۰۰ تا ۱۰۰۰ تایی با یک `SaveChangesAsync` در پایان هر دسته، و افزودن قابلیت ازسرگیری (Resume).
- **آیا منطق را تغییر می‌دهد؟** ترتیب FIFO **باید دقیقاً حفظ شود**. اگر ترتیب `OrderBy(SaleDate).ThenBy(Id)` حفظ شود، نتیجه یکسان است — ولی **نیازمند تست کامل حسابداری و مقایسهٔ خروجی قبل/بعد**.
- **ریسک اصلاح:** **بالا** — مستقیماً روی زنجیرهٔ FIFO و بهای تمام‌شده اثر دارد.
- **اولویت اجرا:** ۷ (بعد از اصلاحات کم‌ریسک)

---

### C-5 — گزارش‌های مدیریتی بدون بازهٔ تاریخ پیش‌فرض

- **بخش سیستم:** گزارش‌ها (Reports)
- **مسیر فایل:** [ManagementReportViewModels.cs:157-169](src/PTGOilSystem.Web/Models/Reports/ManagementReportViewModels.cs#L157-L169)، [ReportsController.cs:672-705](src/PTGOilSystem.Web/Controllers/ReportsController.cs#L672-L705)
- **متد/Action:** `CompanyOverview`، `CashFlow`، `ReceivablesPayables`، `InventoryOperations`، `ContractPnl`
- **الگوی مشکل‌دار:**
  ```csharp
  public DateTime? FromDate { get; set; }   // ← پیش‌فرض null
  public DateTime? ToDate   { get; set; }   // ← پیش‌فرض null

  filter ??= new ManagementReportFilterViewModel();
  if (filter.FromDate.HasValue) query = query.Where(...);   // اگر null باشد فیلتری اعمال نمی‌شود
  ```
- **علت کندی:** باز کردن هر گزارش بدون پارامتر یعنی **اسکن کل تاریخچهٔ چند ساله**. `ContractPnl` علاوه بر این، ۱۰+ کوئری پشت‌سرهم روی همان مجموعه اجرا می‌کند ([:699](src/PTGOilSystem.Web/Controllers/ReportsController.cs#L699)، [:804](src/PTGOilSystem.Web/Controllers/ReportsController.cs#L804)، [:850](src/PTGOilSystem.Web/Controllers/ReportsController.cs#L850)، [:914](src/PTGOilSystem.Web/Controllers/ReportsController.cs#L914)، [:943](src/PTGOilSystem.Web/Controllers/ReportsController.cs#L943)، [:967](src/PTGOilSystem.Web/Controllers/ReportsController.cs#L967)، [:1078](src/PTGOilSystem.Web/Controllers/ReportsController.cs#L1078)).
- **وضعیت فعلی:** با ۱-۲ سال داده قابل تحمل.
- **خطر با افزایش داده:** با ۵ سال داده و میلیون‌ها رکورد، هر بازکردن گزارش = Timeout. Partial Indexهای موجود (`IX_SalesTransactions_Active_SaleDate`) **بدون فیلتر تاریخ عملاً بی‌فایده‌اند** — بهینه‌ساز به Seq Scan می‌رود.
- **راه‌حل پیشنهادی:** مقداردهی پیش‌فرض `FromDate` به ابتدای سال مالی جاری و `ToDate` به امروز، وقتی کاربر پارامتری نداده. این کار ایندکس‌های Partial موجود را **فعال** می‌کند.
- **آیا منطق را تغییر می‌دهد؟** **بله — رفتار پیش‌فرض گزارش عوض می‌شود** (به‌جای «همه» → «سال مالی جاری»). از نظر فنی محاسبات یکسان است ولی **عدد نمایش‌داده‌شده تغییر می‌کند** → نیازمند تأیید کاربر تجاری.
- **ریسک اصلاح:** متوسط (فنی پایین، ولی از نظر انتظار کاربر مهم).
- **اولویت اجرا:** ۴

---

## High

### H-1 — داشبورد: ~۵۶ رفت‌وبرگشت متوالی + Anti-Join روی کل جداول

- **بخش سیستم:** داشبورد اصلی
- **مسیر فایل:** [DashboardService.cs:29-45](src/PTGOilSystem.Web/Services/DashboardService.cs#L29-L45) و [:88-105](src/PTGOilSystem.Web/Services/DashboardService.cs#L88-L105)
- **متد/Action:** `BuildDashboardAsync` → ۸ متد `Populate*Async`
- **الگوی مشکل‌دار:**
  ```csharp
  await PopulateCountsAndTotalsAsync(...);   // هر کدام چندین await _db.
  await PopulateBalanceSummariesAsync(...);
  await PopulateInventoryAsync(...);
  // ... مجموعاً ۵۶ فراخوانی await _db. — همه متوالی (Sequential)

  vm.LoadingsWithoutReceiptCount = await _db.LoadingRegisters.AsNoTracking()
      .CountAsync(l => !_db.LoadingReceipts.Any(r => r.LoadingRegisterId == l.Id), ct);
  vm.SalesWithoutPaymentCount = await _db.SalesTransactions.AsNoTracking()
      .CountAsync(s => !s.IsCancelled && !_db.PaymentTransactions.Any(p => p.SalesTransactionId == s.Id), ct);
  ```
- **علت کندی:** دو مسئله هم‌زمان: (الف) ۵۶ رفت‌وبرگشت **متوالی** — تأخیر شبکه ضرب در ۵۶؛ (ب) کوئری‌های `Count` با `NOT EXISTS` همبسته که **کل جدول را بدون هیچ فیلتر تاریخ** پیمایش می‌کنند (Anti-Join کامل).
- **وضعیت فعلی:** داشبورد قابل استفاده اما کندترین صفحهٔ عمومی.
- **خطر با افزایش داده:** `LoadingsWithoutReceiptCount` و `SalesWithoutPaymentCount` با میلیون‌ها ردیف چند ثانیه هرکدام طول می‌کشند. داشبورد صفحهٔ اول همه است → هر ورود کاربر ۵۶ کوئری. با ۲۰ کاربر همزمان، Connection Pool تمام می‌شود.
- **راه‌حل پیشنهادی:**
  1. کم‌ریسک: کش کردن کل ViewModel داشبورد در `IMemoryCache` با انقضای ۳۰-۶۰ ثانیه (زیرساخت `AddMemoryCache` در [Program.cs:164](src/PTGOilSystem.Web/Program.cs#L164) از قبل موجود است).
  2. محدودکردن شمارنده‌های «بدون رسید/بدون پرداخت» به بازهٔ اخیر (مثلاً ۹۰ روز).
  3. ادغام شمارنده‌های مستقل در یک کوئری واحد با Subqueryهای اسکالر.
- **آیا منطق را تغییر می‌دهد؟** گام ۱: خیر (فقط تازگی داده تا ۶۰ ثانیه تأخیر). گام ۲: **بله** — معنی شمارنده عوض می‌شود.
- **ریسک اصلاح:** گام ۱ پایین، گام ۲ متوسط.
- **اولویت اجرا:** ۵

---

### H-2 — ContractJourney: ~۷۰ کوئری در یک صفحه

- **بخش سیستم:** قرارداد / سفر قرارداد
- **مسیر فایل:** [ContractJourneyController.cs](src/PTGOilSystem.Web/Controllers/ContractJourneyController.cs) (۴۵۵۸ خط؛ ۷۰ فراخوانی `ToListAsync`/`FirstOrDefaultAsync`/`CountAsync`/`SumAsync`/`AnyAsync`، ۸۵ `Include`)
- **متد/Action:** `Details` و متدهای کمکی آن
- **الگوی مشکل‌دار:** یک صفحهٔ جزئیات که ده‌ها کوئری مستقل و متوالی اجرا می‌کند و همهٔ موجودیت‌های مرتبط با قرارداد را بارگذاری می‌کند. صفحه‌بندی ندارد.
- **علت کندی:** تعداد رفت‌وبرگشت، نه سنگینی تک‌تک کوئری‌ها. مقیاس با تعداد محموله/حمل/فروش زیرمجموعهٔ قرارداد رشد می‌کند.
- **وضعیت فعلی:** برای قراردادهای کوچک سریع؛ برای قراردادهای بزرگ کند.
- **خطر با افزایش داده:** قراردادی با هزاران بارگیری و فروش → صفحه چند ده ثانیه یا Timeout.
- **راه‌حل پیشنهادی:** تقسیم صفحه به بخش‌های Lazy (بارگذاری با AJAX هنگام باز کردن هر تب)، و صفحه‌بندی لیست‌های زیرمجموعه.
- **آیا منطق را تغییر می‌دهد؟** خیر (فقط زمان‌بندی بارگذاری) — ولی تغییر UI است.
- **ریسک اصلاح:** متوسط (فایل بسیار بزرگ، سطح تماس زیاد).
- **اولویت اجرا:** ۸

---

### H-3 — صفحات فهرست بدون صفحه‌بندی

- **بخش سیستم:** متعدد
- **مسیر فایل:** ۲۱ کنترلر از ۵۷ کنترلر دارای `Index`، هیچ `Skip(` ندارند:
  `AccountingReadiness`، `ClosingChecklist`، `ContractAmendments`، `ContractBalanceTransfers`، `ContractJourney`، `CustomsPermitTurnover`، `ExpenseRules`، `FinalClose`، `FiscalYears`، `Home`، `PlattsRates`، `Reconciliation`، `ReopenFiscalYear`، `Reports`، `Roles`، `Sarrafs`، `SarrafSettlements`، `ShipmentContracts`، `ThreeWaySettlement`، `TrialClose`، `TruckSettlements`
- **متد/Action:** `Index` هر کدام
- **علت کندی:** کل جدول در هر بار باز کردن صفحه.
- **وضعیت فعلی:** بخشی از این‌ها ذاتاً کم‌ردیف‌اند (`Roles`، `FiscalYears`، `PlattsRates`، `ExpenseRules`) و **مشکلی ندارند**. اما این‌ها با داده رشد می‌کنند و **باید اصلاح شوند**: `SarrafSettlements`، `ThreeWaySettlement`، `TruckSettlements`، `ContractBalanceTransfers`، `ContractAmendments`، `Reconciliation`، `CustomsPermitTurnover`.
- **خطر با افزایش داده:** تسویه‌های صراف و حواله‌های سه‌طرفه با تراکنش رشد می‌کنند → صفحه‌های چند ده هزار ردیفی و رندر سنگین Razor.
- **راه‌حل پیشنهادی:** افزودن صفحه‌بندی سمت SQL با همان الگوی موجود در `InventoryController.Index` ([:133-134](src/PTGOilSystem.Web/Controllers/InventoryController.cs#L133-L134)) — الگو در پروژه از قبل هست و ثابت‌شده.
- **آیا منطق را تغییر می‌دهد؟** **خیر** — فقط نمایش.
- **ریسک اصلاح:** پایین.
- **اولویت اجرا:** ۶

---

### H-4 — نبود CommandTimeout و Rate Limiting

- **بخش سیستم:** زیرساخت
- **مسیر فایل:** [Program.cs:49-56](src/PTGOilSystem.Web/Program.cs#L49-L56)
- **الگوی مشکل‌دار:**
  ```csharp
  builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
  {
      options.UseNpgsql(BuildPostgresConnectionString(rawConnectionString!));
      // ← نه CommandTimeout، نه EnableRetryOnFailure، نه MaxPoolSize صریح
  });
  ```
  و هیچ `AddRateLimiter` در کل `Program.cs` وجود ندارد.
- **علت کندی:** یک کوئری سنگین (مثل C-1 یا C-2) می‌تواند تا Timeout پیش‌فرض ۳۰ ثانیهٔ Npgsql یک اتصال را اشغال کند. هیچ محدودیتی برای درخواست مکرر روی Endpointهای سنگین وجود ندارد.
- **وضعیت فعلی:** بی‌خطر در بار کم.
- **خطر با افزایش داده:** کاربری که چند بار روی «خروجی CSV» کلیک می‌کند می‌تواند به‌تنهایی Connection Pool را تمام کند → کل سیستم برای همه قطع می‌شود (بند ۲۳ و ۲۴ درخواست).
- **راه‌حل پیشنهادی:** تعیین `CommandTimeout` صریح، فعال‌کردن `EnableRetryOnFailure`، و افزودن Rate Limiter روی مسیرهای `/*/Csv` و گزارش‌ها.
- **آیا منطق را تغییر می‌دهد؟** **خیر**.
- **ریسک اصلاح:** پایین.
- **اولویت اجرا:** ۹

---

## Medium

### M-1 — جست‌وجوی `Contains` روی جداول بزرگ → Seq Scan

- **مسیر فایل:** [BalanceController.cs:167-198](src/PTGOilSystem.Web/Controllers/BalanceController.cs#L167-L198)، [ContractsController.cs:176](src/PTGOilSystem.Web/Controllers/ContractsController.cs#L176)، [CashAccountsController.cs:83-87](src/PTGOilSystem.Web/Controllers/CashAccountsController.cs#L83-L87)، [AccountStatementsController.cs:823](src/PTGOilSystem.Web/Controllers/AccountStatementsController.cs#L823)، [CustomsDeclarationsController.cs:838-846](src/PTGOilSystem.Web/Controllers/CustomsDeclarationsController.cs#L838-L846)
- **الگو:** `.Where(c => c.ContractNumber.Contains(search) || c.Customer.Name.Contains(search) || ...)`
- **علت کندی:** `Contains` به `LIKE '%term%'` ترجمه می‌شود — **هیچ ایندکس B-tree نمی‌تواند استفاده شود**. چند `OR` روی جداول Join شده وضع را بدتر می‌کند.
- **خطر با افزایش داده:** هر جست‌وجو = Seq Scan کامل روی چند جدول.
- **راه‌حل پیشنهادی:** برای فیلدهای کد (`ContractNumber`، `Code`) از `StartsWith` استفاده شود (از ایندکس با `text_pattern_ops` بهره می‌برد). برای جست‌وجوی متنی، ایندکس GIN با `pg_trgm`:
  `CREATE INDEX ... USING gin ("Name" gin_trgm_ops);`
- **آیا منطق را تغییر می‌دهد؟** `StartsWith` **بله** (نتایج جست‌وجو تغییر می‌کند). ایندکس GIN **خیر**.
- **ریسک اصلاح:** پایین (GIN) تا متوسط (StartsWith).
- **اولویت اجرا:** ۱۰

---

### M-2 — لغو گروهی اعلامیه گمرکی: Cartesian + Tracking

- **مسیر فایل:** [CustomsDeclarationsController.cs:848-851](src/PTGOilSystem.Web/Controllers/CustomsDeclarationsController.cs#L848-L851)
- **الگو:**
  ```csharp
  var toDelete = await query
      .Include(c => c.Items)        // مجموعه ۱
      .Include(c => c.Documents)    // مجموعه ۲  ← ضرب دکارتی
      .ToListAsync();               // بدون AsNoTracking (لازم است چون حذف می‌کند)، بدون AsSplitQuery
  ```
- **علت کندی:** دو `Include` مجموعه‌ای در یک کوئری = `Items × Documents` ردیف تکراری. برخلاف `Details` در [خط ۲۰۵](src/PTGOilSystem.Web/Controllers/CustomsDeclarationsController.cs#L205) که `AsSplitQuery()` دارد، این مسیر ندارد.
- **خطر با افزایش داده:** فیلتر گسترده → انفجار حافظه و SQL بسیار بزرگ.
- **راه‌حل پیشنهادی:** افزودن `.AsSplitQuery()` و سقف تعداد برای لغو گروهی.
- **آیا منطق را تغییر می‌دهد؟** `AsSplitQuery` **خیر** (فقط باید داخل Transaction باشد که هست). سقف تعداد **بله**.
- **ریسک اصلاح:** پایین.
- **اولویت اجرا:** ۱۱

---

### M-3 — `SaveChangesAsync` داخل حلقه

- **مسیر فایل:** [ExpensesController.cs:3044-3049](src/PTGOilSystem.Web/Controllers/ExpensesController.cs#L3044-L3049)، [LoadingController.cs:2935-2942](src/PTGOilSystem.Web/Controllers/LoadingController.cs#L2935-L2942)، [SalesController.cs:1563-1569](src/PTGOilSystem.Web/Controllers/SalesController.cs#L1563-L1569)، [SalesController.Group.cs:878-885](src/PTGOilSystem.Web/Controllers/SalesController.Group.cs#L878-L885)، [InventoryLineageWriter.cs:509-517](src/PTGOilSystem.Web/Services/InventoryLineageWriter.cs#L509-L517)
- **علت کندی:** هر `SaveChangesAsync` یک رفت‌وبرگشت + `fsync` است. داخل حلقه = N رفت‌وبرگشت و اگر Transaction باز باشد، قفل طولانی.
- **خطر با افزایش داده:** عملیات دسته‌ای روی هزاران ردیف کند می‌شود و ریسک Deadlock/Timeout بالا می‌رود (بند ۱۷ و ۲۴).
- **راه‌حل پیشنهادی:** انتقال `SaveChangesAsync` به بیرون حلقه (یک بار در پایان) — **فقط جایی که ترتیب عملیات به Id تولیدشده وابسته نیست**.
- **آیا منطق را تغییر می‌دهد؟** **بالقوه بله** — بعضی از این حلقه‌ها ممکن است عمداً هر بار ذخیره کنند تا Id بگیرند یا ترتیب رویداد حسابداری حفظ شود. **باید تک‌تک بررسی شود، نه یک‌جا.**
- **ریسک اصلاح:** **متوسط تا بالا** — نیازمند تست حسابداری.
- **اولویت اجرا:** ۱۲

---

### M-4 — تراکنش‌های طولانی

- **مسیر فایل:** ۴۹ مورد `BeginTransactionAsync` در کنترلرها و سرویس‌ها — از جمله `LoadingController`، `PaymentsController`، `InventoryTransportLegsController`، `ExpensesController`، `DispatchController`، `LossEventsController`
- **علت کندی:** بعضی از این تراکنش‌ها منطق سنگین و حلقه در بر می‌گیرند (به M-3 مراجعه شود). هرچه تراکنش بازتر بماند، قفل بیشتر.
- **خطر با افزایش داده:** با کاربران همزمان، افزایش Lock Contention و `deadlock detected` (بند ۲۴).
- **راه‌حل پیشنهادی:** کوتاه‌کردن محدودهٔ تراکنش — انجام خواندن‌ها و محاسبات **قبل** از `BeginTransactionAsync` و نگه‌داشتن تراکنش فقط برای نوشتن.
- **آیا منطق را تغییر می‌دهد؟** **بالقوه بله** — تغییر مرز تراکنش یعنی تغییر تضمین اتمیک بودن.
- **ریسک اصلاح:** **بالا**.
- **اولویت اجرا:** ۱۳

---

### M-5 — کوئری‌های شمارش تکراری در صفحهٔ هشدارها

- **مسیر فایل:** [ReportsController.cs:553-570](src/PTGOilSystem.Web/Controllers/ReportsController.cs#L553-L570) و [:627-643](src/PTGOilSystem.Web/Controllers/ReportsController.cs#L627-L643)
- **الگو:** چندین `CountAsync` با `NOT EXISTS` همبسته روی `LedgerEntries` که هرکدام جدول را جداگانه پیمایش می‌کنند:
  ```csharp
  .CountAsync(s => !s.IsCancelled && !_db.LedgerEntries.Any(l => l.SourceType == "Sale" && l.SourceId == s.Id));
  .CountAsync(e => !e.IsCancelled && !_db.LedgerEntries.Any(l => l.SourceType == "Expense" && l.SourceId == e.Id));
  ```
- **علت کندی:** هر شمارنده یک Anti-Join جدا. ایندکس `IX_LedgerEntries_SourceType_SourceId` موجود است و کمک می‌کند، ولی سمت بیرونی همچنان کل جدول است.
- **راه‌حل پیشنهادی:** ادغام در یک کوئری با `FILTER (WHERE ...)` یا محدودکردن به بازهٔ تاریخ.
- **آیا منطق را تغییر می‌دهد؟** ادغام: خیر. محدودکردن تاریخ: بله.
- **ریسک اصلاح:** پایین.
- **اولویت اجرا:** ۱۴

---

## Low

### L-1 — `ToLower()` داخل کوئری

- **مسیر فایل:** [OperationalAssetsController.cs:118](src/PTGOilSystem.Web/Controllers/OperationalAssetsController.cs#L118)، [AccountingPostingService.cs:289](src/PTGOilSystem.Web/Services/Accounting/AccountingPostingService.cs#L289)
- **الگو:** `.Where(e => e.ExpenseType!.Category.ToLower() == freightCategory)` و `.Where(x => x.IsActive && requestedCodes.Contains(x.Code.ToUpper()))`
- **علت کندی:** اعمال تابع روی **ستون** مانع استفاده از ایندکس می‌شود (Non-SARGable).
- **خطر:** پایین — `ExpenseTypes` و `AccountingAccounts` جداول کوچکی هستند و بزرگ نمی‌شوند.
- **راه‌حل پیشنهادی:** مقایسه با `ILIKE` یا نرمال‌سازی داده هنگام ذخیره، یا ایندکس بیانی (Expression Index).
- **آیا منطق را تغییر می‌دهد؟** خیر.
- **ریسک اصلاح:** پایین.
- **اولویت اجرا:** ۱۵

---

### L-2 — ایندکس‌های Partial خارج از مدل EF

- **مسیر فایل:** [20260519160000_AddReadPerformanceIndexes.cs](src/PTGOilSystem.Web/Migrations/20260519160000_AddReadPerformanceIndexes.cs)، [20260519173000_EnsureReadPerformanceIndexes.cs](src/PTGOilSystem.Web/Migrations/20260519173000_EnsureReadPerformanceIndexes.cs)
- **الگو:** ایندکس‌های مهم با SQL خام ساخته شده‌اند (`CREATE INDEX IF NOT EXISTS ... WHERE NOT "IsCancelled"`) و در `DbContext` با `HasIndex` تعریف نشده‌اند.
- **علت نگرانی:** چون در ModelSnapshot نیستند، EF از وجودشان بی‌خبر است. **خطر حذف شدن ندارند** (EF فقط چیزی را که می‌شناسد مدیریت می‌کند)، ولی خطر این است که کسی ندانسته ایندکس تکراری بسازد یا هنگام بازسازی دیتابیس فراموش شوند.
- **راه‌حل پیشنهادی:** ثبت در مدل با `HasIndex(...).HasFilter("NOT \"IsCancelled\"")` تا در Snapshot دیده شوند — یا صرفاً مستندسازی. **توصیه: فعلاً دست نزنید؛ فقط مستند شود.**
- **آیا منطق را تغییر می‌دهد؟** خیر.
- **ریسک اصلاح:** پایین (ولی سود کمی دارد).
- **اولویت اجرا:** ۱۶

---

## نقاط قوت تأییدشده (نیازی به اقدام ندارند)

بررسی موارد زیر از چک‌لیست درخواست، **مشکلی نشان نداد**:

| بند | یافته |
|---|---|
| ۲ — `ToList` قبل از فیلتر | هیچ موردی از `.ToList().Where(...)` یا `AsEnumerable()` یافت نشد |
| ۶ — نبود `AsNoTracking` | ۱۶۳۱ استفاده در برابر ۶۶۸ `ToListAsync` — پوشش بسیار خوب |
| ۱۹ — Sync/Thread blocking | هیچ `.Result`، `.Wait()` یا `GetAwaiter().GetResult()` یافت نشد |
| ۲۰ — Static Collection | هیچ کش Static یا مجموعهٔ ماندگار در حافظه یافت نشد |
| ۲۱ — Query در ViewModel/View | هیچ دسترسی `_db` در ویوها یا ViewComponentها یافت نشد |
| ۲۲ — Lazy Loading | `UseLazyLoadingProxies` استفاده نشده؛ هیچ `virtual ICollection` — کوئری مخفی وجود ندارد |
| ۱۵ — تبدیل تاریخ | `filter.ToDate.Value.Date` روی **پارامتر** اعمال می‌شود نه ستون → SARGable و بی‌ضرر |
| ۱۳ — کمبود ایندکس | ۳۶۶ ایندکس شامل Partial Indexهای هدفمند — ایندکس‌گذاری وضعیت خوبی دارد |

---

## جدول خلاصه

### ۱۰ کوئری سنگین‌تر سیستم

| # | کوئری | مکان | چرا سنگین |
|---|---|---|---|
| ۱ | کارت انبار — کل InventoryMovements + ۶ Include | `StockService.cs:283` | بدون فیلتر، بدون صفحه‌بندی SQL |
| ۲ | خروجی CSV دفتر کل | `LedgerController.cs:130` | کل جدول LedgerEntries |
| ۳ | خروجی CSV روزنامچه | `PaymentsController.cs:175` | `page: 0` |
| ۴ | Backfill فروش‌ها (N+1) | `InventoryLineageBackfillService.cs:298` | ۲ کوئری × هر فروش |
| ۵ | `LoadingsWithoutReceiptCount` | `DashboardService.cs:88` | Anti-Join کل جدول |
| ۶ | `SalesWithoutPaymentCount` | `DashboardService.cs:94` | Anti-Join کل جدول |
| ۷ | `BuildContractPnlAsync` | `ReportsController.cs:679` | ۱۰+ کوئری، بدون بازهٔ تاریخ |
| ۸ | `ContractJourney.Details` | `ContractJourneyController.cs` | ~۷۰ کوئری در یک صفحه |
| ۹ | جست‌وجوی Balance با `Contains` | `BalanceController.cs:167` | Seq Scan چندجدولی |
| ۱۰ | لغو گروهی اعلامیه گمرکی | `CustomsDeclarationsController.cs:848` | ضرب دکارتی |

### ۱۰ صفحه با بیشترین احتمال کندی

| # | صفحه | علت اصلی |
|---|---|---|
| ۱ | `/Inventory/StockCard` | C-1 — کل جدول در حافظه |
| ۲ | `/Ledger/Csv` | C-2 + C-3 — خروجی نامحدود |
| ۳ | `/Payments/Csv` | C-2 + C-3 |
| ۴ | `/` (داشبورد) | H-1 — ۵۶ کوئری |
| ۵ | `/Reports/ContractPnl` | C-5 — کل تاریخچه |
| ۶ | `/Reports/CompanyOverview` | C-5 |
| ۷ | `/ContractJourney/Details` | H-2 — ۷۰ کوئری |
| ۸ | `/Reports/InventoryOperations` | C-5 + M-5 |
| ۹ | `/SarrafSettlements` | H-3 — بدون صفحه‌بندی |
| ۱۰ | `/ThreeWaySettlement` | H-3 — بدون صفحه‌بندی |

### ایندکس‌های پیشنهادی

ایندکس‌گذاری فعلی قوی است. فقط این موارد **احتمالاً** کم است — **قبل از ساخت، با `EXPLAIN` روی دیتابیس تست تأیید شود**:

```sql
-- برای جست‌وجوی متنی (M-1) — نیازمند: CREATE EXTENSION pg_trgm;
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Contracts_ContractNumber_trgm"
    ON "Contracts" USING gin ("ContractNumber" gin_trgm_ops);
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Customers_Name_trgm"
    ON "Customers" USING gin ("Name" gin_trgm_ops);
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Suppliers_Name_trgm"
    ON "Suppliers" USING gin ("Name" gin_trgm_ops);

-- برای کارت انبار (C-1) — ترتیب دقیقاً مطابق OrderBy موجود
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_InventoryMovements_Product_Terminal_Date_Id"
    ON "InventoryMovements" ("ProductId", "TerminalId", "MovementDate", "Id");

-- برای Anti-Join داشبورد (H-1)
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_LoadingReceipts_LoadingRegisterId"
    ON "LoadingReceipts" ("LoadingRegisterId");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_PaymentTransactions_SalesTransactionId"
    ON "PaymentTransactions" ("SalesTransactionId");
```

> **هشدار:** حتماً `CONCURRENTLY` استفاده شود تا جدول قفل نشود. این دستورات در Migration معمولی EF کار نمی‌کنند (باید خارج از Transaction اجرا شوند).
> **بررسی نشده:** به‌دلیل نبود دسترسی به دیتابیس تست، هیچ `EXPLAIN`/`EXPLAIN ANALYZE` اجرا نشد. این پیشنهادها **صرفاً بر پایهٔ تحلیل کد** هستند و باید قبل از اعمال با `EXPLAIN` تأیید شوند.

### اصلاحات سریع و کم‌ریسک (منطق سیستم را تغییر نمی‌دهند)

| # | اقدام | فایل | سود |
|---|---|---|---|
| ۱ | سقف تعداد ردیف برای خروجی CSV | `LedgerController.cs:87`، `PaymentsController.cs:173` | جلوگیری از OOM |
| ۲ | کش ۶۰ ثانیه‌ای داشبورد | `DashboardService.cs:29` | حذف ۵۶ کوئری در هر بازدید |
| ۳ | افزودن `AsSplitQuery()` به لغو گروهی | `CustomsDeclarationsController.cs:848` | حذف ضرب دکارتی |
| ۴ | افزودن صفحه‌بندی به فهرست‌های تسویه | ۷ کنترلر (H-3) | حذف بارگذاری کل جدول |
| ۵ | تعیین `CommandTimeout` + Rate Limiter | `Program.cs:49` | جلوگیری از تمام‌شدن Connection Pool |
| ۶ | ایندکس‌های GIN برای جست‌وجو | Migration جدید | حذف Seq Scan جست‌وجو |

### اصلاحات حساس (نیازمند تست حسابداری)

| # | اقدام | چرا حساس |
|---|---|---|
| ۱ | بازنویسی محاسبهٔ مانده کارت انبار با Window Function (C-1 گام ۳) | مانده موجودی مستقیماً روی بهای تمام‌شده اثر دارد |
| ۲ | دسته‌ای‌کردن `InventoryLineageBackfillService` (C-4) | ترتیب FIFO نباید تغییر کند |
| ۳ | بازهٔ تاریخ پیش‌فرض گزارش‌ها (C-5) | اعداد نمایش‌داده‌شده تغییر می‌کنند |
| ۴ | انتقال `SaveChangesAsync` به بیرون حلقه (M-3) | ممکن است به Id تولیدشده یا ترتیب رویداد وابسته باشد |
| ۵ | کوتاه‌کردن مرز تراکنش‌ها (M-4) | تغییر تضمین اتمیک بودن |
| ۶ | محدودکردن شمارنده‌های داشبورد به بازهٔ اخیر (H-1 گام ۲) | معنی شمارنده عوض می‌شود |

### ترتیب امن اجرای بهینه‌سازی‌ها

**فاز ۱ — بدون ریسک، بدون تغییر منطق (همین حالا قابل اجرا):**
1. سقف ردیف برای خروجی CSV (C-2)
2. `AsSplitQuery` در لغو گروهی (M-2)
3. `CommandTimeout` + Rate Limiter (H-4)
4. کش داشبورد (H-1 گام ۱)

**فاز ۲ — کم‌ریسک، فقط تغییر نمایش:**
5. صفحه‌بندی فهرست‌های تسویه (H-3)
6. خروجی جریانی CSV (C-3)
7. ایندکس‌های GIN با `CONCURRENTLY` (M-1) — **پس از تأیید با `EXPLAIN`**

**فاز ۳ — نیازمند تأیید کاربر تجاری:**
8. فیلتر اجباری + بازهٔ پیش‌فرض کارت انبار (C-1 گام ۱-۲)
9. بازهٔ تاریخ پیش‌فرض گزارش‌ها (C-5)

**فاز ۴ — نیازمند تست کامل حسابداری و مقایسهٔ خروجی قبل/بعد:**
10. Window Function برای مانده انبار (C-1 گام ۳)
11. دسته‌ای‌کردن Backfill (C-4)
12. بازبینی `SaveChangesAsync` در حلقه (M-3)
13. بازبینی مرز تراکنش‌ها (M-4)

> **قاعدهٔ کلی:** هیچ‌گاه دو مورد از فاز ۴ را در یک تغییر با هم انجام ندهید. هر کدام جداگانه، با تست حسابداری، و با مقایسهٔ خروجی قبل/بعد روی دادهٔ واقعی.

---

## محدودیت‌های این بررسی

این گزارش **فقط بر پایهٔ تحلیل ایستای کد** است. موارد زیر انجام **نشد**:

- **هیچ `EXPLAIN` یا `EXPLAIN ANALYZE` اجرا نشد** — دیتابیس تست در دسترس نبود (احراز هویت PostgreSQL ناموفق).
- **هیچ SQL تولیدشدهٔ EF Core مشاهده نشد** — نیازمند اجرای واقعی با Logging است.
- **هیچ اندازه‌گیری واقعی زمان یا حجم حافظه انجام نشد.**
- ارزیابی شدت‌ها بر پایهٔ الگوی کد و اندازهٔ مورد انتظار جداول است، نه اندازه‌گیری.

**گام بعدی پیشنهادی برای تکمیل بررسی:** فراهم‌کردن دسترسی به یک دیتابیس تست با حجم واقعی، سپس اجرای `EXPLAIN (ANALYZE, BUFFERS)` روی ۱۰ کوئری فهرست‌شده در بالا. پروژه از قبل زیرساخت `MvcQueryCountingInterceptor` ([Program.cs:45](src/PTGOilSystem.Web/Program.cs#L45)) را در حالت Development دارد که برای شمارش کوئری هر درخواست بسیار مفید است.
