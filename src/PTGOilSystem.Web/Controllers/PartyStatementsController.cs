using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PTGOilSystem.Web.Infrastructure.RateLimiting;
using PTGOilSystem.Web.Models.PartyStatements;
using PTGOilSystem.Web.Services.PartyStatements;

namespace PTGOilSystem.Web.Controllers;

[Authorize]
public sealed class PartyStatementsController : Controller
{
    private readonly IPartyStatementReadService _statementService;

    public PartyStatementsController(IPartyStatementReadService statementService)
    {
        _statementService = statementService;
    }

    [HttpGet("Customers/{id:int}/Statement")]
    public Task<IActionResult> Customer(int id, [FromQuery] PartyStatementFilter filter, bool print = false, CancellationToken ct = default)
        => RenderAsync(PartyStatementPartyType.Customer, id, filter, print, ct);

    [HttpGet("Suppliers/{id:int}/Statement")]
    public Task<IActionResult> Supplier(int id, [FromQuery] PartyStatementFilter filter, bool print = false, CancellationToken ct = default)
        => RenderAsync(PartyStatementPartyType.Supplier, id, filter, print, ct);

    [HttpGet("ServiceProviders/{id:int}/Statement")]
    public Task<IActionResult> ServiceProvider(int id, [FromQuery] PartyStatementFilter filter, bool print = false, CancellationToken ct = default)
        => RenderAsync(PartyStatementPartyType.ServiceProvider, id, filter, print, ct);

    [HttpGet("Sarrafs/{id:int}/Statement")]
    public Task<IActionResult> Sarraf(int id, [FromQuery] PartyStatementFilter filter, bool print = false, CancellationToken ct = default)
        => RenderAsync(PartyStatementPartyType.Sarraf, id, filter, print, ct);

    [HttpGet("Employees/{id:int}/Statement")]
    public Task<IActionResult> Employee(int id, [FromQuery] PartyStatementFilter filter, bool print = false, CancellationToken ct = default)
        => RenderAsync(PartyStatementPartyType.Employee, id, filter, print, ct);

    [HttpGet("Partners/{id:int}/Statement")]
    public Task<IActionResult> Partner(int id, [FromQuery] PartyStatementFilter filter, bool print = false, CancellationToken ct = default)
        => RenderAsync(PartyStatementPartyType.Partner, id, filter, print, ct);

    [HttpGet("Drivers/{id:int}/Statement")]
    public Task<IActionResult> Driver(int id, [FromQuery] PartyStatementFilter filter, bool print = false, CancellationToken ct = default)
        => RenderAsync(PartyStatementPartyType.Driver, id, filter, print, ct);

    [HttpGet("Companies/{id:int}/Statement")]
    public Task<IActionResult> Company(int id, [FromQuery] PartyStatementFilter filter, bool print = false, CancellationToken ct = default)
        => RenderAsync(PartyStatementPartyType.Company, id, filter, print, ct);

    [HttpGet("PartyStatements/{partyType}/{id:int}")]
    public Task<IActionResult> Document(PartyStatementPartyType partyType, int id, [FromQuery] PartyStatementFilter filter, bool print = false, CancellationToken ct = default)
        => RenderAsync(partyType, id, filter, print, ct);

    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    [HttpGet("PartyStatements/{partyType}/{id:int}/Csv")]
    public async Task<IActionResult> Csv(
        PartyStatementPartyType partyType,
        int id,
        [FromQuery] PartyStatementFilter filter,
        CancellationToken ct = default)
    {
        PartyStatementResult statement;
        try
        {
            statement = await _statementService.GetStatementAsync(new PartyRef(partyType, id, filter.CompanyId), filter, ct);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var headers = BuildCsvHeaders(statement.ColumnOptions);
        var rows = statement.Rows.Select(row => BuildCsvRow(row, statement.ColumnOptions));
        var fileName = $"statement-{partyType.ToString().ToLowerInvariant()}-{id}-{DateTime.UtcNow:yyyyMMdd}.csv";
        return CsvExportSupport.File(this, fileName, headers, rows);
    }

    private async Task<IActionResult> RenderAsync(
        PartyStatementPartyType partyType,
        int id,
        PartyStatementFilter filter,
        bool print,
        CancellationToken ct)
    {
        try
        {
            var statement = await _statementService.GetStatementAsync(new PartyRef(partyType, id, filter.CompanyId), filter, ct);
            return View("Document", new PartyStatementViewModel
            {
                Statement = statement,
                Filter = filter,
                IsPrintMode = print
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var fallback = new PartyStatementFilter
            {
                FromDate = null,
                ToDate = filter.ToDate,
                ContractId = filter.ContractId,
                CompanyId = filter.CompanyId,
                CurrencyCode = filter.CurrencyCode,
                IncludeOperationalColumns = filter.IncludeOperationalColumns
            };
            var statement = await _statementService.GetStatementAsync(new PartyRef(partyType, id, fallback.CompanyId), fallback, ct);
            return View("Document", new PartyStatementViewModel { Statement = statement, Filter = fallback, IsPrintMode = print });
        }
    }

    private static string[] BuildCsvHeaders(PartyStatementColumnOptions columns)
    {
        var headers = new List<string> { "No", "Date", "Reference", "Description" };
        if (columns.ShowRub) headers.Add("RUB");
        if (columns.ShowAed) headers.Add("AED");
        if (columns.ShowOriginalAmount) headers.AddRange(["OriginalAmount", "Currency"]);
        if (columns.ShowFxRate) headers.Add("ExchangeRate");
        if (columns.ShowQuantity) headers.Add("M-Tone");
        if (columns.ShowPlatts) headers.Add("Platts");
        if (columns.ShowPremiumOrDiscount) headers.Add("PremiumDiscount");
        if (columns.ShowUnitPrice) headers.Add("UnitPrice");
        headers.AddRange(["DebitUsd", "CreditUsd", "BalanceUsd"]);
        return headers.ToArray();
    }

    private static string?[] BuildCsvRow(PartyStatementRow row, PartyStatementColumnOptions columns)
    {
        var values = new List<string?>
        {
            row.IsOpeningBalance ? "" : row.Sequence.ToString(),
            CsvExportSupport.Date(row.Date),
            row.Reference,
            row.Description
        };
        if (columns.ShowRub) values.Add(IsCurrency(row, "RUB") ? CsvExportSupport.Decimal(row.OriginalAmount) : "");
        if (columns.ShowAed) values.Add(IsCurrency(row, "AED") ? CsvExportSupport.Decimal(row.OriginalAmount) : "");
        if (columns.ShowOriginalAmount)
        {
            values.Add(row.OriginalCurrency is not "USD" and not "RUB" and not "AED" ? CsvExportSupport.Decimal(row.OriginalAmount) : "");
            values.Add(row.OriginalCurrency is not "USD" and not "RUB" and not "AED" ? row.OriginalCurrency : "");
        }
        if (columns.ShowFxRate) values.Add(row.FxRateDisplay ?? (row.OriginalCurrency == "USD" ? "1" : "Exchange rate not recorded"));
        if (columns.ShowQuantity) values.Add(CsvExportSupport.Decimal(row.Quantity));
        if (columns.ShowPlatts) values.Add(CsvExportSupport.Decimal(row.PlattsPrice));
        if (columns.ShowPremiumOrDiscount) values.Add(CsvExportSupport.Decimal(row.PremiumOrDiscount));
        if (columns.ShowUnitPrice) values.Add(CsvExportSupport.Decimal(row.UnitPrice));
        values.Add(CsvExportSupport.Decimal(row.DebitBase));
        values.Add(CsvExportSupport.Decimal(row.CreditBase));
        values.Add(CsvExportSupport.Decimal(row.RunningBalance));
        return values.ToArray();
    }

    private static bool IsCurrency(PartyStatementRow row, string currency)
        => string.Equals(row.OriginalCurrency, currency, StringComparison.OrdinalIgnoreCase);
}
