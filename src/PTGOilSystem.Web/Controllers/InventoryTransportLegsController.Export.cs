using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PTGOilSystem.Web.Infrastructure.RateLimiting;
using PTGOilSystem.Web.Models.InventoryTransport;
using PTGOilSystem.Web.Services.Exports;

namespace PTGOilSystem.Web.Controllers;

public partial class InventoryTransportLegsController
{
    [HttpGet, EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> Export(string? format, [FromQuery] InventoryTransportLegIndexFilterViewModel filter)
    {
        var view = (ViewResult)await Index(filter, page: 0);
        var document = TabularExportAuto.Build(view.Model!, "PTG_Inventory_Transfers", "حمل موجودی", "Inventory Transfers",
        [
            new("تاریخ", "Date", TabularExportValueType.Date, 13, "LoadedDate"),
            new("محموله", "Shipment", TabularExportValueType.Text, 15, "ShipmentCode"),
            new("قرارداد", "Contract", TabularExportValueType.Text, 16, "ContractNumber"),
            new("جنس", "Product", TabularExportValueType.Text, 18, "ProductName"),
            new("مبدأ", "Source", TabularExportValueType.Text, 19, "SourceTerminalName", "SourceTankCode"),
            new("وسیله", "Vehicle", TabularExportValueType.Text, 17, "WagonNumber", "RwbNo"),
            new("نوع حمل", "Transport", TabularExportValueType.Text, 13, "TransportType"),
            new("مقدار MT", "Quantity MT", TabularExportValueType.Number, 14, "QuantityMt"),
            new("ارائه‌کننده", "Provider", TabularExportValueType.Text, 19, "ServiceProviderName", "OperationalAssetName"),
            new("وضعیت", "Status", TabularExportValueType.Text, 14, "Status")
        ], TabularExportSupport.FiltersFromQuery(Request), forceLandscape: true);
        return TabularExportSupport.File(this, format, document);
    }
}
