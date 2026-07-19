using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PTGOilSystem.Web.Infrastructure.RateLimiting;
using PTGOilSystem.Web.Models.Dispatch;
using PTGOilSystem.Web.Services.Exports;

namespace PTGOilSystem.Web.Controllers;

public partial class DispatchController
{
    [HttpGet, EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> Export(string? format, [FromQuery] DispatchIndexFilterViewModel? filter = null)
    {
        var view = (ViewResult)await Index(filter, page: 0);
        var document = TabularExportAuto.Build(view.Model!, "PTG_Truck_Dispatches", "ارسال موترها", "Truck Dispatches",
        [
            new("تاریخ", "Date", TabularExportValueType.Date, 13, "DispatchDate"),
            new("نمبر موتر", "Truck no.", TabularExportValueType.Text, 16, "TruckPlateNumber"),
            new("راننده", "Driver", TabularExportValueType.Text, 18, "DriverName"),
            new("قرارداد", "Contract", TabularExportValueType.Text, 16, "ContractNumber"),
            new("جنس", "Product", TabularExportValueType.Text, 18, "ProductName"),
            new("مقصد", "Destination", TabularExportValueType.Text, 18, "DestinationName"),
            new("مقدار MT", "Quantity MT", TabularExportValueType.Number, 14, "LoadedQuantityMt"),
            new("کسری MT", "Shortage MT", TabularExportValueType.Number, 14, "ShortageMt"),
            new("کرایه USD", "Freight USD", TabularExportValueType.Number, 15, "FreightCostUsd"),
            new("شرکت/دارایی", "Provider/asset", TabularExportValueType.Text, 20, "ServiceProviderName", "OperationalAssetName"),
            new("وضعیت", "Status", TabularExportValueType.Text, 14, "StatusName")
        ], TabularExportSupport.FiltersFromQuery(Request), forceLandscape: true);
        return TabularExportSupport.File(this, format, document);
    }
}
