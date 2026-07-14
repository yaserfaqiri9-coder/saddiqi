// fx-rate-field.js
// مکانیزم مشترک «نرخ دالر به ارز» برای همهٔ فورم‌های ارزی.
// قانون واحد: کاربر فقط نرخ ساده «۱ دالر = چند واحد ارز» را وارد می‌کند؛ نرخ معکوس/فنی نمایش داده نمی‌شود.
//
// دو الگو پشتیبانی می‌شود:
//   الف) فیلد ساده model-bound است و کنترلر خودش 1/نرخ را حساب می‌کند (مثل Expenses, Payments).
//       <div data-fx-rate-group data-fx-currency-source="#Currency">
//           <div data-fx-rate-field> <input data-fx-rate-input asp-for="DocumentCurrencyPerUsdRate"> <span data-fx-currency-name></span> </div>
//           <div data-fx-usd-note class="d-none"> پرداخت دالری است؛ نرخ تبدیل لازم نیست. </div>
//       </div>
//   ب) فیلد نمایشی غیرمدلی + یک hidden فنی که JS با 1/نرخ پر می‌کند (کنترلر دست‌نخورده می‌ماند).
//       <input data-fx-rate-input id="...">                         (نمایشی، بدون name)
//       <input data-fx-rate-technical type="hidden" name="AppliedFxRateToUsd" value="@Model.AppliedFxRateToUsd">
(function () {
    "use strict";

    // نام دری ارزها برای برچسب «نرخ دالر به …»؛ کد ناشناخته همان‌طور نشان داده می‌شود.
    const currencyNames = {
        "USD": "دالر",
        "RUB": "روبل",
        "EUR": "یورو",
        "AED": "درهم",
        "AFN": "افغانی",
        "PKR": "کلدار",
        "TRY": "لیره",
        "CNY": "یوان"
    };

    function currencyDisplayName(code) {
        const key = (code || "").trim().toUpperCase();
        return currencyNames[key] || key || "ارز";
    }

    function toNumber(value) {
        const n = parseFloat((value || "").toString().replace(/,/g, ""));
        return isFinite(n) ? n : 0;
    }

    // نمایش تمیز نرخ بازسازی‌شده تا نویز اعشاری (۷۶٫۹۹۹۹ به‌جای ۷۷) دیده نشود.
    function prettyRate(value) {
        if (!isFinite(value) || value <= 0) {
            return "";
        }
        const asInt = Math.round(value);
        if (Math.abs(value - asInt) < 0.005) {
            return String(asInt);
        }
        return String(Math.round(value * 10000) / 10000);
    }

    function resolveCurrency(group) {
        const fixed = group.getAttribute("data-fx-currency");
        if (fixed) {
            return fixed.trim().toUpperCase();
        }

        const selector = group.getAttribute("data-fx-currency-source");
        if (!selector) {
            return "";
        }

        const source = document.querySelector(selector);
        if (!source) {
            return "";
        }

        return (source.value || "").trim().toUpperCase();
    }

    // نرخ سادهٔ نمایشی → نرخ فنی hidden (الگوی ب).
    function syncTechnical(group, isUsd) {
        const technical = group.querySelector("[data-fx-rate-technical]");
        if (!technical) {
            return;
        }

        if (isUsd) {
            technical.value = "1"; // USD: تبدیل همان مقدار است؛ ۱ همیشه معتبر است.
            return;
        }

        const input = group.querySelector("[data-fx-rate-input]");
        const display = input ? toNumber(input.value) : 0;
        technical.value = display > 0 ? (1 / display) : ""; // خالی → fallback نرخ روزانهٔ سرور
    }

    // در حالت ویرایش، اگر فقط نرخ فنی موجود است، نرخ سادهٔ قابل‌فهم را بازسازی کن.
    function reconstructDisplay(group, isUsd) {
        if (isUsd) {
            return;
        }
        const input = group.querySelector("[data-fx-rate-input]");
        const technical = group.querySelector("[data-fx-rate-technical]");
        if (!input || !technical) {
            return;
        }
        if (toNumber(input.value) > 0) {
            return; // کاربر مقدار دارد؛ دست نزن.
        }
        const tech = toNumber(technical.value);
        if (tech > 0) {
            input.value = prettyRate(1 / tech);
        }
    }

    function apply(group) {
        const code = resolveCurrency(group);
        const isUsd = code === "" || code === "USD";

        const field = group.querySelector("[data-fx-rate-field]");
        const note = group.querySelector("[data-fx-usd-note]");
        const input = group.querySelector("[data-fx-rate-input]");

        if (field) {
            field.classList.toggle("d-none", isUsd);
        }
        if (note) {
            note.classList.toggle("d-none", !isUsd);
        }

        const name = currencyDisplayName(code);
        group.querySelectorAll("[data-fx-currency-name]").forEach(function (target) {
            target.textContent = name;
        });

        if (input) {
            if (isUsd) {
                input.value = "";
                input.required = false;
                input.removeAttribute("required");
            } else {
                input.required = true;
                input.setAttribute("required", "required");
            }
        }

        syncTechnical(group, isUsd);
    }

    function init() {
        const groups = Array.prototype.slice.call(document.querySelectorAll("[data-fx-rate-group]"));
        groups.forEach(function (group) {
            const initialCode = resolveCurrency(group);
            const initialIsUsd = initialCode === "" || initialCode === "USD";
            reconstructDisplay(group, initialIsUsd);

            apply(group);

            const input = group.querySelector("[data-fx-rate-input]");
            if (input) {
                input.addEventListener("input", function () {
                    syncTechnical(group, resolveCurrency(group) === "USD" || resolveCurrency(group) === "");
                });
            }

            const selector = group.getAttribute("data-fx-currency-source");
            if (selector) {
                const source = document.querySelector(selector);
                if (source) {
                    source.addEventListener("change", function () { apply(group); });
                }
            }
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }

    // در دسترس برای فورم‌هایی که خودشان پس از تغییر ارز نیاز به sync دارند.
    window.PTG = window.PTG || {};
    window.PTG.refreshFxRateFields = init;
})();
