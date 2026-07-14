/*
 * PTG Page Loader manager
 * API: window.PTG.loader.show() / hide() / reset()
 *  - نمایش فقط اگر کار بیش از 180ms طول بکشد (بدون چشمک).
 *  - پس از نمایش، حداقل 350ms روی صفحه می‌ماند.
 *  - refCount: هرگز دو لودر هم‌زمان یا لودر گیرکرده.
 *  - safety timeout: خطای JS یا درخواست معلق، overlay را قفل نمی‌کند.
 */
(function () {
    "use strict";

    var DELAY_MS = 180;
    var MIN_VISIBLE_MS = 350;
    var SAFETY_MS = 20000;

    var refCount = 0;
    var visible = false;
    var shownAt = 0;
    var showTimer = null;
    var hideTimer = null;
    var safetyTimer = null;

    function node() {
        return document.getElementById("ptg-page-loader");
    }

    function clearTimers() {
        window.clearTimeout(showTimer);
        window.clearTimeout(hideTimer);
        window.clearTimeout(safetyTimer);
        showTimer = null;
        hideTimer = null;
        safetyTimer = null;
    }

    function reveal() {
        showTimer = null;
        var el = node();
        if (!el) return;
        visible = true;
        shownAt = Date.now();
        el.classList.add("is-active");
        el.setAttribute("aria-hidden", "false");
        el.setAttribute("aria-busy", "true");
        window.clearTimeout(safetyTimer);
        safetyTimer = window.setTimeout(function () { hide(true); }, SAFETY_MS);
    }

    function conceal() {
        hideTimer = null;
        visible = false;
        var el = node();
        if (!el) return;
        el.classList.remove("is-active");
        el.setAttribute("aria-hidden", "true");
        el.setAttribute("aria-busy", "false");
    }

    function show() {
        refCount++;
        if (refCount > 1) return;
        window.clearTimeout(hideTimer);
        hideTimer = null;
        if (visible) return;
        window.clearTimeout(showTimer);
        showTimer = window.setTimeout(reveal, DELAY_MS);
    }

    function hide(force) {
        refCount = force ? 0 : Math.max(0, refCount - 1);
        if (refCount > 0) return;

        window.clearTimeout(showTimer);
        showTimer = null;
        window.clearTimeout(safetyTimer);
        safetyTimer = null;

        if (!visible) return;

        var wait = force ? 0 : Math.max(0, MIN_VISIBLE_MS - (Date.now() - shownAt));
        window.clearTimeout(hideTimer);
        hideTimer = window.setTimeout(conceal, wait);
    }

    function reset() {
        clearTimers();
        refCount = 0;
        conceal();
    }

    window.PTG = window.PTG || {};
    window.PTG.loader = { show: show, hide: hide, reset: reset };

    // لود اولیهٔ صفحه
    if (document.readyState !== "complete") {
        show();
        window.addEventListener("load", function () { hide(true); }, { once: true });
    }

    // Back/Forward و bfcache: هیچ overlay باقی‌مانده‌ای روی صفحه نماند.
    window.addEventListener("pageshow", function () { reset(); });
    window.addEventListener("pagehide", function () { reset(); });
})();
