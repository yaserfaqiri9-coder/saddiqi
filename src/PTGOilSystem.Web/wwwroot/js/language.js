/*
 * PTG Oil System - Language Module
 * Handles UI language switching (Persian/English)
 */

(function () {
    "use strict";

    // Auto-initialize
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init, { once: true });
    } else {
        init();
    }

    function init() {
        initializeLanguageSwitcher();
    }

    function initializeLanguageSwitcher() {
        var language = resolveUiLanguage();
        applyUiLanguage(language);

        document.querySelectorAll("[data-language-option]").forEach(function (button) {
            if (button.dataset.languageReady === "true") return;

            button.addEventListener("click", function () {
                var nextLanguage = button.getAttribute("data-language-option") === "en" ? "en" : "fa";
                if (nextLanguage === resolveUiLanguage()) return;

                persistUiLanguage(nextLanguage);
                window.location.reload();
            });
            button.dataset.languageReady = "true";
        });
    }

    function resolveUiLanguage() {
        var bodyLanguage = document.body ? document.body.getAttribute("data-ui-language") : "";
        var storedLanguage = "";
        try {
            storedLanguage = window.localStorage.getItem("ptg-ui-lang") || "";
        } catch (error) {
            storedLanguage = "";
        }
        return (bodyLanguage || storedLanguage) === "en" ? "en" : "fa";
    }

    function persistUiLanguage(language) {
        var safeLanguage = language === "en" ? "en" : "fa";
        try {
            window.localStorage.setItem("ptg-ui-lang", safeLanguage);
        } catch (error) {}
        document.cookie = "ptg-ui-lang=" + safeLanguage + "; path=/; max-age=31536000; samesite=lax";
    }

    function applyUiLanguage(language) {
        var isEnglish = language === "en";
        var direction = isEnglish ? "ltr" : "rtl";

        document.documentElement.setAttribute("lang", isEnglish ? "en" : "fa");
        document.documentElement.setAttribute("dir", direction);

        if (document.body) {
            document.body.setAttribute("data-ui-language", language);
            document.body.classList.toggle("boltz-ltr", isEnglish);
            document.body.classList.toggle("boltz-rtl", !isEnglish);
        }

        document.querySelectorAll("[data-current-language-label]").forEach(function (label) {
            label.textContent = isEnglish ? "EN" : "FA";
        });

        document.querySelectorAll("[data-language-option]").forEach(function (button) {
            var isCurrent = button.getAttribute("data-language-option") === language;
            button.classList.toggle("active", isCurrent);
            button.setAttribute("aria-pressed", String(isCurrent));
        });

        if (isEnglish) {
            translateStaticContentToEnglish();
            normalizeEnglishUiMicrocopy();
        }
    }

    function translateStaticContentToEnglish() {
        var textTranslations = getEnglishTextTranslations();
        var attributeTranslations = getEnglishAttributeTranslations();

        var walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, {
            acceptNode: function (node) {
                var parent = node.parentElement;
                if (!parent || parent.closest("script, style, textarea, input, select, option")) {
                    return NodeFilter.FILTER_REJECT;
                }
                return NodeFilter.FILTER_ACCEPT;
            }
        });

        var textNodes = [];
        var node;
        while ((node = walker.nextNode())) {
            textNodes.push(node);
        }

        textNodes.forEach(function (textNode) {
            var original = textNode.nodeValue || "";
            var trimmed = original.replace(/\s+/g, " ").trim();
            var translated = textTranslations[trimmed];
            if (translated) {
                textNode.nodeValue = original.replace(trimmed, translated);
            }
        });

        ["placeholder", "aria-label", "title"].forEach(function (attribute) {
            document.querySelectorAll("[" + attribute + "]").forEach(function (element) {
                var original = element.getAttribute(attribute) || "";
                var translated = attributeTranslations[original.trim()] || textTranslations[original.trim()];
                if (translated) {
                    element.setAttribute(attribute, translated);
                }
            });
        });
    }

    function normalizeEnglishUiMicrocopy() {
        var textTranslations = {
            "جستجو": "Search",
            "جست‌وجو": "Search",
            "اعمال": "Apply",
            "ذخیره": "Save",
            "انصراف": "Cancel",
            "بازگشت": "Back"
        };

        var root = document.querySelector(".boltz-content") || document.body;
        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
            acceptNode: function (node) {
                var parent = node.parentElement;
                if (!parent || parent.closest("script, style, textarea, input, select")) {
                    return NodeFilter.FILTER_REJECT;
                }
                return NodeFilter.FILTER_ACCEPT;
            }
        });

        var textNode;
        while ((textNode = walker.nextNode())) {
            var originalText = textNode.nodeValue || "";
            var normalizedText = originalText.replace(/\s+/g, " ").trim();
            var translatedText = textTranslations[normalizedText];
            if (translatedText) {
                textNode.nodeValue = originalText.replace(normalizedText, translatedText);
            }
        }
    }

    function getEnglishAttributeTranslations() {
        return {
            "باز کردن منوی کناری": "Open sidebar",
            "بستن منوی کناری": "Close sidebar",
            "جست‌وجوی سریع": "Quick search",
            "پیام‌ها": "Messages",
            "اعلان‌ها": "Notifications"
        };
    }

    function getEnglishTextTranslations() {
        return {
            "فارسی": "Persian",
            "خروج": "Logout",
            "تغییر رمز عبور": "Change Password",
            "داشبورد": "Dashboard",
            "قراردادها": "Contracts",
            "گزارش‌ها": "Reports",
            "مدیریت": "Management",
            "ذخیره": "Save",
            "انصراف": "Cancel",
            "جستجو": "Search",
            "اعمال": "Apply"
        };
    }

    // Expose to global scope for SPA navigation
    window.__ptgApplyLanguage = function () {
        applyUiLanguage(resolveUiLanguage());
    };

    window.PTG = window.PTG || {};
    window.PTG.resolveUiLanguage = resolveUiLanguage;
    window.PTG.applyUiLanguage = applyUiLanguage;

})();
