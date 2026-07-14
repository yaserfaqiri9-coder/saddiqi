(function () {
    "use strict";

    // Shared date picker: progressive enhancement over native <input type="date">.
    // The native input stays in the DOM as the single value / binding / validation
    // source (ISO yyyy-mm-dd). This layer only supplies a calibrated display field
    // ("13 Jul 2026") and a light calendar popup. No markup or value semantics change.

    var MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
    var WEEK = ["Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"];
    var CAL_SVG = '<svg viewBox="0 0 20 20" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4.5" width="14" height="12.5" rx="2"/><path d="M3 8h14M7 3v3M13 3v3"/></svg>';
    var CHEV = '<svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M10 3 5 8l5 5"/></svg>';

    function pad(n) { return (n < 10 ? "0" : "") + n; }
    function iso(y, m, d) { return y + "-" + pad(m + 1) + "-" + pad(d); }

    // The calendar is portalled to a body-level overlay: an ancestor with
    // `overflow:auto` (e.g. the search/filter popover) would otherwise clip it,
    // and no z-index can escape that clip.
    var GAP = 6;
    var overlayRoot = null;
    function overlay() {
        if (overlayRoot && document.body.contains(overlayRoot)) return overlayRoot;
        overlayRoot = document.querySelector(".ak-overlay-root");
        if (!overlayRoot) {
            overlayRoot = document.createElement("div");
            overlayRoot.className = "ak-overlay-root";
            document.body.appendChild(overlayRoot);
        }
        return overlayRoot;
    }

    function isRtl() {
        return (document.documentElement.getAttribute("dir") || "").toLowerCase() === "rtl";
    }

    function place(pop, anchor) {
        var r = anchor.getBoundingClientRect();
        var w = pop.offsetWidth || 268;
        var h = pop.offsetHeight || 300;
        var vw = document.documentElement.clientWidth;
        var vh = document.documentElement.clientHeight;

        var left = isRtl() ? r.right - w : r.left;
        left = Math.max(8, Math.min(left, vw - w - 8));

        var top = r.bottom + GAP;
        if (top + h > vh - 8 && r.top - GAP - h > 8) top = r.top - GAP - h;  // flip above
        top = Math.max(8, Math.min(top, vh - h - 8));

        pop.style.left = Math.round(left) + "px";
        pop.style.top = Math.round(top) + "px";
    }

    function parseISO(value) {
        var m = /^(\d{4})-(\d{2})-(\d{2})$/.exec((value || "").trim());
        if (!m) return null;
        var d = new Date(+m[1], +m[2] - 1, +m[3]);
        return isNaN(d.getTime()) ? null : d;
    }

    function format(date) {
        return date.getDate() + " " + MONTHS[date.getMonth()] + " " + date.getFullYear();
    }

    function enhance(native) {
        if (!native || native.dataset.akDateReady === "true") return;
        if (native.dataset.akDatepicker === "false" || native.closest("[data-loading-date-picker]")) return;
        native.dataset.akDateReady = "true";

        var minDate = parseISO(native.getAttribute("min"));
        var maxDate = parseISO(native.getAttribute("max"));

        var root = document.createElement("div");
        root.className = "ak-datepicker";
        root.dataset.open = "false";

        var field = document.createElement("button");
        field.type = "button";
        field.className = "ak-date-field";
        field.setAttribute("aria-haspopup", "dialog");
        field.setAttribute("aria-expanded", "false");

        var display = document.createElement("span");
        display.className = "ak-date-display";

        var icon = document.createElement("span");
        icon.className = "ak-date-icon";
        icon.setAttribute("aria-hidden", "true");
        icon.innerHTML = CAL_SVG;

        field.appendChild(display);
        field.appendChild(icon);

        var pop = document.createElement("div");
        pop.className = "ak-date-pop";
        pop.setAttribute("role", "dialog");
        pop.setAttribute("dir", "ltr");
        pop.innerHTML =
            '<div class="ak-date-head">' +
                '<button type="button" class="ak-date-nav" data-nav="-1" aria-label="Previous month">' + CHEV + '</button>' +
                '<button type="button" class="ak-date-title" aria-label="Choose year"></button>' +
                '<button type="button" class="ak-date-nav ak-date-nav-next" data-nav="1" aria-label="Next month">' + CHEV + '</button>' +
            '</div>' +
            '<div class="ak-date-week"></div>' +
            '<div class="ak-date-grid"></div>' +
            '<div class="ak-date-years" hidden></div>';

        native.parentNode.insertBefore(root, native);
        root.appendChild(native);
        native.classList.add("ak-date-native");
        root.appendChild(field);
        root.appendChild(pop);

        var title = pop.querySelector(".ak-date-title");
        var weekRow = pop.querySelector(".ak-date-week");
        var grid = pop.querySelector(".ak-date-grid");
        var years = pop.querySelector(".ak-date-years");
        weekRow.innerHTML = WEEK.map(function (w) { return '<span>' + w + '</span>'; }).join("");

        var view = new Date();
        view.setDate(1);

        function selected() { return parseISO(native.value); }

        function disabledDay(y, m, d) {
            var t = new Date(y, m, d).getTime();
            if (minDate && t < minDate.getTime()) return true;
            if (maxDate && t > maxDate.getTime()) return true;
            return false;
        }

        function syncDisplay() {
            var sel = selected();
            display.textContent = sel ? format(sel) : (native.dataset.akPlaceholder || "");
            root.dataset.empty = sel ? "false" : "true";
            var off = native.disabled || native.readOnly;
            field.disabled = native.disabled;
            root.dataset.readonly = native.readOnly ? "true" : "false";
            root.dataset.invalid = native.classList.contains("input-validation-error") ? "true" : "false";
        }

        function renderDays() {
            years.hidden = true;
            grid.hidden = false;
            weekRow.style.display = "";
            var y = view.getFullYear(), m = view.getMonth();
            title.textContent = MONTHS[m] + " " + y;
            var first = new Date(y, m, 1).getDay();
            var days = new Date(y, m + 1, 0).getDate();
            var prevDays = new Date(y, m, 0).getDate();
            var sel = selected();
            var today = new Date();
            var cells = "";
            for (var i = 0; i < first; i++) {
                cells += '<button type="button" class="ak-date-day is-outside" tabindex="-1" disabled>' + (prevDays - first + 1 + i) + '</button>';
            }
            for (var d = 1; d <= days; d++) {
                var cls = "ak-date-day";
                if (disabledDay(y, m, d)) cls += " is-disabled";
                if (sel && sel.getFullYear() === y && sel.getMonth() === m && sel.getDate() === d) cls += " is-selected";
                if (today.getFullYear() === y && today.getMonth() === m && today.getDate() === d) cls += " is-today";
                cells += '<button type="button" class="' + cls + '" data-day="' + d + '"' + (disabledDay(y, m, d) ? " disabled" : "") + '>' + d + '</button>';
            }
            grid.innerHTML = cells;
        }

        function renderYears() {
            grid.hidden = true;
            weekRow.style.display = "none";
            years.hidden = false;
            var cur = view.getFullYear();
            var start = cur - 6;
            var html = "";
            for (var i = 0; i < 12; i++) {
                var yr = start + i;
                html += '<button type="button" class="ak-date-year' + (yr === cur ? " is-selected" : "") + '" data-year="' + yr + '">' + yr + '</button>';
            }
            years.innerHTML = html;
            title.textContent = start + " – " + (start + 11);
        }

        function reposition() { if (root.dataset.open === "true") place(pop, field); }

        function open() {
            if (native.disabled || native.readOnly) return;
            var sel = selected();
            if (sel) { view = new Date(sel.getFullYear(), sel.getMonth(), 1); }
            root.dataset.open = "true";
            field.setAttribute("aria-expanded", "true");
            renderDays();
            overlay().appendChild(pop);   // portal out of any clipping ancestor
            pop.classList.add("is-open");
            place(pop, field);
            window.addEventListener("scroll", reposition, true);
            window.addEventListener("resize", reposition);
        }

        function close() {
            root.dataset.open = "false";
            field.setAttribute("aria-expanded", "false");
            years.hidden = true;
            pop.classList.remove("is-open");
            window.removeEventListener("scroll", reposition, true);
            window.removeEventListener("resize", reposition);
            if (pop.parentNode !== root) root.appendChild(pop);   // back home when closed
        }

        function pick(y, m, d) {
            native.value = iso(y, m, d);
            native.dispatchEvent(new Event("input", { bubbles: true }));
            native.dispatchEvent(new Event("change", { bubbles: true }));
            syncDisplay();
            close();
            field.focus();
        }

        field.addEventListener("click", function () {
            if (root.dataset.open === "true") { close(); } else { open(); }
        });

        pop.addEventListener("click", function (event) {
            var nav = event.target.closest("[data-nav]");
            if (nav) { view.setMonth(view.getMonth() + (+nav.dataset.nav)); renderDays(); return; }
            if (event.target.closest(".ak-date-title")) {
                if (years.hidden) { renderYears(); } else { renderDays(); }
                return;
            }
            var yr = event.target.closest("[data-year]");
            if (yr) { view.setFullYear(+yr.dataset.year); renderDays(); return; }
            var day = event.target.closest("[data-day]:not([disabled])");
            if (day) { pick(view.getFullYear(), view.getMonth(), +day.dataset.day); }
        });

        field.addEventListener("keydown", function (event) {
            if ((event.key === "ArrowDown" || event.key === "Enter" || event.key === " ") && root.dataset.open !== "true") {
                event.preventDefault();
                open();
            } else if (event.key === "Escape" && root.dataset.open === "true") {
                event.preventDefault();
                close();
            }
        });

        root.addEventListener("keydown", function (event) {
            if (event.key === "Escape" && root.dataset.open === "true") {
                event.preventDefault();
                close();
                field.focus();
            }
        });

        // Keep the label[for] behaviour: focusing the native input focuses the field.
        native.addEventListener("focus", function () { field.focus(); });
        native.addEventListener("change", syncDisplay);

        root._akDateClose = close;
        root._akDatePop = pop;
        pop._akDateOwner = root;

        new MutationObserver(syncDisplay).observe(native, {
            attributes: true,
            attributeFilter: ["value", "disabled", "readonly", "class", "min", "max"]
        });

        syncDisplay();
    }

    function scan(node) {
        if (!node) return;
        if (node.matches && node.matches('input[type="date"]')) enhance(node);
        if (node.querySelectorAll) node.querySelectorAll('input[type="date"]').forEach(enhance);
    }

    function start() {
        scan(document);
        document.addEventListener("pointerdown", function (event) {
            document.querySelectorAll('.ak-datepicker[data-open="true"]').forEach(function (root) {
                // While open the calendar lives in the overlay, not inside root.
                var pop = root._akDatePop;
                var inside = root.contains(event.target) || (pop && pop.contains(event.target));
                if (!inside && typeof root._akDateClose === "function") root._akDateClose();
            });
        });
        // SPA navigation can drop the owner without closing: never leave a portalled
        // calendar orphaned in the overlay.
        window.addEventListener("ptg:page-ready", function () {
            var host = document.querySelector(".ak-overlay-root");
            if (!host) return;
            Array.prototype.forEach.call(host.children, function (pop) {
                var owner = pop._akDateOwner;
                if (!owner || !document.body.contains(owner)) pop.remove();
                else if (typeof owner._akDateClose === "function") owner._akDateClose();
            });
        });
        new MutationObserver(function (records) {
            records.forEach(function (record) { record.addedNodes.forEach(scan); });
        }).observe(document.body, { childList: true, subtree: true });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", start, { once: true });
    } else {
        start();
    }
})();
