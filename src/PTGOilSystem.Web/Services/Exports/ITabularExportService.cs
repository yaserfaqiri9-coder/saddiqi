namespace PTGOilSystem.Web.Services.Exports;

public interface ITabularExportService
{
    int GetRowLimit(TabularExportFormat format);

    Task WriteAsync(
        TabularExportDocument document,
        TabularExportFormat format,
        bool isEnglish,
        Stream destination,
        CancellationToken cancellationToken);
}

