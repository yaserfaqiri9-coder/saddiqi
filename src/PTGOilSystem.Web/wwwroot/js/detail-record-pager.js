(function () {
    "use strict";

    var pageSize = 5;

    function buildPager(card, rows) {
        var totalPages = Math.ceil(rows.length / pageSize);
        if (totalPages <= 1 || card.dataset.recordPagerReady === "true") return;

        card.dataset.recordPagerReady = "true";
        var currentPage = 1;

        var pager = document.createElement("nav");
        pager.className = "loading-detail-record-pager";
        pager.setAttribute("aria-label", "صفحه بندی لیست");

        var prev = document.createElement("button");
        prev.type = "button";
        prev.className = "btn btn-sm btn-outline-secondary";
        prev.textContent = "قبلی";

        var status = document.createElement("span");
        status.className = "loading-detail-record-pager-status";

        var next = document.createElement("button");
        next.type = "button";
        next.className = "btn btn-sm btn-outline-secondary";
        next.textContent = "بعدی";

        pager.appendChild(prev);
        pager.appendChild(status);
        pager.appendChild(next);

        function render() {
            var start = (currentPage - 1) * pageSize;
            var end = start + pageSize;

            rows.forEach(function (row, index) {
                row.hidden = index < start || index >= end;
            });

            prev.disabled = currentPage === 1;
            next.disabled = currentPage === totalPages;
            status.textContent = "صفحه " + currentPage + " از " + totalPages;
        }

        prev.addEventListener("click", function () {
            if (currentPage > 1) {
                currentPage -= 1;
                render();
            }
        });

        next.addEventListener("click", function () {
            if (currentPage < totalPages) {
                currentPage += 1;
                render();
            }
        });

        card.appendChild(pager);
        render();
    }

    function initDetailRecordPagers(root) {
        var scope = root || document;
        var pages = scope.querySelectorAll(".loading-details-simple-page");

        pages.forEach(function (page) {
            page.querySelectorAll(".loading-detail-record-card").forEach(function (card) {
                var rows = Array.prototype.slice.call(card.querySelectorAll(".loading-detail-record-list > .loading-detail-record-row"));
                buildPager(card, rows);
            });
        });
    }

    function boot() {
        initDetailRecordPagers(document);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot, { once: true });
    } else {
        boot();
    }

    window.addEventListener("ptg:page-ready", function (event) {
        initDetailRecordPagers(event.detail && event.detail.root ? event.detail.root : document);
    });
})();
