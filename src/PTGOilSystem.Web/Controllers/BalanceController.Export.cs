using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PTGOilSystem.Web.Infrastructure.RateLimiting;
using PTGOilSystem.Web.Models.Balance;
using PTGOilSystem.Web.Services.Exports;

namespace PTGOilSystem.Web.Controllers;

public partial class BalanceController
{
    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> ContractsExport(string? format, [FromQuery] ContractsBalanceFilterViewModel? filter = null)
    {
        var model = await GetViewModelAsync<ContractsBalanceViewModel>(Contracts(filter, page: 0));
        var items = model.Items.ToList();
        return TabularExportSupport.File(this, format, new TabularExportDocument
        {
            FileNameStem = "PTG_Contracts_Balance",
            TitleFa = "مانده قراردادها",
            TitleEn = "Contracts Balance",
            KnownRowCount = items.Count,
            ForceLandscape = true,
            Columns =
            [
                new("قرارداد", "Contract", Width: 18), new("نوع", "Type", Width: 14), new("وضعیت", "Status", Width: 13),
                new("مشتری", "Customer", Width: 20), new("تأمین‌کننده", "Supplier", Width: 20), new("جنس", "Product", Width: 16),
                new("واحد", "Unit", Width: 12), new("مقدار MT", "Quantity MT", TabularExportValueType.Number, 15),
                new("محموله‌ها", "Shipments", TabularExportValueType.Integer, 12), new("فروش USD", "Sales USD", TabularExportValueType.Number, 16),
                new("مصارف USD", "Expenses USD", TabularExportValueType.Number, 16), new("اسناد دفتر", "Ledger entries", TabularExportValueType.Integer, 13),
                new("مانده USD", "Balance USD", TabularExportValueType.Number, 17)
            ],
            Rows = items.Select(item => new TabularExportRow(
            [
                TabularExportCell.Text(item.ContractNumber), TabularExportCell.Text(item.ContractTypeName), TabularExportCell.Text(item.StatusName),
                TabularExportCell.Text(item.CustomerName), TabularExportCell.Text(item.SupplierName), TabularExportCell.Text(item.ProductName),
                TabularExportCell.Text(item.ContractUnitText), TabularExportCell.Number(item.QuantityMt), TabularExportCell.Integer(item.ShipmentCount),
                TabularExportCell.Number(item.TotalSalesUsd), TabularExportCell.Number(item.TotalExpensesUsd), TabularExportCell.Integer(item.RelatedLedgerCount),
                TabularExportCell.Number(item.BaseBalanceUsd)
            ]))
        });
    }

    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> CustomersExport(string? format, [FromQuery] CustomersBalanceFilterViewModel? filter = null)
    {
        var model = await GetViewModelAsync<CustomersBalanceViewModel>(Customers(filter, page: 0));
        return TabularExportSupport.File(this, format, BuildPartyBalanceDocument(
            "PTG_Customers_Balance", "مانده مشتریان", "Customers Balance", "مشتری", "Customer",
            model.Items.Select(item => (item.Name, item.Country, item.RelatedContractsCount, item.ActiveContractsCount,
                item.ClosedContractsCount, item.TotalSalesUsd, item.TotalExpensesUsd, item.RelatedLedgerCount, item.BaseBalanceUsd))));
    }

    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> SuppliersExport(string? format, [FromQuery] SuppliersBalanceFilterViewModel? filter = null)
    {
        var model = await GetViewModelAsync<SuppliersBalanceViewModel>(Suppliers(filter, page: 0));
        return TabularExportSupport.File(this, format, BuildPartyBalanceDocument(
            "PTG_Suppliers_Balance", "مانده تأمین‌کنندگان", "Suppliers Balance", "تأمین‌کننده", "Supplier",
            model.Items.Select(item => (item.Name, item.Country, item.RelatedContractsCount, item.ActiveContractsCount,
                item.ClosedContractsCount, item.TotalSalesUsd, item.TotalExpensesUsd, item.RelatedLedgerCount, item.BaseBalanceUsd))));
    }

    private static TabularExportDocument BuildPartyBalanceDocument(
        string fileNameStem,
        string titleFa,
        string titleEn,
        string partyTitleFa,
        string partyTitleEn,
        IEnumerable<(string Name, string? Country, int Contracts, int ActiveContracts, int ClosedContracts,
            decimal SalesUsd, decimal ExpensesUsd, int LedgerCount, decimal BalanceUsd)> source)
    {
        var items = source.ToList();
        return new TabularExportDocument
        {
            FileNameStem = fileNameStem,
            TitleFa = titleFa,
            TitleEn = titleEn,
            KnownRowCount = items.Count,
            ForceLandscape = true,
            Columns =
            [
                new(partyTitleFa, partyTitleEn, Width: 22), new("کشور", "Country", Width: 14),
                new("قراردادها", "Contracts", TabularExportValueType.Integer, 12),
                new("قرارداد فعال", "Active contracts", TabularExportValueType.Integer, 13),
                new("قرارداد بسته", "Closed contracts", TabularExportValueType.Integer, 13),
                new("فروش USD", "Sales USD", TabularExportValueType.Number, 16),
                new("مصارف USD", "Expenses USD", TabularExportValueType.Number, 16),
                new("اسناد دفتر", "Ledger entries", TabularExportValueType.Integer, 13),
                new("مانده USD", "Balance USD", TabularExportValueType.Number, 17)
            ],
            Rows = items.Select(item => new TabularExportRow(
            [
                TabularExportCell.Text(item.Name), TabularExportCell.Text(item.Country), TabularExportCell.Integer(item.Contracts),
                TabularExportCell.Integer(item.ActiveContracts), TabularExportCell.Integer(item.ClosedContracts),
                TabularExportCell.Number(item.SalesUsd), TabularExportCell.Number(item.ExpensesUsd),
                TabularExportCell.Integer(item.LedgerCount), TabularExportCell.Number(item.BalanceUsd)
            ]))
        };
    }

    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> ContractsCsv([FromQuery] ContractsBalanceFilterViewModel? filter = null)
    {
        var model = await GetViewModelAsync<ContractsBalanceViewModel>(Contracts(filter, page: 0));
        return CsvExportSupport.File(this, "contracts-balance.csv",
            ["Contract", "Type", "Status", "Customer", "Supplier", "Product", "Unit", "QuantityMt", "ShipmentCount", "SalesUsd", "ExpensesUsd", "LedgerCount", "BaseBalanceUsd"],
            model.Items.Select(i => new[]
            {
                i.ContractNumber, i.ContractTypeName, i.StatusName, i.CustomerName, i.SupplierName, i.ProductName,
                i.ContractUnitText, CsvExportSupport.Decimal(i.QuantityMt), i.ShipmentCount.ToString(), CsvExportSupport.Decimal(i.TotalSalesUsd),
                CsvExportSupport.Decimal(i.TotalExpensesUsd), i.RelatedLedgerCount.ToString(), CsvExportSupport.Decimal(i.BaseBalanceUsd)
            }));
    }

    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> CustomersCsv([FromQuery] CustomersBalanceFilterViewModel? filter = null)
    {
        var model = await GetViewModelAsync<CustomersBalanceViewModel>(Customers(filter, page: 0));
        return CsvExportSupport.File(this, "customers-balance.csv",
            ["Customer", "Country", "Contracts", "ActiveContracts", "ClosedContracts", "SalesUsd", "ExpensesUsd", "LedgerCount", "BaseBalanceUsd"],
            model.Items.Select(i => new[]
            {
                i.Name, i.Country, i.RelatedContractsCount.ToString(), i.ActiveContractsCount.ToString(),
                i.ClosedContractsCount.ToString(), CsvExportSupport.Decimal(i.TotalSalesUsd),
                CsvExportSupport.Decimal(i.TotalExpensesUsd), i.RelatedLedgerCount.ToString(), CsvExportSupport.Decimal(i.BaseBalanceUsd)
            }));
    }

    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> SuppliersCsv([FromQuery] SuppliersBalanceFilterViewModel? filter = null)
    {
        var model = await GetViewModelAsync<SuppliersBalanceViewModel>(Suppliers(filter, page: 0));
        return CsvExportSupport.File(this, "suppliers-balance.csv",
            ["Supplier", "Country", "Contracts", "ActiveContracts", "ClosedContracts", "SalesUsd", "ExpensesUsd", "LedgerCount", "BaseBalanceUsd"],
            model.Items.Select(i => new[]
            {
                i.Name, i.Country, i.RelatedContractsCount.ToString(), i.ActiveContractsCount.ToString(),
                i.ClosedContractsCount.ToString(), CsvExportSupport.Decimal(i.TotalSalesUsd),
                CsvExportSupport.Decimal(i.TotalExpensesUsd), i.RelatedLedgerCount.ToString(), CsvExportSupport.Decimal(i.BaseBalanceUsd)
            }));
    }

    private static async Task<T> GetViewModelAsync<T>(Task<IActionResult> actionTask)
    {
        var result = await actionTask;
        var view = (ViewResult)result;
        return (T)view.Model!;
    }
}
