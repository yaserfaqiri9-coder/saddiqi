using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PTGOilSystem.Web.Infrastructure.RateLimiting;
using PTGOilSystem.Web.Models.ShipmentPnl;
using PTGOilSystem.Web.Services.Exports;

namespace PTGOilSystem.Web.Controllers;

public partial class ShipmentPnlController
{
    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> Export(string? format)
    {
        var items = await BuildAllIndexItemsAsync();
        var document = new TabularExportDocument
        {
            FileNameStem = "PTG_Shipment_Profit",
            TitleFa = "سود محموله‌ها",
            TitleEn = "Shipment Profitability",
            KnownRowCount = items.Count,
            ForceLandscape = true,
            Columns =
            [
                new("محموله", "Shipment", Width: 16), new("قرارداد", "Contract", Width: 16), new("واحد", "Unit", Width: 12),
                new("جنس", "Product", Width: 15), new("مشتری", "Customer", Width: 18), new("تأمین‌کننده", "Supplier", Width: 18),
                new("مبدا", "Origin", Width: 14), new("مقصد", "Destination", Width: 14),
                new("مقدار MT", "Quantity MT", TabularExportValueType.Number, 14), new("فروش USD", "Sales USD", TabularExportValueType.Number, 15),
                new("خرید USD", "Purchase cost USD", TabularExportValueType.Number, 16),
                new("مصارف عملیاتی USD", "Operational expenses USD", TabularExportValueType.Number, 18),
                new("هزینه کل USD", "Total cost USD", TabularExportValueType.Number, 16),
                new("سود ناخالص USD", "Gross margin USD", TabularExportValueType.Number, 17),
                new("حمل‌ها", "Transport legs", TabularExportValueType.Integer, 11), new("فروش‌ها", "Sales", TabularExportValueType.Integer, 10),
                new("مصارف", "Expenses", TabularExportValueType.Integer, 10), new("اسناد دفتر", "Ledger entries", TabularExportValueType.Integer, 12)
            ],
            Rows = items.Select(item => new TabularExportRow(
            [
                TabularExportCell.Text(item.ShipmentCode), TabularExportCell.Text(item.ContractNumber), TabularExportCell.Text(item.ContractUnitText),
                TabularExportCell.Text(item.ProductName), TabularExportCell.Text(item.CustomerName), TabularExportCell.Text(item.SupplierName),
                TabularExportCell.Text(item.OriginName), TabularExportCell.Text(item.DestinationName), TabularExportCell.Number(item.QuantityMt),
                TabularExportCell.Number(item.TotalSalesUsd), TabularExportCell.Number(item.TotalPurchaseCostUsd),
                TabularExportCell.Number(item.TotalOperationalExpensesUsd), TabularExportCell.Number(item.TotalExpensesUsd),
                TabularExportCell.Number(item.GrossMarginUsd), TabularExportCell.Integer(item.RelatedTransportLegCount),
                TabularExportCell.Integer(item.RelatedSalesCount), TabularExportCell.Integer(item.RelatedExpensesCount),
                TabularExportCell.Integer(item.RelatedLedgerCount)
            ]))
        };
        return TabularExportSupport.File(this, format, document);
    }

    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
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
