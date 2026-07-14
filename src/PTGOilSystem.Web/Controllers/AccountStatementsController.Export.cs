using Microsoft.AspNetCore.Mvc;
using PTGOilSystem.Web.Models.AccountStatements;

namespace PTGOilSystem.Web.Controllers;

public partial class AccountStatementsController
{
    public async Task<IActionResult> Csv([FromQuery] AccountStatementFilterViewModel? filter = null)
    {
        filter ??= new AccountStatementFilterViewModel();
        NormalizeFilter(filter);

        var statementRows = await BuildStatementRowsAsync(filter);
        return CsvExportSupport.File(this, "account-statements.csv",
            ["Date", "Side", "SourceAmount", "SourceCurrency", "FxToUsd", "AmountUsd", "RunningBalanceUsd", "Reference", "SourceType", "SourceId", "Contract", "Customer", "Supplier", "Description"],
            statementRows.Items.Select(r => new[]
            {
                CsvExportSupport.Date(r.EntryDate),
                r.SideName,
                CsvExportSupport.Decimal(r.SourceAmount),
                r.SourceCurrencyCode,
                CsvExportSupport.Decimal(r.AppliedFxRateToUsd),
                CsvExportSupport.Decimal(r.AmountUsd),
                CsvExportSupport.Decimal(r.RunningBalanceUsd),
                r.Reference,
                r.SourceType,
                r.SourceId.ToString(),
                r.ContractNumber,
                r.CustomerName,
                r.SupplierName,
                r.Description
            }));
    }
}
