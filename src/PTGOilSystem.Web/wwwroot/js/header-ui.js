// Header UI: search dialog + account drawer.
// Accessible overlays — focus trap, Escape close, body scroll lock, restore focus.
(function () {
    "use strict";

    var FOCUSABLE = 'a[href],button:not([disabled]),input:not([disabled]),select,textarea,[tabindex]:not([tabindex="-1"])';

    function lockScroll(on) {
        document.documentElement.style.overflow = on ? "hidden" : "";
    }

    function focusables(container) {
        return Array.prototype.filter.call(
            container.querySelectorAll(FOCUSABLE),
            function (el) { return el.offsetParent !== null || el === document.activeElement; }
        );
    }

    // Generic overlay controller shared by dialog + drawer.
    function makeOverlay(opts) {
        var root = opts.root;            // element toggled via [hidden]
        var panel = opts.panel;          // dialog/drawer element to trap focus in
        var trigger = null;              // element that opened it (to restore focus)
        var openClass = opts.openClass;  // class added to <body> for CSS transitions
        var initialFocus = opts.initialFocus;

        function isOpen() { return !root.hidden; }

        function open(fromEl) {
            if (isOpen()) return;
            trigger = fromEl || document.activeElement;
            root.hidden = false;
            // next frame → allow CSS transition from the closed state
            requestAnimationFrame(function () {
                document.body.classList.add(openClass);
            });
            lockScroll(true);
            if (trigger && trigger.setAttribute) trigger.setAttribute("aria-expanded", "true");
            var f = initialFocus ? root.querySelector(initialFocus) : panel;
            (f || panel).focus();
        }

        function close() {
            if (!isOpen()) return;
            document.body.classList.remove(openClass);
            lockScroll(false);
            if (trigger && trigger.setAttribute) trigger.setAttribute("aria-expanded", "false");
            var t = trigger;
            // wait for the close transition before hiding from the a11y tree
            window.setTimeout(function () {
                root.hidden = true;
                if (t && t.focus) t.focus();
            }, opts.closeMs || 220);
        }

        // Escape + focus trap
        panel.addEventListener("keydown", function (e) {
            if (e.key === "Escape") { e.preventDefault(); close(); return; }
            if (e.key !== "Tab") return;
            var items = focusables(panel);
            if (!items.length) return;
            var first = items[0], last = items[items.length - 1];
            if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
            else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
        });

        return { open: open, close: close, isOpen: isOpen };
    }

    // ---- Search dialog ----
    var searchRoot = document.getElementById("ptgSearchDialog");
    var search = null;
    if (searchRoot) {
        search = makeOverlay({
            root: searchRoot,
            panel: searchRoot.querySelector(".ptg-search-panel"),
            openClass: "ptg-search-open",
            initialFocus: "[data-global-search]",
            closeMs: 180
        });
        Array.prototype.forEach.call(document.querySelectorAll("[data-search-open]"), function (b) {
            b.addEventListener("click", function () { search.open(b); });
        });
        Array.prototype.forEach.call(searchRoot.querySelectorAll("[data-search-close]"), function (b) {
            b.addEventListener("click", function () { search.close(); });
        });
    }

    // ---- Account drawer ----
    var accRoot = document.querySelector("[data-account-root]");
    var account = null;
    if (accRoot) {
        account = makeOverlay({
            root: accRoot,
            panel: accRoot.querySelector(".ptg-account-drawer"),
            openClass: "ptg-account-open",
            closeMs: 220
        });
        Array.prototype.forEach.call(document.querySelectorAll("[data-account-open]"), function (b) {
            b.addEventListener("click", function () { account.open(b); });
        });
        Array.prototype.forEach.call(accRoot.querySelectorAll("[data-account-close]"), function (b) {
            b.addEventListener("click", function () { account.close(); });
        });
    }

    // ---- Global Ctrl/Cmd+K → open search dialog ----
    document.addEventListener("keydown", function (e) {
        if ((e.ctrlKey || e.metaKey) && (e.key === "k" || e.key === "K")) {
            e.preventDefault();
            if (search) search.open(document.querySelector("[data-search-open]"));
        }
    });
})();
