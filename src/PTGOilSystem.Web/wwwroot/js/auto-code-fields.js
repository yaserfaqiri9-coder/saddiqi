(function () {
    const fieldNames = new Set([
        'Code',
        'EmployeeCode',
        'AssetCode',
        'TankCode',
        'ShipmentCode',
        'InvoiceNumber',
        'SaleInvoiceNumber'
    ]);

    const excludedPathParts = [
    '/Currencies',
    '/Units',
    '/PlattsRates',
    '/Locations',
    '/Products',
    '/DailyFxRates',
    '/ExpenseTypes',
    '/ExpenseRules',
    '/StorageTanks',
    '/Terminals',
    '/Trucks',
    '/Wagons',
    '/Drivers',
    '/Vessels',
    '/Companies',
    '/Customers',
    '/Employees',
    '/Partners',
    '/ServiceProviders',
    '/Sarrafs',
    '/OperationalAssets',
    '/CashAccounts'
];

    const controllerKinds = new Map([
        ['CashAccounts', 'CashAccount'],
        ['Companies', 'Company'],
        ['Customers', 'Customer'],
        ['Employees', 'Employee'],
        ['ExpenseTypes', 'ExpenseType'],
        ['Locations', 'Location'],
        ['OperationalAssets', 'OperationalAsset'],
        ['Partners', 'Partner'],
        ['Products', 'Product'],
        ['ServiceProviders', 'ServiceProvider'],
        ['Shipments', 'Shipment'],
        ['StorageTanks', 'StorageTank'],
        ['Suppliers', 'Supplier'],
        ['Terminals', 'Terminal'],
        ['Vessels', 'Vessel']
    ]);

    const fieldKinds = new Map([
        ['EmployeeCode', 'Employee'],
        ['AssetCode', 'OperationalAsset'],
        ['TankCode', 'StorageTank'],
        ['ShipmentCode', 'Shipment'],
        ['InvoiceNumber', 'SalesInvoice'],
        ['SaleInvoiceNumber', 'SalesInvoice']
    ]);

    function getFieldName(input) {
        const name = input.getAttribute('name') || '';
        return name.split('.').pop()?.replace(/\[[0-9]+\]/g, '') || name;
    }

    function getFormPath(form) {
        const action = form?.action || form?.getAttribute('action') || window.location.pathname;
        try {
            return new URL(action, window.location.origin).pathname;
        } catch {
            return action || window.location.pathname;
        }
    }

    function isAutoCodeInput(input) {
        if (!input || input.type === 'hidden') {
            return false;
        }

        if (!fieldNames.has(getFieldName(input))) {
            return false;
        }

        const form = input.closest('form');
        if (form && String(form.method || '').toLowerCase() === 'get') {
            return false;
        }

        const action = getFormPath(form);
        return !excludedPathParts.some(part => action.includes(part) || window.location.pathname.includes(part));
    }

    function inferKind(input) {
        const field = getFieldName(input);
        if (fieldKinds.has(field)) {
            return fieldKinds.get(field);
        }

        const form = input.closest('form');
        const paths = [getFormPath(form), window.location.pathname];
        for (const path of paths) {
            const controller = path.split('/').filter(Boolean)[0];
            if (controllerKinds.has(controller)) {
                return controllerKinds.get(controller);
            }
        }

        return null;
    }

    function getOrCreateNote(input) {
        const wrapper = input.closest('.col-md-3, .col-md-4, .col-md-5, .mb-3, .form-group, .employee-modal-field, .ptg-master-field') || input.parentElement;
        if (!wrapper) {
            return null;
        }

        let note = wrapper.querySelector('[data-auto-code-note]');
        if (!note) {
            note = document.createElement('div');
            note.className = 'ptg-auto-code-note';
            note.dataset.autoCodeNote = 'true';
            input.insertAdjacentElement('afterend', note);
        }

        return note;
    }

    function lockAutoCodeInput(input) {
        input.readOnly = true;
        input.autocomplete = 'off';
        input.classList.add('ptg-auto-code-input');
        input.removeAttribute('placeholder');
        input.title = 'کد توسط سیستم ساخته می‌شود';

        const note = getOrCreateNote(input);
        if (note) {
            note.textContent = input.value ? 'کد سیستم' : 'در حال دریافت کد...';
        }
    }

    function incrementCode(code, offset) {
        if (!offset) {
            return code;
        }

        const match = String(code || '').match(/^(.*?)(\d+)$/);
        if (!match) {
            return code;
        }

        const prefix = match[1];
        const digits = match[2];
        const next = Number.parseInt(digits, 10) + offset;
        return prefix + String(next).padStart(digits.length, '0');
    }

    async function fetchPreview(kind) {
        const url = `/AutoCodes/Preview?kind=${encodeURIComponent(kind)}`;
        const response = await fetch(url, {
            headers: { 'Accept': 'application/json' },
            credentials: 'same-origin',
            cache: 'no-store'
        });

        if (!response.ok) {
            throw new Error(`Auto code preview failed: ${response.status}`);
        }

        const payload = await response.json();
        return payload.code || '';
    }

    function populateAutoCodes(inputs) {
        const groups = new Map();
        inputs.forEach(input => {
            if (input.value || input.dataset.autoCodeLoading === 'true') {
                return;
            }

            const kind = inferKind(input);
            if (!kind) {
                const note = getOrCreateNote(input);
                if (note) {
                    note.textContent = 'هنگام ذخیره خودکار ساخته می‌شود';
                }
                return;
            }

            input.dataset.autoCodeLoading = 'true';
            const key = `${kind}|${getFormPath(input.closest('form'))}`;
            if (!groups.has(key)) {
                groups.set(key, { kind, inputs: [] });
            }
            groups.get(key).inputs.push(input);
        });

        groups.forEach(group => {
            fetchPreview(group.kind)
                .then(code => {
                    group.inputs.forEach((input, index) => {
                        input.value = incrementCode(code, index);
                        const note = getOrCreateNote(input);
                        if (note) {
                            note.textContent = 'کد سیستم';
                        }
                    });
                })
                .catch(() => {
                    group.inputs.forEach(input => {
                        const note = getOrCreateNote(input);
                        if (note) {
                            note.textContent = 'هنگام ذخیره خودکار ساخته می‌شود';
                        }
                    });
                })
                .finally(() => {
                    group.inputs.forEach(input => {
                        delete input.dataset.autoCodeLoading;
                    });
                });
        });
    }

    function applyAutoCodeFields(root) {
        const inputs = Array.from(root.querySelectorAll('input')).filter(isAutoCodeInput);
        inputs.forEach(lockAutoCodeInput);
        populateAutoCodes(inputs);
    }

    document.addEventListener('DOMContentLoaded', function () {
        applyAutoCodeFields(document);
    });

    document.addEventListener('shown.bs.modal', function (event) {
        applyAutoCodeFields(event.target);
    });
})();
