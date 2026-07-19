# بسته رابط Akaunting-inspired برای ASP.NET Core MVC

این بسته یک پیاده‌سازی مستقل و Clean-room از الگوی رابط عمومی Akaunting است. هیچ لوگو، تصویر برند، فایل کامپایل‌شده یا کد اختصاصی نسخه Cloud در آن کپی نشده است.

## فایل‌ها

- `demo/index.html` دموی کامل و قابل اجرا
- `wwwroot/css/akaunting-inspired.css` تمام رنگ‌ها، ابعاد، سایدبار، فیلتر، جدول و Responsive
- `wwwroot/js/akaunting-filter.js` ماشین حالت فیلتر و رفتار سایدبار
- `Views/Shared/_AkauntingSidebar.cshtml` Partial آماده ASP.NET Core
- `Views/Shared/_AkauntingSearchFilter.cshtml` Partial نوار جستجو/فیلتر
- `Models/FilterModels.cs` مدل‌های درخواست فیلتر
- `Services/CustomerFilterService.cs` نمونه اعمال فیلتر امن با EF Core
- `design-tokens.json` توکن‌های رنگ، فاصله، حرکت و تایپوگرافی

## اجرای دمو

فایل زیر را مستقیم در Chrome یا Edge باز کنید:

```text
demo/index.html
```

در نوار فیلتر کلیک کنید:

1. فیلد را انتخاب کنید.
2. عملگر را انتخاب کنید.
3. مقدار را انتخاب کنید.
4. برای چند مقدار، چک‌باکس‌ها را انتخاب و Apply را بزنید.
5. برای تاریخ، تاریخ شروع/پایان را ثبت کنید.
6. Enter فیلتر را روی جدول اعمال می‌کند.
7. Backspace در ورودی خالی آخرین توکن را حذف می‌کند.
8. Escape پنجره را می‌بندد.

## اتصال به پروژه ASP.NET Core

### 1. فایل‌های استاتیک

این دو فایل را داخل پروژه کپی کنید:

```text
wwwroot/css/akaunting-inspired.css
wwwroot/js/akaunting-filter.js
```

در `_Layout.cshtml`:

```html
<link rel="stylesheet" href="~/css/akaunting-inspired.css" asp-append-version="true" />
<script src="~/js/akaunting-filter.js" asp-append-version="true"></script>
```

### 2. پوسته صفحه

داخل Layout:

```cshtml
<div class="ak-shell" id="appShell">
    <div class="ak-overlay" data-ak-overlay></div>
    <partial name="_AkauntingSidebar" />

    <main class="ak-content">
        @RenderBody()
    </main>
</div>

<script>
    new AkSidebar(document.getElementById("appShell"));
</script>
```

### 3. نوار فیلتر

داخل صفحه `Customers/Index.cshtml`:

```cshtml
<partial name="_AkauntingSearchFilter" />

<script>
const bar = new AkFilterBar(document.querySelector("[data-ak-filter]"), {
    fields: [
        {
            key: "status",
            label: "وضعیت",
            type: "select",
            operators: ["eq", "neq", "in"],
            multiple: true,
            values: [
                { key: "active", label: "فعال" },
                { key: "inactive", label: "غیرفعال" }
            ]
        },
        {
            key: "created_at",
            label: "تاریخ ایجاد",
            type: "date",
            operators: ["eq", "neq", "between"]
        }
    ],
    onApply: async payload => {
        const response = await fetch("/Customers/Filter", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken":
                    document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? ""
            },
            body: JSON.stringify({
                text: payload.text,
                filters: payload.tokens.map(x => ({
                    field: x.fieldKey,
                    operator: ({
                        eq: 0,
                        neq: 1,
                        in: 2,
                        between: 3,
                        contains: 4
                    })[x.operator],
                    values: Array.isArray(x.value) ? x.value.map(String) : [String(x.value)]
                }))
            })
        });

        if (!response.ok) throw new Error("Filter request failed.");
        const html = await response.text();
        document.querySelector("#customer-results").innerHTML = html;
    }
});
</script>
```

### 4. نکته امنیتی مهم

نام ستون یا عبارت LINQ را مستقیماً از کاربر نپذیرید. در Backend همیشه از `switch` یا فهرست سفید استفاده کنید؛ نمونه آن در `CustomerFilterService.cs` قرار دارد. این روش جلوی فیلتر روی فیلدهای غیرمجاز و ساخت Query ناامن را می‌گیرد.

## قواعد طراحی

- نوار ابزار: 56px
- پنل اصلی سایدبار: 224px در Desktop و 256px در Mobile
- پس‌زمینه پنل اصلی: `#F2F4FC`
- رنگ اصلی متن/آیکن: `#55588B`
- ارتفاع نوار جستجو: 48px
- فاصله بالا و پایین نوار جستجو: 20px
- پنجره فیلتر: 32px فاصله از لبه داخلی، 8px padding، radius 6px
- ردیف گزینه: 36px
- بازشدن زیرمنو: 500ms ease-out از `opacity:0` و `translateY(-0.5em)`
- transition عمومی: 150ms
- فونت لاتین: Quicksand
- برای فارسی: Vazirmatn/Tahoma پیشنهاد شده است

## رفتار پیشنهادی Production

- متن جستجو را با Debounce حدود 250 تا 350 میلی‌ثانیه فقط برای پیشنهادهای Remote بفرستید.
- اجرای نهایی Query را با Enter یا انتخاب کامل توکن انجام دهید.
- مقادیر Remote را در بسته‌های 10تایی دریافت کنید.
- وضعیت فیلتر را در URL نگه دارید تا Refresh و Share درست کار کند.
- در پروژه‌های مالی، پارامتر ساختاری JSON امن‌تر از Parser متن آزاد است؛ رشته شبیه Akaunting را فقط برای نمایش یا URL خلاصه نگه دارید.
- برای لیست‌های بزرگ، Pagination و Sorting باید روی Server انجام شود.
