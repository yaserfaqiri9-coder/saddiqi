(function () {
    "use strict";

    function resolveTransportKind(select) {
        if (!select) {
            return "default";
        }

        var value = select.value || "";
        if (value === select.dataset.transportValueWagon) {
            return "wagon";
        }

        if (value === select.dataset.transportValueTruck) {
            return "truck";
        }

        if (value === select.dataset.transportValueVessel) {
            return "vessel";
        }

        return "default";
    }

    var labelDatasetKeys = {
        default: "transportLabelDefault",
        wagon: "transportLabelWagon",
        truck: "transportLabelTruck",
        vessel: "transportLabelVessel"
    };

    var placeholderDatasetKeys = {
        default: "transportPlaceholderDefault",
        wagon: "transportPlaceholderWagon",
        truck: "transportPlaceholderTruck",
        vessel: "transportPlaceholderVessel"
    };

    var listDatasetKeys = {
        default: "transportListDefault",
        wagon: "transportListWagon",
        truck: "transportListTruck",
        vessel: "transportListVessel"
    };

    function datasetValue(element, keys, kind) {
        if (!element) {
            return "";
        }

        return element.dataset[keys[kind]] || element.dataset[keys.default] || "";
    }

    function updateLabels(form) {
        var select = form.querySelector("[data-transport-type-select]");
        var kind = resolveTransportKind(select);

        form.querySelectorAll("[data-transport-dynamic-label]").forEach(function (label) {
            var text = datasetValue(label, labelDatasetKeys, kind);
            if (text) {
                label.textContent = text;
            }
        });

        form.querySelectorAll("[data-transport-dynamic-placeholder]").forEach(function (input) {
            var text = datasetValue(input, placeholderDatasetKeys, kind);
            if (text) {
                input.setAttribute("placeholder", text);
            } else {
                input.removeAttribute("placeholder");
            }
        });

        form.querySelectorAll("[data-transport-dynamic-list]").forEach(function (input) {
            var listId = datasetValue(input, listDatasetKeys, kind);
            if (listId) {
                input.setAttribute("list", listId);
            } else {
                input.removeAttribute("list");
            }
        });

        updateVesselOnlyFields(form, kind);
    }

    function updateVesselOnlyFields(form, kind) {
        var isVessel = kind === "vessel";
        form.querySelectorAll("[data-vessel-only-field]").forEach(function (group) {
            setConditionalGroupVisible(group, isVessel);
            if (!isVessel) {
                group.querySelectorAll("select, input").forEach(function (field) {
                    if (field.type !== "hidden") {
                        field.value = "";
                    }
                });
            }
        });
    }

    function initializeForm(form) {
        if (!form) {
            return;
        }

        var select = form.querySelector("[data-transport-type-select]");
        if (select) {
            if (form.dataset.inventoryTransportLegFormReady !== "true") {
                select.addEventListener("change", function () {
                    updateLabels(form);
                });
                form.dataset.inventoryTransportLegFormReady = "true";
            }

            updateLabels(form);
        }

        initializeAllocationRows(form);
        initializeContractStockFilters(form);
        initializeTransferSourceMetrics(form);
        initializeExclusiveFieldGroups(form);
        initializeDestinationMode(form);
        initializeTransportPartySelector(form);
        initializeTransportAdvancedDetails(form);
        initializeTransportSummary(form);
        initializeTransportReset(form);
    }

    function formatNumber(value, fractionDigits) {
        return new Intl.NumberFormat("en-US", {
            minimumFractionDigits: fractionDigits,
            maximumFractionDigits: fractionDigits
        }).format(value);
    }

    function parseDecimal(value) {
        if (value === null || value === undefined || value === "") {
            return 0;
        }

        var parsed = Number.parseFloat(String(value).replace(/,/g, ""));
        return Number.isFinite(parsed) ? parsed : 0;
    }

    function selectedText(form, selector) {
        var select = form.querySelector(selector);
        if (!(select instanceof HTMLSelectElement)) {
            return "";
        }

        var option = select.selectedOptions && select.selectedOptions.length > 0 ? select.selectedOptions[0] : null;
        return option && option.value ? option.textContent.trim() : "";
    }

    function controlHasValue(control) {
        if (!control) {
            return false;
        }

        if (control.type === "checkbox" || control.type === "radio") {
            return control.checked;
        }

        return String(control.value || "").trim() !== "";
    }

    function setConditionalGroupVisible(group, visible) {
        if (!group) {
            return;
        }

        group.hidden = !visible;
        group.classList.toggle("d-none", !visible);
        group.querySelectorAll("input, select, textarea").forEach(function (field) {
            if (field.type !== "hidden") {
                field.disabled = !visible;
            }
        });
    }

    function initializeExclusiveFieldGroups(form) {
        if (form.dataset.transportExclusiveGroupsReady === "true") {
            return;
        }

        var groups = {};
        form.querySelectorAll("[data-exclusive-field-group]").forEach(function (wrapper) {
            var name = wrapper.getAttribute("data-exclusive-field-group");
            if (!name) {
                return;
            }
            groups[name] = groups[name] || [];
            groups[name].push(wrapper);
        });

        Object.keys(groups).forEach(function (name) {
            var wrappers = groups[name];
            function activeRole() {
                var role = "";
                wrappers.forEach(function (wrapper) {
                    var field = wrapper.querySelector("select, input, textarea");
                    if (!role && controlHasValue(field)) {
                        role = wrapper.getAttribute("data-exclusive-role") || "";
                    }
                });
                return role;
            }

            function refresh() {
                var selectedRole = activeRole();
                wrappers.forEach(function (wrapper) {
                    var role = wrapper.getAttribute("data-exclusive-role") || "";
                    setConditionalGroupVisible(wrapper, !selectedRole || role === selectedRole);
                });
            }

            wrappers.forEach(function (wrapper) {
                wrapper.querySelectorAll("select, input, textarea").forEach(function (field) {
                    field.addEventListener("change", refresh);
                    field.addEventListener("input", refresh);
                });
            });
            refresh();
        });

        form.dataset.transportExclusiveGroupsReady = "true";
    }

    function initializeDestinationMode(form) {
        var select = form.querySelector("[data-transport-destination-mode]");
        if (!select || select.dataset.destinationModeReady === "true") {
            return;
        }

        var fields = form.querySelectorAll("[data-transport-destination-field]");
        function refresh() {
            var mode = select.value || "inventory";
            fields.forEach(function (wrapper) {
                setConditionalGroupVisible(wrapper, wrapper.getAttribute("data-transport-destination-field") === mode);
            });
        }

        select.addEventListener("change", refresh);
        refresh();
        select.dataset.destinationModeReady = "true";
    }

    function initializeTransportPartySelector(form) {
        var typeSelect = form.querySelector("[data-transport-party-type]");
        if (!typeSelect) {
            return;
        }

        var transportTypeSelect = form.querySelector("[data-transport-type-select]");
        var serviceProviderSelect = form.querySelector("[name='ServiceProviderId']");
        var operationalAssetSelect = form.querySelector("[name='OperationalAssetId']");

        function hasTransportType() {
            return resolveTransportKind(transportTypeSelect) !== "default";
        }

        function setPartyField(select, active, clearInactive) {
            if (!select) {
                return;
            }

            select.disabled = !active;
            if (!active && clearInactive) {
                select.selectedIndex = 0;
            }

            var wrapper = select.closest("[data-transport-party-field], .col-md-4, .col-md-3, .col-md-6, .transport-allocation-field");
            if (wrapper) {
                wrapper.hidden = !active;
                wrapper.classList.toggle("d-none", !active);
            }
        }

        function refresh(clearInactive) {
            var transportTypeSelected = hasTransportType();
            var value = typeSelect.value || "";

            typeSelect.disabled = !transportTypeSelected;
            if (!transportTypeSelected) {
                if (clearInactive) {
                    typeSelect.selectedIndex = 0;
                }

                setPartyField(serviceProviderSelect, false, true);
                setPartyField(operationalAssetSelect, false, true);
                return;
            }

            setPartyField(serviceProviderSelect, value === "service-provider", clearInactive);
            setPartyField(operationalAssetSelect, value === "operational-asset", clearInactive);
        }

        if (typeSelect.dataset.transportPartyReady !== "true") {
            typeSelect.addEventListener("change", function () {
                refresh(true);
            });

            if (transportTypeSelect) {
                transportTypeSelect.addEventListener("change", function () {
                    refresh(true);
                });
            }

            typeSelect.dataset.transportPartyReady = "true";
        }

        refresh(false);
    }

    function initializeTransportAdvancedDetails(form) {
        var toggleButton = form.querySelector("[data-transport-advanced-toggle]");
        var toggleText = form.querySelector("[data-transport-advanced-toggle-text]");
        var fields = Array.prototype.slice.call(form.querySelectorAll("[data-transport-advanced-field]"));

        if (!toggleButton || fields.length === 0) {
            return;
        }

        function setVisible(visible) {
            fields.forEach(function (field) {
                field.hidden = !visible;
                field.classList.toggle("d-none", !visible);
            });

            toggleButton.setAttribute("aria-expanded", visible ? "true" : "false");
            if (toggleText) {
                toggleText.textContent = visible
                    ? (toggleButton.dataset.transportAdvancedCloseText || toggleText.textContent)
                    : (toggleButton.dataset.transportAdvancedOpenText || toggleText.textContent);
            }
        }

        form.__setTransportAdvancedVisible = setVisible;

        if (toggleButton.dataset.transportAdvancedReady !== "true") {
            toggleButton.addEventListener("click", function () {
                setVisible(toggleButton.getAttribute("aria-expanded") !== "true");
            });
            toggleButton.dataset.transportAdvancedReady = "true";
        }

        setVisible(false);
    }

    function initializeTransportSummary(form) {
        var contractsSummary = form.querySelector("[data-transport-summary-contracts]");
        var quantitySummary = form.querySelector("[data-transport-summary-quantity]");
        var averageSummary = form.querySelector("[data-transport-summary-average]");
        var destinationSummary = form.querySelector("[data-transport-summary-destination]");
        var tableContracts = form.querySelector("[data-transport-table-contracts]");
        var tableQuantity = form.querySelector("[data-transport-table-quantity]");
        var tableAverage = form.querySelector("[data-transport-table-average]");
        var primaryTerminal = form.querySelector("[data-transport-primary-source-terminal]");
        var primaryProduct = form.querySelector("[data-transport-primary-product]");
        var destinationFallback = destinationSummary ? destinationSummary.textContent.trim() : "";
        var primaryTerminalFallback = primaryTerminal ? primaryTerminal.textContent.trim() : "";
        var primaryProductFallback = primaryProduct ? primaryProduct.textContent.trim() : "";
        var contractUnit = tableContracts ? (tableContracts.dataset.transportTableContractUnit || "contracts") : "contracts";

        function refresh() {
            var rows = Array.prototype.slice.call(form.querySelectorAll("[data-transport-allocation-row]"));
            var contractIds = new Set();
            var totalQuantity = 0;
            var weightedCostBase = 0;
            var weightedCostQty = 0;
            var firstTerminalText = "";
            var firstProductText = "";

            rows.forEach(function (row) {
                var contractSelect = row.querySelector("[name$='.SourcePurchaseContractId']");
                var quantityInput = row.querySelector("[data-transport-allocation-quantity]");
                var costInput = row.querySelector("[data-transport-allocation-cost]");
                var terminalSelect = row.querySelector("[name$='.SourceTerminalId']");
                var productSelect = row.querySelector("[name$='.ProductId']");
                var contractId = contractSelect instanceof HTMLSelectElement ? contractSelect.value : "";
                var quantity = parseDecimal(quantityInput instanceof HTMLInputElement ? quantityInput.value : "");
                var cost = parseDecimal(costInput instanceof HTMLInputElement ? costInput.value : "");

                if (contractId) {
                    contractIds.add(contractId);
                }

                totalQuantity += quantity;
                if (quantity > 0 && cost > 0) {
                    weightedCostBase += quantity * cost;
                    weightedCostQty += quantity;
                }

                if (!firstTerminalText && terminalSelect instanceof HTMLSelectElement && terminalSelect.value) {
                    firstTerminalText = terminalSelect.selectedOptions[0].textContent.trim();
                }

                if (!firstProductText && productSelect instanceof HTMLSelectElement && productSelect.value) {
                    firstProductText = productSelect.selectedOptions[0].textContent.trim();
                }
            });

            var averageCost = weightedCostQty > 0 ? weightedCostBase / weightedCostQty : 0;
            var destinationText =
                selectedText(form, "[name='DestinationTerminalId']") ||
                selectedText(form, "[name='DestinationLocationId']") ||
                selectedText(form, "[name='DestinationStorageTankId']") ||
                destinationFallback;

            if (contractsSummary) contractsSummary.textContent = String(contractIds.size);
            if (quantitySummary) quantitySummary.textContent = formatNumber(totalQuantity, 4) + " TON";
            if (averageSummary) averageSummary.textContent = "$ " + formatNumber(averageCost, 2);
            if (destinationSummary) destinationSummary.textContent = destinationText;
            if (tableContracts) tableContracts.textContent = contractIds.size + " " + contractUnit;
            if (tableQuantity) tableQuantity.textContent = formatNumber(totalQuantity, 4) + " TON";
            if (tableAverage) tableAverage.textContent = "$ " + formatNumber(averageCost, 2);
            if (primaryTerminal) primaryTerminal.textContent = firstTerminalText || primaryTerminalFallback;
            if (primaryProduct) primaryProduct.textContent = firstProductText || primaryProductFallback;
        }

        form.__refreshTransportSummary = refresh;

        if (form.dataset.transportSummaryReady !== "true") {
            form.addEventListener("input", refresh);
            form.addEventListener("change", refresh);
            form.addEventListener("click", function (event) {
                if (event.target.closest("[data-add-transport-allocation-row], [data-remove-transport-allocation-row]")) {
                    window.setTimeout(refresh, 0);
                }
            });
            form.dataset.transportSummaryReady = "true";
        }

        refresh();
    }

    function initializeTransportReset(form) {
        var resetButton = form.querySelector("[data-inventory-transport-reset]");
        if (!resetButton || resetButton.dataset.inventoryTransportResetReady === "true") {
            return;
        }

        resetButton.addEventListener("click", function () {
            window.setTimeout(function () {
                boot();
                if (typeof form.__setTransportAdvancedVisible === "function") {
                    form.__setTransportAdvancedVisible(false);
                }
                if (typeof form.__refreshTransportSummary === "function") {
                    form.__refreshTransportSummary();
                }
            }, 0);
        });

        resetButton.dataset.inventoryTransportResetReady = "true";
    }

    function normalizeStockScope(scope) {
        var contractId = Number(scope.contractId || scope.ContractId || 0);
        var terminalId = Number(scope.terminalId || scope.TerminalId || 0);
        var storageTankId = scope.storageTankId != null ? Number(scope.storageTankId) : Number(scope.StorageTankId || 0);
        var quantityMt = Number(scope.quantityMt || scope.QuantityMt || 0);

        return {
            contractId: contractId > 0 ? String(contractId) : "",
            terminalId: terminalId > 0 ? String(terminalId) : "",
            storageTankId: storageTankId > 0 ? String(storageTankId) : "",
            quantityMt: quantityMt
        };
    }

    function getContractStockScopes(form) {
        if (form.__contractStockScopes) {
            return form.__contractStockScopes;
        }

        var script = form.querySelector("[data-contract-stock-scopes]");
        var scopes = [];

        if (script) {
            try {
                var parsed = JSON.parse(script.textContent || "[]");
                if (Array.isArray(parsed)) {
                    scopes = parsed
                        .map(normalizeStockScope)
                        .filter(function (scope) {
                            return scope.contractId && scope.terminalId && scope.quantityMt > 0;
                        });
                }
            } catch (error) {
                scopes = [];
            }
        }

        form.__contractStockScopes = scopes;
        return scopes;
    }

    function getTransferSourceMetrics(form) {
        if (form.__transferSourceMetrics) {
            return form.__transferSourceMetrics;
        }

        var script = form.querySelector("[data-transfer-source-metrics]");
        var metrics = [];
        if (script) {
            try {
                var parsed = JSON.parse(script.textContent || "[]");
                if (Array.isArray(parsed)) {
                    metrics = parsed.map(function (source) {
                        return {
                            terminalId: String(source.terminalId || ""),
                            storageTankId: source.storageTankId ? String(source.storageTankId) : "",
                            inbound: Number(source.inbound || 0),
                            outbound: Number(source.outbound || 0),
                            available: Number(source.available || 0)
                        };
                    });
                }
            } catch (error) {
                metrics = [];
            }
        }

        form.__transferSourceMetrics = metrics;
        return metrics;
    }

    function initializeTransferSourceMetrics(form) {
        var metrics = getTransferSourceMetrics(form);
        if (metrics.length === 0 || form.dataset.transferSourceMetricsReady === "true") {
            return;
        }

        function setQuantity(selector, value) {
            var node = form.querySelector(selector);
            if (node) {
                node.textContent = formatNumber(value, 4) + " MT";
            }
        }

        function refresh() {
            var row = form.querySelector("[data-transport-allocation-row]");
            var terminal = row && row.querySelector("[name$='.SourceTerminalId']");
            var tank = row && row.querySelector("[name$='.SourceStorageTankId']");
            var terminalId = terminal instanceof HTMLSelectElement ? terminal.value : "";
            var tankId = tank instanceof HTMLSelectElement ? tank.value : "";
            var source = metrics.find(function (item) {
                return item.terminalId === terminalId && item.storageTankId === tankId;
            });
            if (!source) {
                return;
            }

            setQuantity("[data-transfer-source-inbound]", source.inbound);
            setQuantity("[data-transfer-source-outbound]", source.outbound);
            setQuantity("[data-transfer-source-available]", source.available);
            setQuantity("[data-transfer-summary-available]", source.available);
        }

        form.addEventListener("change", function (event) {
            if (event.target.matches("[name$='.SourceTerminalId'], [name$='.SourceStorageTankId']")) {
                refresh();
            }
        });
        form.addEventListener("click", function (event) {
            if (event.target.closest("[data-add-transport-allocation-row], [data-remove-transport-allocation-row]")) {
                window.setTimeout(refresh, 0);
            }
        });
        form.dataset.transferSourceMetricsReady = "true";
        refresh();
    }

    function applyScopedOptions(select, allowedValues, shouldFilter) {
        if (!select) {
            return;
        }

        var selectedValue = select.value || "";
        var selectedStillAllowed = !selectedValue;
        var activeFilter = shouldFilter && allowedValues;

        Array.prototype.forEach.call(select.options, function (option) {
            var value = option.value || "";
            var visible = !value || !activeFilter || allowedValues.has(value);

            option.hidden = !visible;
            option.disabled = !visible;
            option.style.display = visible ? "" : "none";

            if (value && value === selectedValue && visible) {
                selectedStillAllowed = true;
            }
        });

        if (!selectedStillAllowed) {
            select.value = "";
            select.dispatchEvent(new Event("change", { bubbles: true }));
        }
    }

    function refreshAllocationStockNotice(row, scopes) {
        var notice = row.querySelector("[data-transport-stock-notice]");
        var contractSelect = row.querySelector("[name$='.SourcePurchaseContractId']");
        var terminalSelect = row.querySelector("[name$='.SourceTerminalId']");
        var tankSelect = row.querySelector("[name$='.SourceStorageTankId']");

        if (!notice || !contractSelect || !tankSelect) {
            return;
        }

        var contractId = contractSelect.value || "";
        var terminalId = terminalSelect instanceof HTMLSelectElement ? (terminalSelect.value || "") : "";
        var tankId = tankSelect.value || "";
        var label = notice.dataset.stockLabel || "Current stock";
        var unit = notice.dataset.stockUnit || "MT";

        if (!contractId || !tankId) {
            notice.textContent = "";
            notice.hidden = true;
            notice.classList.remove("is-active");
            return;
        }

        var matchingScopes = scopes.filter(function (scope) {
            return scope.contractId === contractId
                && scope.storageTankId === tankId
                && (!terminalId || scope.terminalId === terminalId);
        });
        var quantityMt = matchingScopes.reduce(function (sum, scope) {
            return sum + (Number(scope.quantityMt) || 0);
        }, 0);

        notice.replaceChildren();

        var labelNode = document.createElement("span");
        labelNode.className = "transport-stock-label";
        labelNode.textContent = label;

        var valueNode = document.createElement("strong");
        valueNode.className = "transport-stock-value";
        valueNode.textContent = formatNumber(quantityMt, 4);

        var unitNode = document.createElement("span");
        unitNode.className = "transport-stock-unit";
        unitNode.textContent = unit;

        notice.append(labelNode, valueNode, unitNode);
        notice.hidden = false;
        notice.classList.add("is-active");
    }

    function refreshAllocationStockFilter(row, scopes) {
        var contractSelect = row.querySelector("[name$='.SourcePurchaseContractId']");
        var terminalSelect = row.querySelector("[name$='.SourceTerminalId']");
        var tankSelect = row.querySelector("[name$='.SourceStorageTankId']");

        if (!contractSelect || !terminalSelect || !tankSelect) {
            return;
        }

        var contractId = contractSelect.value || "";
        var contractScopes = contractId
            ? scopes.filter(function (scope) { return scope.contractId === contractId; })
            : [];

        var terminalIds = new Set(contractScopes.map(function (scope) { return scope.terminalId; }));
        applyScopedOptions(terminalSelect, terminalIds, Boolean(contractId));

        var activeTerminalId = terminalSelect.value || "";
        var tankScopes = contractScopes.filter(function (scope) {
            return scope.storageTankId && (!activeTerminalId || scope.terminalId === activeTerminalId);
        });
        var tankIds = new Set(tankScopes.map(function (scope) { return scope.storageTankId; }));
        applyScopedOptions(tankSelect, tankIds, Boolean(contractId));
        refreshAllocationStockNotice(row, scopes);
    }

    function initializeContractStockFilters(form) {
        var scopes = getContractStockScopes(form);

        form.querySelectorAll("[data-transport-allocation-row]").forEach(function (row) {
            var contractSelect = row.querySelector("[name$='.SourcePurchaseContractId']");
            var terminalSelect = row.querySelector("[name$='.SourceTerminalId']");
            var tankSelect = row.querySelector("[name$='.SourceStorageTankId']");

            if (row.dataset.contractStockFilterReady !== "true") {
                if (contractSelect) {
                    contractSelect.addEventListener("change", function () {
                        refreshAllocationStockFilter(row, scopes);
                    });
                }

                if (terminalSelect) {
                    terminalSelect.addEventListener("change", function () {
                        refreshAllocationStockFilter(row, scopes);
                    });
                }

                if (tankSelect) {
                    tankSelect.addEventListener("change", function () {
                        refreshAllocationStockNotice(row, scopes);
                    });
                }

                row.dataset.contractStockFilterReady = "true";
            }

            refreshAllocationStockFilter(row, scopes);
        });
    }

    function updateIndexedAttribute(element, attributeName, index) {
        var value = element.getAttribute(attributeName);
        if (!value) {
            return;
        }

        element.setAttribute(attributeName, value.replace(/Allocations\[\d+\]/g, "Allocations[" + index + "]"));
    }

    function clearInputValue(input) {
        if (input.tagName === "SELECT") {
            input.selectedIndex = 0;
            return;
        }

        input.value = "";
    }

    function renumberAllocationRows(form) {
        form.querySelectorAll("[data-transport-allocation-row]").forEach(function (row, index) {
            row.dataset.allocationIndex = String(index);

            var numberCell = row.querySelector("[data-allocation-row-number]");
            if (numberCell) {
                numberCell.textContent = String(index + 1);
            }

            row.querySelectorAll("[name]").forEach(function (field) {
                updateIndexedAttribute(field, "name", index);
            });

            row.querySelectorAll("[id]").forEach(function (field) {
                field.setAttribute("id", field.getAttribute("id").replace(/Allocations_\d+__/g, "Allocations_" + index + "__"));
            });

            row.querySelectorAll("[data-valmsg-for]").forEach(function (message) {
                updateIndexedAttribute(message, "data-valmsg-for", index);
            });
        });
    }

    function addAllocationRow(form) {
        var tbody = form.querySelector("[data-transport-allocation-rows]");
        var rows = tbody ? tbody.querySelectorAll("[data-transport-allocation-row]") : [];
        if (!tbody || rows.length === 0) {
            return;
        }

        var clone = rows[rows.length - 1].cloneNode(true);
        delete clone.dataset.contractStockFilterReady;
        clone.querySelectorAll("input, select, textarea").forEach(clearInputValue);
        clone.querySelectorAll(".field-validation-error").forEach(function (message) {
            message.textContent = "";
        });
        tbody.appendChild(clone);
        renumberAllocationRows(form);
        initializeContractStockFilters(form);
    }

    function removeAllocationRow(form, button) {
        var rows = form.querySelectorAll("[data-transport-allocation-row]");
        var row = button.closest("[data-transport-allocation-row]");
        if (!row) {
            return;
        }

        if (rows.length <= 1) {
            row.querySelectorAll("input, select, textarea").forEach(clearInputValue);
        } else {
            row.remove();
        }

        renumberAllocationRows(form);
        initializeContractStockFilters(form);
    }

    function initializeAllocationRows(form) {
        if (form.dataset.transportAllocationRowsReady === "true") {
            return;
        }

        var addButton = form.querySelector("[data-add-transport-allocation-row]");
        if (addButton) {
            addButton.addEventListener("click", function () {
                addAllocationRow(form);
            });
        }

        form.addEventListener("click", function (event) {
            var removeButton = event.target.closest("[data-remove-transport-allocation-row]");
            if (removeButton && form.contains(removeButton)) {
                removeAllocationRow(form, removeButton);
            }
        });

        renumberAllocationRows(form);
        form.dataset.transportAllocationRowsReady = "true";
    }

    function boot() {
        document.querySelectorAll("[data-inventory-transport-leg-form]").forEach(initializeForm);
    }

    window.__ptgInitInventoryTransportLegForms = boot;

    if (window.__ptgInventoryTransportLegFormReady !== true) {
        window.addEventListener("ptg:page-ready", boot);
        window.__ptgInventoryTransportLegFormReady = true;
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot, { once: true });
    } else {
        boot();
    }
})();
