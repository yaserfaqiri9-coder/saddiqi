using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PTGOilSystem.Web.Infrastructure.RateLimiting;
using PTGOilSystem.Web.Services.Exports;

namespace PTGOilSystem.Web.Controllers;

public partial class CustomsDeclarationsController
{
    [HttpGet, EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> Export(string? format, int? loadingRegisterId = null, int? transportLegId = null,
        int? truckDispatchId = null, string? q = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var view = (ViewResult)await Index(loadingRegisterId, transportLegId, truckDispatchId, q, fromDate, toDate, page: 0);
        var document = TabularExportAuto.Build(view.Model!, "PTG_Customs_Declarations", "اظهارنامه‌های گمرکی", "Customs Declarations",
        [
            new("تاریخ", "Date", TabularExportValueType.Date, 13, "DeclarationDate"),
            new("واگن/موتر", "Wagon/truck", TabularExportValueType.Text, 16, "WagonOrTruckNumber"),
            new("قرارداد", "Contract", TabularExportValueType.Text, 16, "ContractNumber"),
            new("جنس", "Product", TabularExportValueType.Text, 18, "ProductName"),
            new("منبع", "Source", TabularExportValueType.Text, 18, "SourceLabel"),
            new("مرجع", "Reference", TabularExportValueType.Text, 18, "DeclarationReference"),
            new("وزن MT", "Weight MT", TabularExportValueType.Number, 14, "ConsignmentWeightMt"),
            new("مجموع AFN", "Total AFN", TabularExportValueType.Number, 15, "TotalAfn"),
            new("مجموع USD", "Total USD", TabularExportValueType.Number, 15, "TotalUsd")
        ], TabularExportSupport.FiltersFromQuery(Request), forceLandscape: true);
        return TabularExportSupport.File(this, format, document);
    }
}
