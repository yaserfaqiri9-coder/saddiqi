using Microsoft.AspNetCore.Mvc;

namespace PTGOilSystem.Web.Controllers;

public partial class ReconciliationController
{
    public async Task<IActionResult> OpenContractsCsv()
    {
        var model = await BuildOpenContractsAsync();
        return CsvExportSupport.File(this, "open-contracts.csv",
            ["Contract", "Product", "ContractUnit", "ContractQuantityMt", "LoadedQuantityMt", "ReceivedQuantityMt", "SoldQuantityMt", "RemainingQuantityMt", "Status"],
            model.Rows.Select(r => new[]
            {
                r.ContractNumber, r.ProductName, r.ContractUnitText, CsvExportSupport.Decimal(r.ContractQuantityMt),
                CsvExportSupport.Decimal(r.LoadedQuantityMt), CsvExportSupport.Decimal(r.ReceivedQuantityMt),
                CsvExportSupport.Decimal(r.SoldQuantityMt), CsvExportSupport.Decimal(r.RemainingQuantityMt), r.Status
            }));
    }

    public async Task<IActionResult> OpenShipmentsCsv()
    {
        var model = await BuildOpenShipmentsAsync();
        var rows = model.ShipmentsWithoutSales.Select(r => new[]
            {
                "ShipmentWithoutSales", r.ShipmentCode, r.ContractNumber, r.ContractUnitText, CsvExportSupport.Decimal(r.QuantityMt), r.Status, ""
            })
            .Concat(model.ShipmentsWithoutExpenses.Select(r => new[]
            {
                "ShipmentWithoutExpenses", r.ShipmentCode, r.ContractNumber, r.ContractUnitText, CsvExportSupport.Decimal(r.QuantityMt), r.Status, ""
            }))
            .Concat(model.DispatchesWithoutReceipt.Select(r => new[]
            {
                "DispatchWithoutReceipt", "#" + r.DispatchId, r.ContractNumber, r.ContractUnitText, CsvExportSupport.Decimal(r.LoadedQuantityMt), r.Status, r.Reason
            }));

        return CsvExportSupport.File(this, "open-shipments.csv",
            ["Kind", "Reference", "Contract", "ContractUnit", "QuantityMt", "Status", "Reason"],
            rows);
    }

    public async Task<IActionResult> MissingLedgerCsv()
    {
        var model = await BuildMissingLedgerAsync();
        var rows = model.SalesWithoutLedger
            .Concat(model.ExpensesWithoutLedger)
            .Concat(model.PaymentsWithoutLedger)
            .Select(r => new[]
            {
                r.SourceType, r.SourceId.ToString(), CsvExportSupport.Date(r.Date), r.Reference,
                CsvExportSupport.Decimal(r.AmountUsd), r.Status
            });

        return CsvExportSupport.File(this, "missing-ledger.csv",
            ["SourceType", "SourceId", "Date", "Reference", "AmountUsd", "Status"],
            rows);
    }

    public async Task<IActionResult> NonZeroBalancesCsv()
    {
        var model = await BuildNonZeroBalancesAsync();
        var rows = model.ContractBalances.Concat(model.CustomerBalances).Concat(model.SupplierBalances)
            .Select(r => new[]
            {
                r.EntityType, r.EntityId.ToString(), r.Name, CsvExportSupport.Decimal(r.DebitUsd),
                CsvExportSupport.Decimal(r.CreditUsd), CsvExportSupport.Decimal(r.BalanceUsd), r.Status
            });

        return CsvExportSupport.File(this, "non-zero-balances.csv",
            ["EntityType", "EntityId", "Name", "DebitUsd", "CreditUsd", "BalanceUsd", "Status"],
            rows);
    }
}
