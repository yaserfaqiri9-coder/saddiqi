// Header global search: live-filters the current page's primary list/table.
// Purely client-side and non-destructive — hides non-matching rows/cards only.
(function () {
    "use strict";

    var input = document.querySelector("[data-global-search]");
    if (!input) return;

    // Candidate row containers, in priority order. First group that has rows wins.
    function collectTargets() {
        var groups = [
            document.querySelectorAll(".ak-table tbody tr"),
            document.querySelectorAll(".ak-list .ak-list-row")
        ];
        for (var i = 0; i < groups.length; i++) {
            if (groups[i].length) return Array.prototype.slice.call(groups[i]);
        }
        return [];
    }

    function norm(s) {
        return (s || "")
            .toLowerCase()
            // normalise Persian/Arabic digits so a query in either script matches
            .replace(/[۰-۹]/g, function (d) { return d.charCodeAt(0) - 0x06f0; })
            .replace(/[٠-٩]/g, function (d) { return d.charCodeAt(0) - 0x0660; })
            .replace(/ي/g, "ی").replace(/ك/g, "ک"); // Arabic ya/kaf -> Persian
    }

    function apply() {
        var q = norm(input.value.trim());
        var rows = collectTargets();
        if (!rows.length) return;
        rows.forEach(function (row) {
            if (!q) { row.hidden = false; return; }
            row.hidden = norm(row.textContent).indexOf(q) === -1;
        });
    }

    var t;
    input.addEventListener("input", function () {
        clearTimeout(t);
        t = setTimeout(apply, 120);
    });
    // Ctrl/Cmd+K opening is handled in header-ui.js (the input lives in a dialog).
})();
