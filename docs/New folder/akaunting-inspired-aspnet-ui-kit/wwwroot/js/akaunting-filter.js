/**
 * Clean-room search/filter component inspired by Akaunting's public interaction pattern.
 * No framework dependency. Supports RTL, keyboard navigation, multi-select, date ranges,
 * free-text search and per-page persistence.
 */
class AkFilterBar {
  constructor(root, options = {}) {
    if (!root) throw new Error("AkFilterBar root element is required.");
    this.root = root;
    this.options = {
      fields: [],
      storage: true,
      storagePrefix: "ak-filter:",
      onApply: () => {},
      ...options
    };

    this.state = "idle"; // idle | field | operator | value | date
    this.query = "";
    this.tokens = [];
    this.currentField = null;
    this.currentOperator = null;
    this.multiSelection = new Set();
    this.highlighted = 0;

    this.input = root.querySelector("[data-ak-filter-input]");
    this.chips = root.querySelector("[data-ak-filter-chips]");
    this.popover = root.querySelector("[data-ak-filter-popover]");
    this.enterButton = root.querySelector("[data-ak-filter-enter]");
    this.clearButton = root.querySelector("[data-ak-filter-clear]");

    if (!this.input || !this.chips || !this.popover) {
      throw new Error("AkFilterBar markup is incomplete.");
    }

    this.boundDocumentPointer = this.onDocumentPointer.bind(this);
    this.bind();
    this.restore();
    this.render();
  }

  bind() {
    this.input.addEventListener("focus", () => this.onFocus());
    this.input.addEventListener("input", () => {
      this.query = this.input.value.trimStart();
      this.highlighted = 0;
      if (this.state === "idle") this.state = "field";
      this.renderPopover();
      this.syncEnterButton();
    });
    this.input.addEventListener("keydown", (event) => this.onKeyDown(event));
    this.enterButton?.addEventListener("click", () => this.apply());
    this.clearButton?.addEventListener("click", () => this.clear());
    this.root.addEventListener("click", (event) => this.onRootClick(event));
    document.addEventListener("pointerdown", this.boundDocumentPointer, true);
  }

  destroy() {
    document.removeEventListener("pointerdown", this.boundDocumentPointer, true);
  }

  onFocus() {
    this.root.classList.add("is-focused");
    if (this.state === "idle") this.state = "field";
    this.renderPopover();
  }

  onDocumentPointer(event) {
    if (!this.root.contains(event.target)) {
      this.closePopover();
      this.root.classList.remove("is-focused");
    }
  }

  onRootClick(event) {
    const fieldButton = event.target.closest("[data-ak-field]");
    if (fieldButton) return this.selectField(fieldButton.dataset.akField);

    const operatorButton = event.target.closest("[data-ak-operator]");
    if (operatorButton) return this.selectOperator(operatorButton.dataset.akOperator);

    const valueButton = event.target.closest("[data-ak-value]");
    if (valueButton) return this.selectValue(valueButton.dataset.akValue);

    const removeButton = event.target.closest("[data-ak-remove-token]");
    if (removeButton) return this.removeToken(Number(removeButton.dataset.akRemoveToken));

    const multiCheck = event.target.closest("[data-ak-multi]");
    if (multiCheck) return this.toggleMulti(multiCheck.dataset.akMulti);

    const multiApply = event.target.closest("[data-ak-multi-apply]");
    if (multiApply && this.multiSelection.size) return this.commitMulti();

    const dateApply = event.target.closest("[data-ak-date-apply]");
    if (dateApply) return this.commitDate();

    const searchText = event.target.closest("[data-ak-search-text]");
    if (searchText) return this.apply();
  }

  onKeyDown(event) {
    const options = [...this.popover.querySelectorAll(
      "[data-ak-field], [data-ak-operator], [data-ak-value], [data-ak-search-text]"
    )].filter(el => !el.disabled);

    if (event.key === "ArrowDown" && options.length) {
      event.preventDefault();
      this.highlighted = Math.min(this.highlighted + 1, options.length - 1);
      this.renderHighlight(options);
      return;
    }
    if (event.key === "ArrowUp" && options.length) {
      event.preventDefault();
      this.highlighted = Math.max(this.highlighted - 1, 0);
      this.renderHighlight(options);
      return;
    }
    if (event.key === "Enter") {
      event.preventDefault();
      const highlighted = options[this.highlighted];
      if (highlighted && !this.query && this.state !== "idle") highlighted.click();
      else this.apply();
      return;
    }
    if (event.key === "Escape") {
      this.closePopover();
      return;
    }
    if (event.key === "Backspace" && !this.input.value && this.tokens.length) {
      this.removeToken(this.tokens.length - 1);
    }
  }

  renderHighlight(options) {
    options.forEach((el, index) =>
      el.classList.toggle("is-highlighted", index === this.highlighted)
    );
    options[this.highlighted]?.scrollIntoView({ block: "nearest" });
  }

  selectField(key) {
    const field = this.options.fields.find(item => item.key === key);
    if (!field) return;

    this.currentField = field;
    this.currentOperator = null;
    this.multiSelection.clear();
    this.query = "";
    this.input.value = "";
    this.state = "operator";
    this.setPlaceholder("Select an operator");
    this.renderPopover();
    this.input.focus();
  }

  selectOperator(operator) {
    if (!this.currentField) return;
    this.currentOperator = operator;
    this.query = "";
    this.input.value = "";

    if (this.currentField.type === "date") {
      this.state = "date";
      this.setPlaceholder(operator === "between" ? "Choose a date range" : "Choose a date");
    } else {
      this.state = "value";
      this.setPlaceholder("Select or type a value");
    }
    this.renderPopover();
    this.input.focus();
  }

  selectValue(key) {
    const field = this.currentField;
    if (!field) return;
    let item = (field.values || []).find(value => String(value.key) === String(key));
    if (!item && field.allowCustom) {
      item = { key, label: key };
    }
    if (!item) return;

    this.commitToken({
      fieldKey: field.key,
      fieldLabel: field.label,
      operator: this.currentOperator || "eq",
      value: item.key,
      displayValue: item.label,
      type: field.type || "select"
    });
  }

  toggleMulti(key) {
    if (this.multiSelection.has(key)) this.multiSelection.delete(key);
    else this.multiSelection.add(key);
    this.renderPopover();
  }

  commitMulti() {
    const field = this.currentField;
    const selected = (field.values || []).filter(item =>
      this.multiSelection.has(String(item.key)) || this.multiSelection.has(item.key)
    );
    if (!selected.length) return;

    this.commitToken({
      fieldKey: field.key,
      fieldLabel: field.label,
      operator: this.currentOperator || "in",
      value: selected.map(item => item.key),
      displayValue: selected.map(item => item.label),
      type: "multi"
    });
  }

  commitDate() {
    const start = this.popover.querySelector("[data-ak-date-start]")?.value;
    const end = this.popover.querySelector("[data-ak-date-end]")?.value;
    if (!start) return;

    const isRange = this.currentOperator === "between";
    const finalEnd = isRange ? (end || start) : start;
    this.commitToken({
      fieldKey: this.currentField.key,
      fieldLabel: this.currentField.label,
      operator: this.currentOperator || "eq",
      value: isRange ? [start, finalEnd] : start,
      displayValue: isRange ? `${start} → ${finalEnd}` : start,
      type: "date"
    });
  }

  commitToken(token) {
    // One token per field, matching the public Akaunting pattern where a used field
    // is removed from the field menu until its token is deleted.
    const existing = this.tokens.findIndex(item => item.fieldKey === token.fieldKey);
    if (existing >= 0) this.tokens.splice(existing, 1, token);
    else this.tokens.push(token);

    this.currentField = null;
    this.currentOperator = null;
    this.multiSelection.clear();
    this.query = "";
    this.input.value = "";
    this.state = "field";
    this.highlighted = 0;
    this.setPlaceholder("Search or filter results...");
    this.persist();
    this.render();
    this.input.focus();
  }

  removeToken(index) {
    if (!Number.isInteger(index) || index < 0 || index >= this.tokens.length) return;
    this.tokens.splice(index, 1);
    this.persist();
    this.render();
    this.input.focus();
  }

  clear() {
    this.tokens = [];
    this.query = "";
    this.input.value = "";
    this.currentField = null;
    this.currentOperator = null;
    this.state = "idle";
    this.setPlaceholder("Search or filter results...");
    this.persist();
    this.render();
    this.apply();
  }

  apply() {
    const payload = {
      text: this.input.value.trim(),
      tokens: typeof structuredClone === "function"
        ? structuredClone(this.tokens)
        : JSON.parse(JSON.stringify(this.tokens)),
      queryString: this.serialize()
    };
    this.persist();
    this.closePopover();
    this.options.onApply(payload);
    this.root.dispatchEvent(new CustomEvent("ak:filter-apply", { detail: payload }));
  }

  serialize() {
    const parts = [];
    const text = this.input.value.trim();
    if (text) parts.push(`search="${this.escape(text)}"`);

    for (const token of this.tokens) {
      const key = token.fieldKey;
      const op = token.operator;
      const val = token.value;

      if (op === "neq") {
        parts.push(`not ${key}:${this.escapeValue(val)}`);
      } else if (op === "between" && Array.isArray(val)) {
        parts.push(`${key}>=${this.escapeValue(val[0])}`);
        parts.push(`${key}<=${this.escapeValue(val[1])}`);
      } else if (op === "in" || Array.isArray(val)) {
        parts.push(`${key}:${val.map(v => this.escapeValue(v)).join(",")}`);
      } else {
        parts.push(`${key}:${this.escapeValue(val)}`);
      }
    }
    return parts.join(" ");
  }

  escape(value) {
    return String(value).replaceAll("\\", "\\\\").replaceAll('"', '\\"');
  }

  escapeValue(value) {
    const string = String(value);
    return /\s/.test(string) ? `"${this.escape(string)}"` : string;
  }

  getAvailableFields() {
    const used = new Set(this.tokens.map(token => token.fieldKey));
    const query = this.input.value.trim().toLocaleLowerCase();
    return this.options.fields.filter(field =>
      !used.has(field.key) &&
      (!query || field.label.toLocaleLowerCase().includes(query))
    );
  }

  getOperators(field) {
    const configured = field?.operators || ["eq", "neq"];
    const labels = {
      eq: { label: "Is", symbol: "=" },
      neq: { label: "Is not", symbol: "≠" },
      in: { label: "Is any of", symbol: "∈" },
      between: { label: "Between", symbol: "↔" },
      contains: { label: "Contains", symbol: "⊃" }
    };
    return configured.map(key => ({ key, ...(labels[key] || { label: key, symbol: "•" }) }));
  }

  getFilteredValues() {
    const field = this.currentField;
    const query = this.input.value.trim().toLocaleLowerCase();
    return (field?.values || []).filter(item =>
      !query || item.label.toLocaleLowerCase().includes(query)
    );
  }

  render() {
    this.renderChips();
    this.renderPopover();
    this.syncEnterButton();
    this.clearButton.hidden = !(this.tokens.length || this.input.value);
  }

  renderChips() {
    this.chips.innerHTML = this.tokens.map((token, index) => {
      const op = { eq: "=", neq: "≠", in: "∈", between: "↔", contains: "⊃" }[token.operator] || token.operator;
      let display = token.displayValue;
      if (Array.isArray(display)) {
        display = display.length > 3
          ? `${display.slice(0, 3).join(", ")} + ${display.length - 3} more`
          : display.join(", ");
      }
      return `
        <span class="ak-chip-group">
          <span class="ak-chip">${this.html(token.fieldLabel)}</span>
          <span class="ak-chip ak-chip-operator" aria-label="${this.html(token.operator)}">${this.html(op)}</span>
          <span class="ak-chip">
            <span>${this.html(display)}</span>
            <button type="button" class="ak-chip-remove" data-ak-remove-token="${index}" aria-label="Remove filter">×</button>
          </span>
        </span>`;
    }).join("");
  }

  renderPopover() {
    if (this.state === "idle") return this.closePopover();

    let content = "";
    if (this.state === "field") content = this.renderFields();
    if (this.state === "operator") content = this.renderOperators();
    if (this.state === "value") content = this.renderValues();
    if (this.state === "date") content = this.renderDate();

    this.popover.innerHTML = content;
    this.popover.hidden = !content;
    if (content) {
      this.popover.classList.remove("is-opening");
      void this.popover.offsetWidth;
      this.popover.classList.add("is-opening");
      this.renderHighlight([...this.popover.querySelectorAll(
        "[data-ak-field], [data-ak-operator], [data-ak-value], [data-ak-search-text]"
      )]);
    }
  }

  renderFields() {
    const fields = this.getAvailableFields();
    const rows = fields.map(field => `
      <li>
        <button type="button" class="ak-option" data-ak-field="${this.attr(field.key)}">
          <span class="ak-option-icon">${field.icon || "⌕"}</span>
          <span>${this.html(field.label)}</span>
          <span class="ak-option-meta">${this.html(field.type || "text")}</span>
        </button>
      </li>`).join("");

    const searchRow = this.input.value.trim()
      ? `<li class="ak-option-search">
           <button type="button" class="ak-option" data-ak-search-text>
             Search for “${this.html(this.input.value.trim())}”
           </button>
         </li>`
      : "";

    return rows || searchRow
      ? `${rows}${searchRow}`
      : `<li class="ak-option" aria-disabled="true">No filters available</li>`;
  }

  renderOperators() {
    return this.getOperators(this.currentField).map(operator => `
      <li>
        <button type="button" class="ak-option" data-ak-operator="${operator.key}">
          <span class="ak-option-icon">${this.html(operator.symbol)}</span>
          <span>${this.html(operator.label)}</span>
        </button>
      </li>`).join("");
  }

  renderValues() {
    const values = this.getFilteredValues();
    const isMulti = this.currentField?.multiple || this.currentOperator === "in";

    const rows = values.map(value => {
      if (isMulti) {
        const checked = this.multiSelection.has(String(value.key)) || this.multiSelection.has(value.key);
        return `
          <li>
            <label class="ak-option">
              <input class="ak-check" type="checkbox" data-ak-multi="${this.attr(value.key)}" ${checked ? "checked" : ""}>
              <span>${this.html(value.label)}</span>
            </label>
          </li>`;
      }
      return `
        <li>
          <button type="button" class="ak-option" data-ak-value="${this.attr(value.key)}">
            <span>${this.html(value.label)}</span>
          </button>
        </li>`;
    }).join("");

    const customText = !values.length && this.input.value.trim() && this.currentField?.allowCustom
      ? `<li>
           <button type="button" class="ak-option" data-ak-value="${this.attr(this.input.value.trim())}">
             Use “${this.html(this.input.value.trim())}”
           </button>
         </li>`
      : "";

    const footer = isMulti
      ? `<li class="ak-multi-footer" data-ak-multi-apply aria-disabled="${this.multiSelection.size ? "false" : "true"}">
           Apply ${this.multiSelection.size ? `(${this.multiSelection.size})` : ""}
         </li>`
      : "";

    return `${rows || customText || '<li class="ak-option" aria-disabled="true">No matching values</li>'}${footer}`;
  }

  renderDate() {
    const isRange = this.currentOperator === "between";
    return `
      <li class="ak-date-panel">
        <label>
          ${isRange ? "Start date" : "Date"}
          <input type="date" data-ak-date-start>
        </label>
        ${isRange ? `
        <label>
          End date
          <input type="date" data-ak-date-end>
        </label>` : ""}
        <button type="button" class="ak-primary ak-date-apply" data-ak-date-apply>Apply date</button>
      </li>`;
  }

  closePopover() {
    this.popover.hidden = true;
    this.popover.innerHTML = "";
    if (!this.currentField) this.state = "idle";
  }

  setPlaceholder(value) {
    this.input.placeholder = value;
  }

  syncEnterButton() {
    if (!this.enterButton) return;
    this.enterButton.disabled = !this.input.value.trim() && !this.tokens.length;
  }

  storageKey() {
    return `${this.options.storagePrefix}${location.pathname}`;
  }

  persist() {
    if (!this.options.storage) return;
    const value = { tokens: this.tokens, text: this.input.value };
    localStorage.setItem(this.storageKey(), JSON.stringify(value));
  }

  restore() {
    if (!this.options.storage) return;
    try {
      const raw = localStorage.getItem(this.storageKey());
      if (!raw) return;
      const value = JSON.parse(raw);
      if (Array.isArray(value.tokens)) this.tokens = value.tokens;
      if (typeof value.text === "string") {
        this.input.value = value.text;
        this.query = value.text;
      }
    } catch {
      localStorage.removeItem(this.storageKey());
    }
  }

  html(value) {
    return String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#039;");
  }

  attr(value) { return this.html(value); }
}

class AkSidebar {
  constructor(shell) {
    this.shell = shell;
    this.sidebar = shell.querySelector("[data-ak-sidebar]");
    this.toggle = shell.querySelector("[data-ak-sidebar-toggle]");
    this.mobileToggle = shell.querySelector("[data-ak-mobile-toggle]");
    this.overlay = shell.querySelector("[data-ak-overlay]");
    this.bind();
  }

  bind() {
    this.toggle?.addEventListener("click", () => {
      this.sidebar.classList.toggle("is-collapsed");
      this.shell.classList.toggle("sidebar-collapsed");
      this.toggle.setAttribute(
        "aria-expanded",
        String(!this.sidebar.classList.contains("is-collapsed"))
      );
    });
    this.mobileToggle?.addEventListener("click", () => this.openMobile());
    this.overlay?.addEventListener("click", () => this.closeMobile());
    document.addEventListener("keydown", event => {
      if (event.key === "Escape") this.closeMobile();
    });
    this.sidebar?.querySelectorAll("details").forEach(details => {
      details.addEventListener("toggle", () => {
        details.querySelector(":scope > summary")?.setAttribute(
          "aria-expanded",
          String(details.open)
        );
      });
    });
  }

  openMobile() {
    this.shell.classList.add("mobile-menu-open");
    document.body.style.overflow = "hidden";
  }
  closeMobile() {
    this.shell.classList.remove("mobile-menu-open");
    document.body.style.overflow = "";
  }
}

window.AkFilterBar = AkFilterBar;
window.AkSidebar = AkSidebar;
