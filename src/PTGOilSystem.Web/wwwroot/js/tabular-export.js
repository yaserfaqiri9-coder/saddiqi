(function () {
    "use strict";

    function restore(link) {
        if (!link) return;
        link.classList.remove("disabled");
        link.removeAttribute("aria-disabled");
        link.removeAttribute("data-export-busy");
        var icon = link.querySelector("i");
        if (icon && icon.dataset.exportIcon) {
            icon.className = icon.dataset.exportIcon;
            delete icon.dataset.exportIcon;
        }
    }

    document.addEventListener("click", function (event) {
        var link = event.target.closest("[data-export-link]");
        if (!link) return;
        if (link.hasAttribute("data-export-busy")) {
            event.preventDefault();
            return;
        }

        link.setAttribute("data-export-busy", "true");
        link.setAttribute("aria-disabled", "true");
        link.classList.add("disabled");
        var icon = link.querySelector("i");
        if (icon) {
            icon.dataset.exportIcon = icon.className;
            icon.className = "spinner-border spinner-border-sm";
        }

        window.setTimeout(function () { restore(link); }, 5000);
    });

    window.addEventListener("pageshow", function () {
        document.querySelectorAll("[data-export-link][data-export-busy]").forEach(restore);
    });
}());
