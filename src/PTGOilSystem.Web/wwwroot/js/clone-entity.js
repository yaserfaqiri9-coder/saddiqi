/* clone-entity.js — quick-clone any master-data record into its Create modal */
(function () {
    'use strict';

    function toPascal(key) {
        return key.charAt(0).toUpperCase() + key.slice(1);
    }

    function applyData(form, data) {
        Object.entries(data).forEach(function ([key, value]) {
            var name = toPascal(key);
            var field = form.querySelector('[name="' + name + '"]');
            if (!field) return;
            if (field.type === 'checkbox') {
                field.checked = !!value;
                field.dispatchEvent(new Event('change', { bubbles: true }));
            } else {
                field.value = (value === null || value === undefined) ? '' : String(value);
                field.dispatchEvent(new Event('change', { bubbles: true }));
            }
        });
        var previewSrc = form.querySelector('[data-modal-preview-source]');
        if (previewSrc) previewSrc.dispatchEvent(new Event('input', { bubbles: true }));
    }

    function showNotice(modal, sourceName) {
        var old = modal.querySelector('.ptg-clone-notice');
        if (old) old.remove();
        var n = document.createElement('div');
        n.className = 'ptg-clone-notice';
        n.style.cssText = [
            'display:flex', 'align-items:center', 'gap:.45rem',
            'padding:.42rem .85rem', 'margin-bottom:10px',
            'background:linear-gradient(135deg,#eff6ff,#e8f0fe)',
            'border:1px solid #bfdbfe', 'border-radius:8px',
            'color:#1d4ed8', 'font-size:.76rem', 'font-weight:700',
            'animation:ptg-modal-spring-in .22s cubic-bezier(.175,.885,.32,1.275) both'
        ].join(';');
        n.innerHTML = '<i class="bi bi-copy" style="font-size:.85rem"></i>'
            + '<span>کلون از: <strong>' + sourceName + '</strong></span>';
        var scroll = modal.querySelector('.ptg-modal-form-scroll');
        if (scroll) scroll.prepend(n);
    }

    document.addEventListener('click', async function (e) {
        var btn = e.target.closest('[data-ptg-clone-url]');
        if (!btn) return;
        e.preventDefault();

        var url = btn.getAttribute('data-ptg-clone-url');
        var modalId = btn.getAttribute('data-ptg-clone-modal');
        if (!url || !modalId) return;

        var origHTML = btn.innerHTML;
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';

        try {
            var resp = await fetch(url, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                credentials: 'same-origin'
            });
            if (!resp.ok) throw new Error('HTTP ' + resp.status);
            var data = await resp.json();

            var modal = document.getElementById(modalId);
            if (!modal) return;

            /* open modal */
            var bsModal = window.bootstrap && window.bootstrap.Modal.getOrCreateInstance(modal);
            if (bsModal) bsModal.show();

            /* reset scroll */
            modal.querySelectorAll('.ptg-modal-form-scroll').forEach(function (el) { el.scrollTop = 0; });

            requestAnimationFrame(function () {
                var form = modal.querySelector('form');
                if (!form) return;

                /* clear validation state */
                form.querySelectorAll('.field-validation-error').forEach(function (el) { el.textContent = ''; });
                form.querySelectorAll('.alert.alert-danger').forEach(function (el) { el.textContent = ''; });

                /* clear Id hidden field (so it's treated as Create) */
                var idField = form.querySelector('input[name="Id"][type="hidden"]');
                if (idField) idField.value = '';

                applyData(form, data);

                var name = data.name || data.Name || '';
                if (name) showNotice(modal, name);
            });

        } catch (err) {
            console.warn('[clone-entity]', err);
        } finally {
            btn.disabled = false;
            btn.innerHTML = origHTML;
        }
    });
})();
