(() => {
    "use strict";

    const root = document.documentElement;
    const toggle = document.querySelector("[data-finance-theme-toggle]");
    const icon = toggle?.querySelector("[data-finance-theme-icon]");
    const storageKey = "ptg-finance-theme";

    if (!toggle) return;

    const labels = () => document.body.dataset.uiLanguage === "en"
        ? { dark: "Enable dark mode", light: "Enable light mode" }
        : { dark: "فعال‌کردن حالت تیره", light: "فعال‌کردن حالت روشن" };

    const applyTheme = (theme) => {
        const isDark = theme === "dark";
        const currentLabels = labels();
        root.dataset.theme = isDark ? "dark" : "light";
        root.dataset.bsTheme = isDark ? "dark" : "light";
        root.style.colorScheme = isDark ? "dark" : "light";
        toggle.setAttribute("aria-pressed", String(isDark));
        toggle.setAttribute("aria-label", isDark ? currentLabels.light : currentLabels.dark);
        if (icon) icon.className = isDark ? "bi bi-sun" : "bi bi-moon-stars";
    };

    const preferredTheme = () => {
        try {
            const saved = localStorage.getItem(storageKey);
            if (saved === "dark" || saved === "light") {
                return saved;
            }
            if (window.matchMedia?.("(prefers-color-scheme: dark)").matches) {
                return "dark";
            }
        } catch {
            // Storage can be unavailable in restricted browser contexts.
        }
        return "light";
    };

    const syncForCurrentPage = () => {
        const isFinancePage = document.body.classList.contains("is-finance-workspace");
        toggle.hidden = !isFinancePage;

        // The finance theme must not leak into operational pages that reuse the
        // persistent SPA shell. Keep the saved preference for the next visit.
        applyTheme(isFinancePage ? preferredTheme() : "light");
    };

    toggle.addEventListener("click", () => {
        const nextTheme = root.dataset.theme === "dark" ? "light" : "dark";
        applyTheme(nextTheme);
        try {
            localStorage.setItem(storageKey, nextTheme);
        } catch {
            // The active theme still works for the current page.
        }
    });

    syncForCurrentPage();
    window.addEventListener("ptg:page-ready", syncForCurrentPage);
    window.addEventListener("pageshow", syncForCurrentPage);
})();
