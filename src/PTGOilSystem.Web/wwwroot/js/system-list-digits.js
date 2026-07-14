/*
 * PTG system list digits.
 * Display-only normalizer: keeps list numbers readable with English digits.
 */

(function () {
    "use strict";

    var DIGIT_MAP = {
        "۰": "0", "۱": "1", "۲": "2", "۳": "3", "۴": "4",
        "۵": "5", "۶": "6", "۷": "7", "۸": "8", "۹": "9",
        "٠": "0", "١": "1", "٢": "2", "٣": "3", "٤": "4",
        "٥": "5", "٦": "6", "٧": "7", "٨": "8", "٩": "9",
        "٬": ",", "٫": "."
    };

    var LIST_SELECTOR = [
        ".ak-table-wrap",
        ".table-responsive",
        ".ak-list",
        "table.ak-table"
    ].join(",");

    var SKIP_SELECTOR = [
        "input",
        "textarea",
        "select",
        "option",
        "script",
        "style",
        "code",
        "pre",
        "[contenteditable='true']"
    ].join(",");

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init, { once: true });
    } else {
        init();
    }

    function init() {
        normalizeSystemListDigits(document);
        observeListChanges();
    }

    function normalizeDigits(value) {
        return String(value || "").replace(/[۰-۹٠-٩٬٫]/g, function (char) {
            return DIGIT_MAP[char] || char;
        });
    }

    function normalizeSystemListDigits(root) {
        var scope = root && root.querySelectorAll ? root : document;
        var parentList = scope.closest ? scope.closest(LIST_SELECTOR) : null;

        if (scope.matches && scope.matches(LIST_SELECTOR)) {
            normalizeElement(scope);
        }

        if (parentList) {
            normalizeElement(parentList);
        }

        scope.querySelectorAll(LIST_SELECTOR).forEach(normalizeElement);
    }

    function normalizeElement(element) {
        if (!element) return;

        normalizeAttribute(element, "data-column-label");

        element.querySelectorAll("[data-column-label]").forEach(function (node) {
            normalizeAttribute(node, "data-column-label");
        });

        walkTextNodes(element);
    }

    function normalizeAttribute(element, name) {
        var current = element.getAttribute(name);
        var normalized;

        if (!current) return;

        normalized = normalizeDigits(current);
        if (normalized !== current) element.setAttribute(name, normalized);
    }

    function walkTextNodes(root) {
        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
            acceptNode: function (node) {
                if (!node.nodeValue || !/[۰-۹٠-٩٬٫]/.test(node.nodeValue)) return NodeFilter.FILTER_REJECT;
                if (node.parentElement && node.parentElement.closest(SKIP_SELECTOR)) return NodeFilter.FILTER_REJECT;
                return NodeFilter.FILTER_ACCEPT;
            }
        });

        var nodes = [];
        var node;

        while ((node = walker.nextNode())) nodes.push(node);

        nodes.forEach(function (textNode) {
            textNode.nodeValue = normalizeDigits(textNode.nodeValue);
        });
    }

    function observeListChanges() {
        if (!window.MutationObserver) return;

        var observer = new MutationObserver(function (mutations) {
            mutations.forEach(function (mutation) {
                mutation.addedNodes.forEach(function (node) {
                    if (node.nodeType !== 1) return;
                    normalizeSystemListDigits(node);
                });
            });
        });

        observer.observe(document.body, { childList: true, subtree: true });
    }

    window.PTG = window.PTG || {};
    window.PTG.normalizeSystemListDigits = normalizeSystemListDigits;
    window.PTG.normalizeDigits = normalizeDigits;
})();
