(function () {
    "use strict";

    function num(value, digits) {
        var n = Number.parseFloat(value);
        if (!Number.isFinite(n)) return "";
        return n.toLocaleString("en-US", { minimumFractionDigits: 0, maximumFractionDigits: digits == null ? 2 : digits });
    }

    function escapeHtml(value) {
        return String(value == null ? "" : value)
            .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;").replace(/'/g, "&#039;");
    }

    function initialize(form) {
        if (form.dataset.ready === "true") return;
        form.dataset.ready = "true";

        var fileInput = form.querySelector('input[name="file"]');
        var previewBtn = form.querySelector("[data-preview-btn]");
        var saveBtn = form.querySelector("[data-save-btn]");
        var loading = form.querySelector("[data-import-loading]");
        var loadingText = form.querySelector("[data-import-loading-text]");
        var errorBox = form.querySelector("[data-import-error]");

        var previewCard = document.querySelector("[data-preview-card]");
        var previewRows = document.querySelector("[data-preview-rows]");
        var previewSaved = document.querySelector("[data-preview-saved]");
        var previewSkipped = document.querySelector("[data-preview-skipped]");

        var resultCard = document.querySelector("[data-result-card]");
        var resultRows = document.querySelector("[data-result-rows]");
        var resultSaved = document.querySelector("[data-result-saved]");
        var resultSkipped = document.querySelector("[data-result-skipped]");

        var previewUrl = form.getAttribute("data-preview-url");
        var saveUrl = form.getAttribute("data-save-url") || form.action;
        var detailsUrl = form.getAttribute("data-details-url") || "";
        var savedDone = false;

        function showLoading(text) {
            if (loadingText) loadingText.textContent = text;
            if (loading) { loading.classList.remove("d-none"); loading.classList.add("d-flex"); }
        }
        function hideLoading() {
            if (loading) { loading.classList.add("d-none"); loading.classList.remove("d-flex"); }
        }
        function showError(msg) {
            if (!errorBox) return;
            errorBox.textContent = msg || "";
            errorBox.classList.toggle("d-none", !msg);
        }
        function resetAll() {
            if (savedDone) return; // بعد از ثبت موفق، تا تغییر ورودی چیزی را پاک نکن
            if (previewCard) previewCard.classList.add("d-none");
            if (resultCard) resultCard.classList.add("d-none");
            if (saveBtn) saveBtn.disabled = true;
            showError("");
        }

        function rowCells(r, statusHtml) {
            return "<tr>" +
                "<td>" + escapeHtml(r.rowNumber) + "</td>" +
                "<td>" + escapeHtml(r.simir) + "</td>" +
                "<td>" + escapeHtml(r.plate) + "</td>" +
                "<td>" + (r.weight != null ? escapeHtml(num(r.weight, 3)) : "") + "</td>" +
                "<td>" + escapeHtml(num(r.afn, 2)) + "</td>" +
                "<td>" + (Number(r.usd) > 0 ? escapeHtml(num(r.usd, 2)) : "—") + "</td>" +
                "<td>" + statusHtml + "</td>" +
                "</tr>";
        }

        function renderPreview(data) {
            if (previewSaved) previewSaved.textContent = data.savedCount;
            if (previewSkipped) previewSkipped.textContent = data.skippedCount;
            var html = (data.rows || []).map(function (r) {
                var status = r.matched
                    ? '<span class="badge bg-success">ثبت می‌شود</span>' + (r.label ? ' <span class="ms-1 small">' + escapeHtml(r.label) + '</span>' : '')
                    : '<span class="badge bg-danger">رد می‌شود</span> <span class="ms-1 small text-muted">' + escapeHtml(r.skip) + '</span>';
                return rowCells(r, status);
            }).join("");
            if (previewRows) previewRows.innerHTML = html;
            if (previewCard) previewCard.classList.remove("d-none");
            if (resultCard) resultCard.classList.add("d-none");
            if (saveBtn) saveBtn.disabled = !(data.savedCount > 0);
            if (data.savedCount <= 0) showError("هیچ ردیفی با عملیاتِ در جریان تطبیق نشد؛ چیزی برای ثبت وجود ندارد.");
        }

        function renderResult(data) {
            if (resultSaved) resultSaved.textContent = data.savedCount;
            if (resultSkipped) resultSkipped.textContent = data.skippedCount;
            var html = (data.rows || []).map(function (r) {
                var status;
                if (r.matched) {
                    var label = r.label ? escapeHtml(r.label) : "ثبت شد";
                    var link = (r.declarationId && detailsUrl)
                        ? '<a class="ms-1 small" href="' + detailsUrl + "/" + r.declarationId + '">' + label + '</a>'
                        : ' <span class="ms-1 small">' + label + '</span>';
                    status = '<span class="badge bg-success">ثبت شد</span>' + link;
                } else {
                    status = '<span class="badge bg-danger">رد شد</span> <span class="ms-1 small text-muted">' + escapeHtml(r.skip) + '</span>';
                }
                return rowCells(r, status);
            }).join("");
            if (resultRows) resultRows.innerHTML = html;
            if (resultCard) resultCard.classList.remove("d-none");
            if (previewCard) previewCard.classList.add("d-none");
        }

        function hasFile() {
            return fileInput && fileInput.files && fileInput.files.length > 0;
        }

        async function post(url) {
            var response = await fetch(url, {
                method: "POST",
                body: new FormData(form),
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            return response.json();
        }

        async function runPreview() {
            showError("");
            if (!hasFile()) { showError("اول فایل اکسل را انتخاب کنید."); return; }
            if (!previewUrl) return;
            if (previewBtn) previewBtn.disabled = true;
            showLoading("در حال خواندن و تطبیق فایل… لطفاً صبر کنید");
            try {
                var data = await post(previewUrl);
                if (!data.ok) { resetAll(); showError(data.message || "پیش‌نمایش ناموفق بود."); }
                else renderPreview(data);
            } catch (_) {
                resetAll(); showError("خطا در ارتباط با سرور هنگام پیش‌نمایش.");
            } finally {
                hideLoading();
                if (previewBtn) previewBtn.disabled = false;
            }
        }

        async function runSave() {
            if (savedDone) return;
            showError("");
            if (!hasFile()) { showError("فایل انتخاب نشده است؛ دوباره پیش‌نمایش بگیرید."); return; }
            if (saveBtn) saveBtn.disabled = true;
            if (previewBtn) previewBtn.disabled = true;
            showLoading("در حال ثبت… لطفاً صبر کنید");
            try {
                var data = await post(saveUrl);
                if (!data.ok) {
                    showError(data.message || "ثبت ناموفق بود.");
                    if (saveBtn) saveBtn.disabled = false;
                } else {
                    savedDone = true;
                    renderResult(data);
                }
            } catch (_) {
                showError("خطا در ارتباط با سرور هنگام ثبت. اگر تکرار شد، برنامه را ری‌استارت کنید.");
                if (saveBtn) saveBtn.disabled = false;
            } finally {
                hideLoading();
                if (previewBtn) previewBtn.disabled = false;
            }
        }

        if (previewBtn) previewBtn.addEventListener("click", runPreview);
        if (saveBtn) saveBtn.addEventListener("click", runSave);

        // فرم دیگر ارسال کامل صفحه نمی‌کند؛ همه‌چیز از طریق دکمه‌های AJAX است.
        form.addEventListener("submit", function (event) { event.preventDefault(); });

        // تغییر فایل/حوزه/نرخ ⇒ پیش‌نمایش و نتیجه باطل، اجازهٔ ثبت مجدد.
        form.addEventListener("change", function (event) {
            if (event.target.matches('input[name="file"], input[name="Scope"], input[name="FxRateToUsd"]')) {
                savedDone = false;
                resetAll();
            }
        });
    }

    function boot() {
        document.querySelectorAll("[data-customs-import]").forEach(initialize);
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", boot, { once: true });
    else boot();
})();
