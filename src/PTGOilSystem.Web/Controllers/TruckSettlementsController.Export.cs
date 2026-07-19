using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PTGOilSystem.Web.Infrastructure.RateLimiting;
using PTGOilSystem.Web.Models.TruckSettlements;
using PTGOilSystem.Web.Services.Exports;

namespace PTGOilSystem.Web.Controllers;

public partial class TruckSettlementsController
{
    [HttpGet, EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> Export(string? format, string? q, TruckSettlementSourceKind? kind)
    {
        var model = await BuildIndexAsync(preserveInputs: null, q, kind);
        var document = TabularExportAuto.Build(model, "PTG_Truck_Settlements", "تسویه موتر و واگن", "Truck Settlements",
        [
            new("تاریخ", "Date", TabularExportValueType.Date, 13, "Date"),
            new("نوع", "Type", TabularExportValueType.Text, 18, "TypeLabel"),
            new("نمبر وسیله", "Vehicle no.", TabularExportValueType.Text, 16, "VehicleNumber"),
            new("راننده", "Driver", TabularExportValueType.Text, 18, "DriverName"),
            new("قرارداد", "Contract", TabularExportValueType.Text, 16, "ContractNumber"),
            new("جنس", "Product", TabularExportValueType.Text, 18, "ProductName"),
            new("مسیر", "Route", TabularExportValueType.Text, 22, "SourceName", "DestinationName"),
            new("باقیمانده MT", "Remaining MT", TabularExportValueType.Number, 15, "RemainingQuantityMt")
        ], TabularExportSupport.FiltersFromQuery(Request), forceLandscape: true);
        return TabularExportSupport.File(this, format, document);
    }
}
