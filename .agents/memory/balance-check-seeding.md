---
name: ContractBalanceTransfer test seeding
description: How to correctly seed test data for ContractBalanceTransfer service tests after the balance guard was added.
---

The `ContractBalanceTransferService.CreateAsync` now enforces that the fromContract has a ledger net balance ≥ amountUsd before creating a transfer. This means tests that create a successful transfer must first seed a credit entry.

**Rule:** Before calling `service.CreateAsync` or `controller.Create` in tests that expect a Redirect (success), call `SeedContractCreditAsync(db, fromContract.Id, sufficientAmount)`.

**Why:** Before this change, contracts had zero balance and all transfers went through. The new guard `if (amountUsd > availableBalance) throw BusinessRuleException(...)` now blocks zero-balance transfers.

**How to apply:**
- Use `SeedContractCreditAsync` helper in the test class (seeds a `SupplierPayment` credit ledger entry).
- When asserting on `db.LedgerEntries.ToListAsync()`, filter by `SourceType == ContractBalanceTransferService.LedgerSourceType` to avoid the seed entry distorting `Assert.Equal` or `Assert.All` checks.
- `GetContractNetBalanceUsdAsync` sums ALL ledger entries for the contract (not just ContractBalanceTransfer ones), so it includes seed credits.
