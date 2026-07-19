namespace PTGOilSystem.Web.Services.Exports;

public sealed class TabularExportOptions
{
    public const string SectionName = "Exports";

    public int ExcelMaxRows { get; set; } = 50_000;
    public int PdfMaxRows { get; set; } = 10_000;
    public string CompanyNameFa { get; set; } = "PTG Oil System";
    public string CompanyNameEn { get; set; } = "PTG Oil System";
    public string QuestPdfLicense { get; set; } = "Community";
}

