---
name: ptg-finance-ledger-guard
description: Use for PTG finance, ledger, supplier, sarraf, payments, RUB/USD, settlement, and debt logic.
effort: high
---

Guard PTG finance and ledger logic.

Rules:
- Never change debit/credit logic blindly.
- First explain the current money flow.
- Check USD base currency and settlement currency.
- Check RUB conversion direction.
- Check Supplier, Sarraf, Customer, CashAccount impact.
- Check old records compatibility.
- Do not create duplicate LedgerEntry records.
- Do not change entity structure unless no safer option exists.
- Prefer small safe fix over redesign.
- Final answer must clearly state financial impact.