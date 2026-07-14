using Microsoft.AspNetCore.Mvc;
using PTGOilSystem.Web.Models.ShipmentPnl;

namespace PTGOilSystem.Web.Controllers;

public partial class ShipmentPnlController
{
    public async Task<IActionResult> Csv()
    {
        var items = await BuildAllIndexItemsAsync();
        return CsvExportSupport.File(this, "shipment-pnl.csv",
            ["Shipment", "Contract", "ContractUnit", "Product", "Customer", "Supplier", "Origin", "Destination", "QuantityMt", "SalesUsd", "PurchaseCostUsd", "OperationalExpensesUsd", "TotalCostUsd", "GrossMarginUsd", "TransportLegCount", "SalesCount", "ExpensesCount", "LedgerCount"],
            items.Select(i => new[]
            {
                i.ShipmentCode, i.ContractNumber, i.ContractUnitText, i.ProductName, i.CustomerName, i.SupplierName, i.OriginName, i.DestinationName,
                CsvExportSupport.Decimal(i.QuantityMt), CsvExportSupport.Decimal(i.TotalSalesUsd), CsvExportSupport.Decimal(i.TotalPurchaseCostUsd),
                CsvExportSupport.Decimal(i.TotalOperationalExpensesUsd), CsvExportSupport.Decimal(i.TotalExpensesUsd),
                CsvExportSupport.Decimal(i.GrossMarginUsd), i.RelatedTransportLegCount.ToString(), i.RelatedSalesCount.ToString(), i.RelatedExpensesCount.ToString(), i.RelatedLedgerCount.ToString()
            }));
    }
}
