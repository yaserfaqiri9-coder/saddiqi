// commission-field.js — فرم روزنامچه: بخش کمیسیون (اختیاری).
// فقط UI: نمایش/مخفی‌سازی فیلدها بر اساس نوع، و محاسبهٔ زندهٔ مبلغ کمیسیون.
// هیچ منطق مالی سمت کلاینت قطعی نیست؛ محاسبهٔ نهایی و اعتبارسنجی در سرور انجام می‌شود.
(function () {
    'use strict';

    function init() {
        var section = document.querySelector('[data-commission-section]');
        if (!section) { return; }

        var form = section.closest('form');
        var toggle = section.querySelector('[data-commission-toggle]');
        var body = section.querySelector('[data-commission-body]');
        var typeSel = section.querySelector('[data-commission-type]');
        var percentField = section.querySelector('[data-commission-percent-field]');
        var fixedField = section.querySelector('[data-commission-fixed-field]');
        var currencyField = section.querySelector('[data-commission-currency-field]');
        var rateField = section.querySelector('[data-commission-rate-field]');
        var accountField = section.querySelector('[data-commission-account-field]');
        var percentInput = section.querySelector('[data-commission-percent]');
        var fixedInput = section.querySelector('[data-commission-fixed]');
        var currencySel = section.querySelector('[data-commission-currency]');
        var mainOut = section.querySelector('[data-commission-main]');
        var amountOut = section.querySelector('[data-commission-amount]');
        var effectOut = section.querySelector('[data-commission-effect]');
        var sarrafNote = section.querySelector('[data-commission-sarraf-note]');

        var PERCENT = '1';
        var FIXED = '2';

        function parseNum(v) {
            if (v == null) { return 0; }
            var s = ('' + v).replace(/[^\d.\-]/g, '');
            var n = parseFloat(s);
            return isNaN(n) ? 0 : n;
        }

        function isViaSarraf() {
            var input = document.getElementById('paymentMethodInput');
            return input && input.value === '1';
        }

        function mainAmount() {
            if (isViaSarraf()) {
                var s = form && form.querySelector('[name="SarrafSupplierAmount"]');
                return parseNum(s && s.value);
            }
            var a = document.getElementById('Amount');
            return parseNum(a && a.value);
        }

        function mainCurrency() {
            if (isViaSarraf()) {
                var sc = document.getElementById('sarrafCurrency');
                return (sc && sc.value) || section.getAttribute('data-main-currency') || 'USD';
            }
            var pc = document.getElementById('paymentCurrency');
            return (pc && pc.value) || section.getAttribute('data-main-currency') || 'USD';
        }

        function show(el, on) {
            if (!el) { return; }
            el.classList.toggle('d-none', !on);
        }

        function refresh() {
            var enabled = toggle && toggle.checked;
            show(body, enabled);
            if (!enabled) { return; }

            var type = typeSel ? typeSel.value : '';
            var via = isViaSarraf();

            show(percentField, type === PERCENT);
            show(fixedField, type === FIXED);
            // ارز/نرخ فقط برای مبلغ ثابت؛ در حالت درصدی ارز = ارز پرداخت اصلی.
            show(currencyField, type === FIXED);
            show(rateField, type === FIXED);
            // حساب پرداخت کمیسیون فقط در حالت نقد/بانک.
            show(accountField, !via);
            show(sarrafNote, via);

            var curPercent = mainCurrency();
            var amount = 0;
            var cur = curPercent;

            if (type === PERCENT) {
                amount = mainAmount() * parseNum(percentInput && percentInput.value) / 100;
                cur = curPercent;
            } else if (type === FIXED) {
                amount = parseNum(fixedInput && fixedInput.value);
                cur = (currencySel && currencySel.value) || curPercent;
            }

            if (mainOut) { mainOut.textContent = mainCurrency() + ' ' + mainAmount().toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
            if (amountOut) { amountOut.textContent = cur + ' ' + amount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
            if (effectOut) {
                effectOut.textContent = via
                    ? 'اثر: افزوده‌شدن به حساب صراف (صندوق دست‌نخورده)'
                    : 'اثر: مصرف در P&L + کم‌شدن از حساب انتخابی';
            }
        }

        if (toggle) { toggle.addEventListener('change', refresh); }
        if (typeSel) { typeSel.addEventListener('change', refresh); }
        ['input', 'change'].forEach(function (ev) {
            [percentInput, fixedInput, currencySel].forEach(function (el) {
                if (el) { el.addEventListener(ev, refresh); }
            });
            var a = document.getElementById('Amount');
            if (a) { a.addEventListener(ev, refresh); }
            var s = form && form.querySelector('[name="SarrafSupplierAmount"]');
            if (s) { s.addEventListener(ev, refresh); }
            var pc = document.getElementById('paymentCurrency');
            if (pc) { pc.addEventListener(ev, refresh); }
            var sc = document.getElementById('sarrafCurrency');
            if (sc) { sc.addEventListener(ev, refresh); }
        });
        // تغییر روش پرداخت (نقد/بانک ↔ صراف) توسط roznamcha-form.js انجام می‌شود؛ پس از کلیک دوباره محاسبه کن.
        document.querySelectorAll('[data-payment-method-choice]').forEach(function (btn) {
            btn.addEventListener('click', function () { setTimeout(refresh, 0); });
        });

        refresh();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
