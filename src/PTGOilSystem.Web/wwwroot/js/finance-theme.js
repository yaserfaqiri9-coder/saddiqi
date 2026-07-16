(() => {
    "use strict";

    const root = document.documentElement;
    const toggle = document.querySelector("[data-finance-theme-toggle]");
    const icon = toggle?.querySelector("[data-finance-theme-icon]");
    const storageKey = "ptg-finance-theme";

    if (!toggle) return;

    const labels = document.body.dataset.uiLanguage === "en"
        ? { dark: "Enable dark mode", light: "Enable light mode" }
        : { dark: "فعال‌کردن حالت تیره", light: "فعال‌کردن حالت روشن" };

    const applyTheme = (theme) => {
        const isDark = theme === "dark";
        root.dataset.theme = isDark ? "dark" : "light";
        root.dataset.bsTheme = isDark ? "dark" : "light";
        root.style.colorScheme = isDark ? "dark" : "light";
        toggle.setAttribute("aria-pressed", String(isDark));
        toggle.setAttribute("aria-label", isDark ? labels.light : labels.dark);
        if (icon) icon.className = isDark ? "bi bi-sun" : "bi bi-moon-stars";
    };

    let preferredTheme = "light";
    try {
        const saved = localStorage.getItem(storageKey);
        if (saved === "dark" || saved === "light") {
            preferredTheme = saved;
        } else if (window.matchMedia?.("(prefers-color-scheme: dark)").matches) {
            preferredTheme = "dark";
        }
    } catch {
        // Storage can be unavailable in restricted browser contexts.
    }

    applyTheme(preferredTheme);

    toggle.addEventListener("click", () => {
        const nextTheme = root.dataset.theme === "dark" ? "light" : "dark";
        applyTheme(nextTheme);
        try {
            localStorage.setItem(storageKey, nextTheme);
        } catch {
            // The active theme still works for the current page.
        }
    });
})();
