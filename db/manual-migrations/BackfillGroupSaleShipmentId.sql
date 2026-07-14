-- Backfill ShipmentId for existing GROUP sales (SalesBatch) of terminal stock / truck dispatch.
--
-- Context: group sales for «موجودی مخزن» and «موتر در جریان» were created with ShipmentId = NULL,
-- so the shipment file's sales tab (which counts by ShipmentId) missed them. New sales are fixed in
-- code (ResolveShipmentIdForContractAsync); this script tags the already-saved rows the same way.
--
-- Safe by design:
--   • Only touches SalesTransactions with a SalesBatchId, ShipmentId IS NULL, not cancelled.
--   • Derives the shipment from the source purchase contract:
--        - truck dispatch line  → TruckDispatches.ContractId
--        - terminal stock line  → the sale's inventory OUT movement ContractId
--   • Applies ONLY when that contract maps to exactly one shipment (no ambiguity); otherwise skipped.
--   • Idempotent: re-running changes nothing once rows are tagged.
--
-- Review first with the SELECT at the bottom, then run the UPDATE inside a transaction.

BEGIN;

WITH contract_ship AS (
    -- Purchase contracts that belong to exactly one shipment (unambiguous).
    SELECT "ContractId", MIN("ShipmentId") AS "ShipmentId"
    FROM "ShipmentContracts"
    GROUP BY "ContractId"
    HAVING COUNT(DISTINCT "ShipmentId") = 1
),
sale_contract AS (
    -- موتر در جریان: قرارداد از دیسپچِ لینک‌شده به فروش
    SELECT d."SalesTransactionId" AS "SaleId", d."ContractId" AS "ContractId"
    FROM "TruckDispatches" d
    WHERE d."SalesTransactionId" IS NOT NULL

    UNION

    -- موجودی مخزن: قرارداد از حرکتِ خروجِ همان فروش
    SELECT m."SalesTransactionId" AS "SaleId", m."ContractId" AS "ContractId"
    FROM "InventoryMovements" m
    WHERE m."SalesTransactionId" IS NOT NULL
      AND m."ContractId" IS NOT NULL
      AND m."Direction" = 2   -- MovementDirection.Out
),
resolved AS (
    SELECT sc."SaleId", cs."ShipmentId"
    FROM sale_contract sc
    JOIN contract_ship cs ON cs."ContractId" = sc."ContractId"
    GROUP BY sc."SaleId", cs."ShipmentId"
),
final AS (
    -- فقط فروش‌هایی که پس از تطبیق به یک محموله می‌رسند
    SELECT "SaleId", MIN("ShipmentId") AS "ShipmentId"
    FROM resolved
    GROUP BY "SaleId"
    HAVING COUNT(DISTINCT "ShipmentId") = 1
)
UPDATE "SalesTransactions" s
SET "ShipmentId" = f."ShipmentId"
FROM final f
WHERE s."Id" = f."SaleId"
  AND s."ShipmentId" IS NULL
  AND s."SalesBatchId" IS NOT NULL
  AND s."IsCancelled" = false;

-- Preview what remains NULL after the update (group sales that stayed ambiguous / unmapped):
-- SELECT s."Id", s."InvoiceNumber", s."SalesBatchId", s."SaleStage"
-- FROM "SalesTransactions" s
-- WHERE s."SalesBatchId" IS NOT NULL AND s."ShipmentId" IS NULL AND s."IsCancelled" = false;

COMMIT;
