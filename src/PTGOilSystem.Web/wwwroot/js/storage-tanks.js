(function () {
    "use strict";

    function initContractMovementPagers(root) {
        var scope = root || document;
        scope.querySelectorAll("[data-st-modal-pager]").forEach(function (pager) {
            if (pager.dataset.ready === "true") return;

            var modal = pager.closest("[data-storage-contract-modal]");
            if (!modal) return;

            var rows = Array.prototype.slice.call(modal.querySelectorAll("[data-st-modal-row]"));
            var pageSize = parseInt(pager.dataset.pageSize || "7", 10);
            if (!rows.length || !pageSize || pageSize < 1) return;

            var totalPages = Math.max(1, Math.ceil(rows.length / pageSize));
            var currentPage = 1;
            var prev = pager.querySelector("[data-st-modal-prev]");
            var next = pager.querySelector("[data-st-modal-next]");
            var status = pager.querySelector("[data-st-modal-status]");
            var pageButtons = Array.prototype.slice.call(pager.querySelectorAll("[data-st-modal-page]"));
            var labelPage = pager.dataset.labelPage || "Page";
            var labelOf = pager.dataset.labelOf || "of";

            function render() {
                var start = (currentPage - 1) * pageSize;
                var end = start + pageSize;

                rows.forEach(function (row, index) {
                    row.hidden = index < start || index >= end;
                });

                if (prev) prev.disabled = currentPage === 1;
                if (next) next.disabled = currentPage === totalPages;
                if (status) status.textContent = labelPage + " " + currentPage + " " + labelOf + " " + totalPages;

                pageButtons.forEach(function (button) {
                    var page = parseInt(button.dataset.stModalPage || "1", 10);
                    var isActive = page === currentPage;
                    var isVisible = totalPages <= 5 || page === 1 || page === totalPages || Math.abs(page - currentPage) <= 1;

                    button.hidden = !isVisible;
                    button.classList.toggle("is-active", isActive);
                    if (isActive) {
                        button.setAttribute("aria-current", "page");
                    } else {
                        button.removeAttribute("aria-current");
                    }
                });
            }

            if (prev) {
                prev.addEventListener("click", function () {
                    if (currentPage > 1) {
                        currentPage -= 1;
                        render();
                    }
                });
            }

            if (next) {
                next.addEventListener("click", function () {
                    if (currentPage < totalPages) {
                        currentPage += 1;
                        render();
                    }
                });
            }

            pageButtons.forEach(function (button) {
                button.addEventListener("click", function () {
                    var page = parseInt(button.dataset.stModalPage || "1", 10);
                    if (page >= 1 && page <= totalPages) {
                        currentPage = page;
                        render();
                    }
                });
            });

            modal.addEventListener("shown.bs.modal", render);
            pager.dataset.ready = "true";
            render();
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", function () {
            initContractMovementPagers(document);
        }, { once: true });
    } else {
        initContractMovementPagers(document);
    }

    window.addEventListener("ptg:page-ready", function (event) {
        initContractMovementPagers(event.detail && event.detail.root ? event.detail.root : document);
    });
})();
