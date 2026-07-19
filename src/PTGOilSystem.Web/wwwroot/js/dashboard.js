(function () {
    "use strict";

    var svgNs = "http://www.w3.org/2000/svg";

    function parseList(container, name) {
        try {
            var value = JSON.parse(container.dataset[name] || "[]");
            return Array.isArray(value) ? value : [];
        } catch (_error) {
            return [];
        }
    }

    function number(value) {
        var parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : 0;
    }

    function svgElement(name, attributes) {
        var element = document.createElementNS(svgNs, name);
        Object.keys(attributes || {}).forEach(function (key) {
            element.setAttribute(key, attributes[key]);
        });
        return element;
    }

    function locale() {
        return document.documentElement.lang === "en" ? "en-US" : "fa-AF";
    }

    function formatCompact(value) {
        return new Intl.NumberFormat(locale(), {
            notation: "compact",
            maximumFractionDigits: 1
        }).format(value);
    }

    function formatMoney(value) {
        return new Intl.NumberFormat(locale(), {
            maximumFractionDigits: 0
        }).format(value) + " USD";
    }

    function linePath(points) {
        if (!points.length) return "";
        var path = "M " + points[0].x + " " + points[0].y;
        for (var index = 1; index < points.length; index += 1) {
            var previous = points[index - 1];
            var current = points[index];
            var middle = (previous.x + current.x) / 2;
            path += " C " + middle + " " + previous.y + ", " + middle + " " + current.y + ", " + current.x + " " + current.y;
        }
        return path;
    }

    function areaPath(points, baseline) {
        if (!points.length) return "";
        return linePath(points) + " L " + points[points.length - 1].x + " " + baseline + " L " + points[0].x + " " + baseline + " Z";
    }

    function appendText(parent, className, value, x, y) {
        var text = svgElement("text", { x: x, y: y, class: className });
        text.textContent = value;
        parent.appendChild(text);
    }

    function renderEmpty(container) {
        var empty = document.createElement("div");
        var icon = document.createElement("i");
        var label = document.createElement("span");

        empty.className = "dashboard-chart-empty";
        icon.className = "bi bi-graph-up";
        icon.setAttribute("aria-hidden", "true");
        label.textContent = container.dataset.emptyLabel || "No data";
        empty.append(icon, label);
        container.appendChild(empty);
    }

    function seriesPoints(values, maximum, count, left, top, plotWidth, plotHeight) {
        return values.map(function (value, index) {
            var x = count <= 1 ? left + (plotWidth / 2) : left + ((plotWidth * index) / (count - 1));
            var y = top + plotHeight - ((Math.max(0, value) / maximum) * plotHeight);
            return { x: x, y: y, value: value };
        });
    }

    function appendSeries(svg, points, kind, baseline, label) {
        var area = svgElement("path", {
            d: areaPath(points, baseline),
            class: "dashboard-chart-area dashboard-chart-area--" + kind,
            "aria-hidden": "true"
        });
        var line = svgElement("path", {
            d: linePath(points),
            class: "dashboard-chart-line dashboard-chart-line--" + kind
        });
        var title = svgElement("title");
        title.textContent = label + " · " + formatMoney(points[points.length - 1].value);
        line.appendChild(title);
        svg.append(area, line);

        [0, points.length - 1].filter(function (value, index, items) {
            return items.indexOf(value) === index;
        }).forEach(function (index) {
            svg.appendChild(svgElement("circle", {
                cx: points[index].x,
                cy: points[index].y,
                r: "3.5",
                class: "dashboard-chart-dot dashboard-chart-dot--" + kind,
                "aria-hidden": "true"
            }));
        });
    }

    function appendLegend(shell, labels) {
        var legend = document.createElement("div");
        legend.className = "dashboard-chart-legend";
        legend.setAttribute("aria-label", document.documentElement.lang === "en" ? "Chart legend" : "راهنمای نمودار");

        ["sales", "expenses"].forEach(function (kind, index) {
            var item = document.createElement("span");
            var line = document.createElement("span");
            var label = document.createElement("span");
            item.className = "dashboard-chart-legend-item";
            line.className = "dashboard-chart-legend-line dashboard-chart-legend-line--" + kind;
            label.textContent = labels[index];
            item.append(line, label);
            legend.appendChild(item);
        });

        shell.appendChild(legend);
    }

    function renderChart(container) {
        if (!container) return;

        var labels = parseList(container, "labels").map(String);
        var sales = parseList(container, "sales").map(number);
        var expenses = parseList(container, "expenses").map(number);
        var count = Math.min(labels.length, sales.length, expenses.length);
        var maximum = Math.max.apply(null, sales.concat(expenses, [0]));
        container.replaceChildren();

        if (count === 0 || maximum <= 0) {
            renderEmpty(container);
            return;
        }

        labels = labels.slice(0, count);
        sales = sales.slice(0, count);
        expenses = expenses.slice(0, count);

        var width = 760;
        var height = 300;
        var left = 54;
        var right = 18;
        var top = 18;
        var bottom = 42;
        var plotWidth = width - left - right;
        var plotHeight = height - top - bottom;
        var baseline = top + plotHeight;
        var shell = document.createElement("div");
        var svg = svgElement("svg", {
            viewBox: "0 0 " + width + " " + height,
            role: "img",
            focusable: "false",
            "aria-label": container.dataset.chartLabel || "Sales and expense trend"
        });
        shell.className = "dashboard-chart-shell";

        for (var gridIndex = 0; gridIndex <= 4; gridIndex += 1) {
            var ratio = gridIndex / 4;
            var y = top + (plotHeight * ratio);
            svg.appendChild(svgElement("line", {
                x1: left,
                y1: y,
                x2: left + plotWidth,
                y2: y,
                class: "dashboard-chart-grid",
                "aria-hidden": "true"
            }));
            appendText(svg, "dashboard-chart-axis-label", formatCompact(maximum * (1 - ratio)), left - 9, y + 4);
        }

        var labelIndexes = [0, Math.round((count - 1) / 3), Math.round(((count - 1) * 2) / 3), count - 1];
        labelIndexes.filter(function (value, index, items) {
            return items.indexOf(value) === index;
        }).forEach(function (index) {
            var x = count <= 1 ? left + (plotWidth / 2) : left + ((plotWidth * index) / (count - 1));
            appendText(svg, "dashboard-chart-x-label", labels[index], x, height - 14);
        });

        var salesPoints = seriesPoints(sales, maximum, count, left, top, plotWidth, plotHeight);
        var expensePoints = seriesPoints(expenses, maximum, count, left, top, plotWidth, plotHeight);
        appendSeries(svg, salesPoints, "sales", baseline, container.dataset.salesLabel || "Sales");
        appendSeries(svg, expensePoints, "expenses", baseline, container.dataset.expensesLabel || "Expenses");

        shell.appendChild(svg);
        appendLegend(shell, [container.dataset.salesLabel || "Sales", container.dataset.expensesLabel || "Expenses"]);
        container.appendChild(shell);
    }

    function renderDashboardCharts() {
        document.querySelectorAll("[data-dashboard-chart]").forEach(renderChart);
    }

    window.__ptgRenderDashboardCharts = renderDashboardCharts;
    if (window.__ptgDashboardChartsReady !== true) {
        window.addEventListener("ptg:page-ready", renderDashboardCharts);
        window.addEventListener("pageshow", renderDashboardCharts);
        window.__ptgDashboardChartsReady = true;
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", renderDashboardCharts, { once: true });
    } else {
        renderDashboardCharts();
    }
}());
