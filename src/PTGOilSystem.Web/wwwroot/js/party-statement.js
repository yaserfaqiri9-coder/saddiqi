(function () {
    "use strict";

    function printStatement() {
        window.print();
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("[data-statement-print]").forEach(function (button) {
            button.addEventListener("click", printStatement);
        });

        var documentRoot = document.querySelector("[data-statement-auto-print='true']");
        if (documentRoot) {
            window.addEventListener("load", printStatement, { once: true });
        }
    });
})();
