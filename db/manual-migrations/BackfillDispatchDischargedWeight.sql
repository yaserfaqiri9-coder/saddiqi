-- Backfill DischargedQuantityMt for truck dispatches freight-settled by an
-- intermediate version of TruckSettlementsController that set IsFreightSettled +
-- ShortageMt but left DischargedQuantityMt NULL. Group sale reads
-- (DischargedQuantityMt ?? LoadedQuantityMt), so these showed the ORIGINAL loaded
-- weight instead of the settled (discharge) weight.
--
-- Settled discharge weight = LoadedQuantityMt - ShortageMt. This reproduces exactly
-- what the current (fixed) SettleDispatchFreightAsync now stores as row.QuantityMt.
--
-- SAFE: only touches dispatches whose cargo is still on the vehicle — freight-settled,
-- not sold (SalesTransactionId IS NULL), still Loaded (Status = 1), and never assigned
-- a discharge weight (DischargedQuantityMt IS NULL). No inventory movement / sale exists,
-- so there is no double-count risk. Reversible: set DischargedQuantityMt back to NULL.

UPDATE "TruckDispatches"
SET "DischargedQuantityMt" = ROUND("LoadedQuantityMt" - COALESCE("ShortageMt", 0), 4)
WHERE "IsFreightSettled" = true
  AND "DischargedQuantityMt" IS NULL
  AND "SalesTransactionId" IS NULL
  AND "Status" = 1;
