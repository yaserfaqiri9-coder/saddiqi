# Stage 2 accounting: legacy company ownership report

This report records ownership gaps only. Stage 2 does not change any legacy entity and does not infer a company from a party, user, related record, or the first company in the database.

## Definitive today

| Entity | Current ownership |
|---|---|
| `Contract` | Required `CompanyId`; suitable as a definitive company source for that contract only. |
| `SalesTransaction` | Nullable `CompanyId`; definitive only when populated. A null value must remain unresolved. |

## Not definitive for future journal integration

| Area | Legacy entities | Gap |
|---|---|---|
| Cash and payments | `CashAccount`, `PaymentTransaction` | No direct required `CompanyId`. A cash account or payment must not be assigned from its party or linked document by guesswork. |
| Existing subledger | `LedgerEntry`, `ContractBalanceTransfer`, `SupplierPaymentAllocation` | No direct required company ownership on the legacy ledger path. Contract linkage can be absent or insufficient for every case. |
| Sales and expenses | `SalesTransaction` when `CompanyId` is null, `ExpenseTransaction` | Sales ownership is nullable; expenses have no direct required company ownership. |
| Logistics and stock | `InventoryMovement`, `LoadingRegister`, `LoadingReceipt`, `Shipment`, `StorageTank` | Company may sometimes be reachable through a contract path, but the path is not a required direct invariant for every record. Shipments can also have multiple contract links. |
| Parties and staff | `Customer`, `Supplier`, `ServiceProvider`, `Sarraf`, `Employee`, `Driver`, `Partner` | These master records are currently shared and have no required company ownership. Party identity must not choose a journal company. |
| Other dimensions | `Product`, `CashAccount`, `StorageTank` | These dimensions are not company-owned in the legacy model, so a journal line dimension cannot establish company ownership. |

## Required decision before pilot integration

Each pilot event must provide an explicit, validated `CompanyId`. If its legacy source has no required company, the pilot needs a separate approved ownership rule or data change. No historical backfill or automatic ownership inference is part of Stage 2.
