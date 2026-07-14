(function () {
    "use strict";

    function clearInactiveInputs(field) {
        field.querySelectorAll("input, select, textarea").forEach(function (input) {
            input.value = "";
        });
    }

    function resolveActiveOwnerField(ownerType) {
        var value = ownerType ? ownerType.value || "" : "";
        var selectedText = ownerType && ownerType.selectedOptions && ownerType.selectedOptions.length > 0
            ? (ownerType.selectedOptions[0].textContent || "").trim().toLowerCase()
            : "";

        if (value === "1" || selectedText.includes("company")) {
            return "company";
        }

        if (value === "2" || selectedText.includes("partner")) {
            return "partner";
        }

        return "other";
    }

    function initializeOwnerForm(form) {
        if (!form) {
            return;
        }

        var ownerType = form.querySelector("[data-oa-owner-type]");
        var fields = Array.prototype.slice.call(form.querySelectorAll("[data-oa-owner-field]"));

        function syncOwnerFields() {
            var activeField = resolveActiveOwnerField(ownerType);

            fields.forEach(function (field) {
                var isActive = field.dataset.oaOwnerField === activeField;
                field.hidden = !isActive;
                field.classList.toggle("is-hidden", !isActive);

                if (!isActive) {
                    clearInactiveInputs(field);
                }
            });
        }

        if (ownerType && form.dataset.oaOwnerFormReady !== "true") {
            ownerType.addEventListener("change", syncOwnerFields);
            form.dataset.oaOwnerFormReady = "true";
        }

        syncOwnerFields();
    }

    function activateProfitTabFromQuery() {
        var tab = new URLSearchParams(window.location.search).get("tab");
        if (tab !== "profit") {
            return;
        }

        if (window.ptgTabs) {
            window.ptgTabs.show("asset-profit");
        }
    }

    function boot(event) {
        var root = event && event.detail && event.detail.root ? event.detail.root : document;
        root.querySelectorAll("[data-oa-owner-form]").forEach(initializeOwnerForm);
        activateProfitTabFromQuery();
    }

    window.__ptgInitOperationalAssetDetails = boot;

    if (window.__ptgOperationalAssetDetailsReady !== true) {
        window.addEventListener("ptg:page-ready", boot);
        window.__ptgOperationalAssetDetailsReady = true;
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot, { once: true });
    } else {
        boot();
    }
})();
