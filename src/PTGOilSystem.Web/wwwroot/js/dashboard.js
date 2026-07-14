(function () {
    'use strict';

    var svgNs = 'http://www.w3.org/2000/svg';
    var fontFamily = 'Poppins,Vazirmatn,sans-serif';

    function isEnglishUi() {
        var match = document.cookie.match(/(?:^|; )ptg-ui-lang=([^;]+)/);
        return !!match && decodeURIComponent(match[1]).toLowerCase() === 'en';
    }
    function text(fa, en) { return isEnglishUi() ? en : fa; }

    function parseJsonData(element, key, fallback) {
        if (!element || !element.dataset || !element.dataset[key]) return fallback;
        try {
            var parsed = JSON.parse(element.dataset[key]);
            return Array.isArray(parsed) ? parsed : fallback;
        } catch (e) { return fallback; }
    }
    function toNumber(v) { var n = Number(v); return Number.isFinite(n) ? n : 0; }

    function createSvg(w, h) {
        var svg = document.createElementNS(svgNs, 'svg');
        svg.setAttribute('viewBox', '0 0 ' + w + ' ' + h);
        svg.setAttribute('width', '100%');
        svg.setAttribute('height', '100%');
        svg.setAttribute('role', 'img');
        svg.setAttribute('focusable', 'false');
        return svg;
    }
    function el(name, attrs) {
        var e = document.createElementNS(svgNs, name);
        Object.keys(attrs || {}).forEach(function (k) { e.setAttribute(k, attrs[k]); });
        return e;
    }
    function polar(cx, cy, r, deg) {
        var a = (deg - 90) * Math.PI / 180;
        return { x: cx + r * Math.cos(a), y: cy + r * Math.sin(a) };
    }
    function arc(cx, cy, r, start, end) {
        var s = polar(cx, cy, r, end), e = polar(cx, cy, r, start);
        var large = end - start <= 180 ? '0' : '1';
        return ['M', s.x.toFixed(3), s.y.toFixed(3), 'A', r, r, 0, large, 0, e.x.toFixed(3), e.y.toFixed(3)].join(' ');
    }
    function smoothPath(pts) {
        if (!pts.length) return '';
        if (pts.length === 1) return 'M ' + pts[0].x + ' ' + pts[0].y;
        var p = 'M ' + pts[0].x.toFixed(2) + ' ' + pts[0].y.toFixed(2);
        for (var i = 0; i < pts.length - 1; i++) {
            var c = pts[i], n = pts[i + 1], span = (n.x - c.x) / 2;
            p += ' C ' + (c.x + span).toFixed(2) + ' ' + c.y.toFixed(2) + ', '
                + (n.x - span).toFixed(2) + ' ' + n.y.toFixed(2) + ', '
                + n.x.toFixed(2) + ' ' + n.y.toFixed(2);
        }
        return p;
    }

    // ---- Concentric multi-ring donut ----
    function renderRings(element) {
        if (!element) return;
        var values = parseJsonData(element, 'values', []).map(toNumber);
        var colors = parseJsonData(element, 'colors', ['#8BB475', '#7779A2', '#FB7185']);
        var total = values.reduce(function (s, v) { return s + Math.max(0, v); }, 0);
        var svg = createSvg(220, 220);
        var cx = 110, cy = 110;
        var radii = [92, 70, 48];
        svg.setAttribute('aria-label', text('نمودار ترکیب فروش', 'Sales breakdown chart'));

        values.slice(0, 3).forEach(function (v, i) {
            var r = radii[i];
            svg.appendChild(el('circle', { cx: cx, cy: cy, r: r, fill: 'none', stroke: '#E5E7EB', 'stroke-width': '13' }));
            var frac = total > 0 ? Math.max(0, v) / total : 0;
            if (frac > 0) {
                svg.appendChild(el('path', {
                    d: arc(cx, cy, r, 0, Math.min(359.9, 360 * frac)),
                    fill: 'none', stroke: colors[i] || '#8BB475', 'stroke-width': '13', 'stroke-linecap': 'round'
                }));
            }
        });
        element.textContent = '';
        element.appendChild(svg);
    }

    // ---- Two-series smooth area line (income vs expenses) ----
    function renderAreaLine(element) {
        if (!element) return;
        var labels = parseJsonData(element, 'labels', []);
        var income = parseJsonData(element, 'income', []).map(toNumber);
        var expenses = parseJsonData(element, 'expenses', []).map(toNumber);
        var n = Math.max(labels.length, income.length, expenses.length, 2);

        var W = 1000, H = 360;
        var m = { top: 18, right: 20, bottom: 40, left: 52 };
        var cw = W - m.left - m.right, ch = H - m.top - m.bottom;
        var max = Math.max.apply(null, income.concat(expenses).concat([1]));
        var scale = max > 1000 ? 1000 : 1;
        var incK = income.map(function (v) { return v / scale; });
        var expK = expenses.map(function (v) { return v / scale; });
        var maxK = Math.max.apply(null, incK.concat(expK).concat([1]));
        var step = maxK > 600 ? 200 : maxK > 120 ? 50 : maxK > 60 ? 20 : maxK > 12 ? 10 : 5;
        var yMax = Math.max(step, Math.ceil(maxK / step) * step);

        var svg = createSvg(W, H);
        svg.setAttribute('aria-label', text('نمودار درآمد و مصارف', 'Income vs expenses chart'));
        function xs(i) { return n <= 1 ? m.left : m.left + cw * i / (n - 1); }
        function ys(v) { return m.top + ch - ch * v / yMax; }

        var defs = el('defs', {});
        [['incFill', '#8BB475'], ['expFill', '#FB7185']].forEach(function (g) {
            var grad = el('linearGradient', { id: g[0], x1: '0', y1: '0', x2: '0', y2: '1' });
            grad.appendChild(el('stop', { offset: '0', 'stop-color': g[1], 'stop-opacity': '0.22' }));
            grad.appendChild(el('stop', { offset: '1', 'stop-color': g[1], 'stop-opacity': '0' }));
            defs.appendChild(grad);
        });
        svg.appendChild(defs);

        for (var t = 0; t <= 5; t++) {
            var y = ys(yMax * t / 5);
            svg.appendChild(el('line', { x1: m.left, x2: W - m.right, y1: y.toFixed(1), y2: y.toFixed(1), stroke: '#E5E7EB', 'stroke-width': '1', 'stroke-dasharray': '6 8' }));
            var lb = el('text', { x: m.left - 10, y: (y + 4).toFixed(1), fill: '#7B7B7B', 'font-family': fontFamily, 'font-size': '12', 'text-anchor': 'end' });
            lb.textContent = Math.round(yMax * t / 5);
            svg.appendChild(lb);
        }
        for (var i = 0; i < n; i++) {
            var xl = el('text', { x: xs(i).toFixed(1), y: String(H - 12), fill: '#7B7B7B', 'font-family': fontFamily, 'font-size': '12', 'text-anchor': 'middle' });
            xl.textContent = labels[i] || '';
            svg.appendChild(xl);
        }

        [{ vals: incK, color: '#8BB475', fill: 'incFill' }, { vals: expK, color: '#FB7185', fill: 'expFill' }].forEach(function (s) {
            var pts = s.vals.map(function (v, i) { return { x: xs(i), y: ys(v) }; });
            if (!pts.length) return;
            var d = smoothPath(pts);
            svg.appendChild(el('path', { d: d + ' L ' + pts[pts.length - 1].x.toFixed(2) + ' ' + (m.top + ch) + ' L ' + pts[0].x.toFixed(2) + ' ' + (m.top + ch) + ' Z', fill: 'url(#' + s.fill + ')', stroke: 'none' }));
            svg.appendChild(el('path', { d: d, fill: 'none', stroke: s.color, 'stroke-width': '3', 'stroke-linecap': 'round', 'stroke-linejoin': 'round' }));
        });

        element.textContent = '';
        element.appendChild(svg);
    }

    function renderDashboardCharts() {
        renderRings(document.getElementById('ptg-rings'));
        renderAreaLine(document.getElementById('ptg-area'));
    }

    window.__ptgRenderDashboardCharts = renderDashboardCharts;
    if (window.__ptgDashboardChartsReady !== true) {
        window.addEventListener('ptg:page-ready', function () {
            if (typeof window.__ptgRenderDashboardCharts === 'function') {
                window.__ptgRenderDashboardCharts();
            }
        });
        window.__ptgDashboardChartsReady = true;
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', renderDashboardCharts, { once: true });
    } else {
        renderDashboardCharts();
    }
}());
