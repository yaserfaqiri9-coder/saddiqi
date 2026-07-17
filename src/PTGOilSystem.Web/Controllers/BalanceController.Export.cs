using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PTGOilSystem.Web.Infrastructure.RateLimiting;
using PTGOilSystem.Web.Models.Balance;

namespace PTGOilSystem.Web.Controllers;

public partial class BalanceController
{
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
