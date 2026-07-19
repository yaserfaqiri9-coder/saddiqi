using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PTGOilSystem.Web.Infrastructure.RateLimiting;
using PTGOilSystem.Web.Helpers;
using PTGOilSystem.Web.Services.Exports;

namespace PTGOilSystem.Web.Controllers;

public partial class ReconciliationController
{
    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> OpenContractsExport(string? format)
    {
        var model = await BuildOpenContractsAsync();
        var rows = model.Rows.ToList();
        return TabularExportSupport.File(this, format, new TabularExportDocument
        {
            FileNameStem = "PTG_Open_Contracts",
            TitleFa = "قراردادهای باز",
            TitleEn = "Open Contracts",
            KnownRowCount = rows.Count,
            ForceLandscape = true,
            Columns =
            [
                new("قرارداد", "Contract", Width: 17), new("جنس", "Product", Width: 15), new("واحد", "Unit", Width: 12),
                new("مقدار قرارداد MT", "Contract quantity MT", TabularExportValueType.Number, 16),
                new("بارگیری MT", "Loaded MT", TabularExportValueType.Number, 14), new("رسید MT", "Received MT", TabularExportValueType.Number, 14),
                new("فروش MT", "Sold MT", TabularExportValueType.Number, 14), new("باقیمانده MT", "Remaining MT", TabularExportValueType.Number, 15),
                new("وضعیت", "Status", Width: 14)
            ],
            Rows = rows.Select(row => new TabularExportRow(
            [
                TabularExportCell.Text(row.ContractNumber), TabularExportCell.Text(row.ProductName), TabularExportCell.Text(row.ContractUnitText),
                TabularExportCell.Number(row.ContractQuantityMt), TabularExportCell.Number(row.LoadedQuantityMt),
                TabularExportCell.Number(row.ReceivedQuantityMt), TabularExportCell.Number(row.SoldQuantityMt),
                TabularExportCell.Number(row.RemainingQuantityMt), TabularExportCell.Text(row.Status)
            ]))
        });
    }

    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> OpenShipmentsExport(string? format)
    {
        var model = await BuildOpenShipmentsAsync();
        var rows = model.ShipmentsWithoutSales.Select(row =>
                (KindFa: "محموله بدون فروش", KindEn: "Shipment without sales", Reference: (string?)row.ShipmentCode,
                    ContractNumber: (string?)row.ContractNumber, ContractUnitText: row.ContractUnitText,
                    Quantity: row.QuantityMt, Status: row.Status, Reason: (string?)null))
            .Concat(model.ShipmentsWithoutExpenses.Select(row =>
                (KindFa: "محموله بدون مصرف", KindEn: "Shipment without expenses", Reference: (string?)row.ShipmentCode,
                    ContractNumber: (string?)row.ContractNumber, ContractUnitText: row.ContractUnitText,
                    Quantity: row.QuantityMt, Status: row.Status, Reason: (string?)null)))
            .Concat(model.DispatchesWithoutReceipt.Select(row =>
                (KindFa: "ارسال بدون رسید", KindEn: "Dispatch without receipt", Reference: (string?)$"#{row.DispatchId}",
                    ContractNumber: (string?)row.ContractNumber, ContractUnitText: row.ContractUnitText,
                    Quantity: row.LoadedQuantityMt, Status: row.Status, Reason: (string?)row.Reason)))
            .ToList();

        return TabularExportSupport.File(this, format, new TabularExportDocument
        {
            FileNameStem = "PTG_Open_Shipments",
            TitleFa = "محموله‌های باز",
            TitleEn = "Open Shipments",
            KnownRowCount = rows.Count,
            Columns =
            [
                new("نوع", "Kind", Width: 22), new("مرجع", "Reference", Width: 16), new("قرارداد", "Contract", Width: 17),
                new("واحد", "Unit", Width: 12), new("مقدار MT", "Quantity MT", TabularExportValueType.Number, 15),
                new("وضعیت", "Status", Width: 14), new("دلیل", "Reason", Width: 28, Wrap: true)
            ],
            Rows = rows.Select(row => new TabularExportRow(
            [
                TabularExportCell.Text(UiText.IsEn(HttpContext) ? row.KindEn : row.KindFa), TabularExportCell.Text(row.Reference),
                TabularExportCell.Text(row.ContractNumber), TabularExportCell.Text(row.ContractUnitText), TabularExportCell.Number(row.Quantity),
                TabularExportCell.Text(row.Status), TabularExportCell.Text(row.Reason)
            ]))
        });
    }

    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> MissingLedgerExport(string? format)
    {
        var model = await BuildMissingLedgerAsync();
        var rows = model.SalesWithoutLedger.Concat(model.ExpensesWithoutLedger).Concat(model.PaymentsWithoutLedger).ToList();
        return TabularExportSupport.File(this, format, new TabularExportDocument
        {
            FileNameStem = "PTG_Missing_Ledger",
            TitleFa = "اسناد فاقد ثبت دفتر کل",
            TitleEn = "Missing Ledger Entries",
            KnownRowCount = rows.Count,
            Columns =
            [
                new("نوع منبع", "Source type", Width: 18), new("شناسه منبع", "Source ID", TabularExportValueType.Integer, 12),
                new("تاریخ", "Date", TabularExportValueType.Date, 13), new("مرجع", "Reference", Width: 18),
                new("مبلغ USD", "Amount USD", TabularExportValueType.Number, 16), new("وضعیت", "Status", Width: 15)
            ],
            Rows = rows.Select(row => new TabularExportRow(
            [
                TabularExportCell.Text(row.SourceType), TabularExportCell.Integer(row.SourceId), TabularExportCell.Date(row.Date),
                TabularExportCell.Text(row.Reference), TabularExportCell.Number(row.AmountUsd), TabularExportCell.Text(row.Status)
            ]))
        });
    }

    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> NonZeroBalancesExport(string? format)
    {
        var model = await BuildNonZeroBalancesAsync();
        var rows = model.ContractBalances.Concat(model.CustomerBalances).Concat(model.SupplierBalances).ToList();
        return TabularExportSupport.File(this, format, new TabularExportDocument
        {
            FileNameStem = "PTG_Non_Zero_Balances",
            TitleFa = "مانده‌های غیرصفر",
            TitleEn = "Non-zero Balances",
            KnownRowCount = rows.Count,
            Columns =
            [
                new("نوع", "Entity type", Width: 15), new("شناسه", "Entity ID", TabularExportValueType.Integer, 11),
                new("نام", "Name", Width: 22), new("بدهکار USD", "Debit USD", TabularExportValueType.Number, 16),
                new("بستانکار USD", "Credit USD", TabularExportValueType.Number, 16),
                new("مانده USD", "Balance USD", TabularExportValueType.Number, 16), new("وضعیت", "Status", Width: 14)
            ],
            Rows = rows.Select(row => new TabularExportRow(
            [
                TabularExportCell.Text(row.EntityType), TabularExportCell.Integer(row.EntityId), TabularExportCell.Text(row.Name),
                TabularExportCell.Number(row.DebitUsd), TabularExportCell.Number(row.CreditUsd),
                TabularExportCell.Number(row.BalanceUsd), TabularExportCell.Text(row.Status)
            ]))
        });
    }

    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> OpenContractsCsv()
    {
        var model = await BuildOpenContractsAsync();
        return CsvExportSupport.File(this, "open-contracts.csv",
            ["Contract", "Product", "ContractUnit", "ContractQuantityMt", "LoadedQuantityMt", "ReceivedQuantityMt", "SoldQuantityMt", "RemainingQuantityMt", "Status"],
            model.Rows.Select(r => new[]
            {
                r.ContractNumber, r.ProductName, r.ContractUnitText, CsvExportSupport.Decimal(r.ContractQuantityMt),
                CsvExportSupport.Decimal(r.LoadedQuantityMt), CsvExportSupport.Decimal(r.ReceivedQuantityMt),
                CsvExportSupport.Decimal(r.SoldQuantityMt), CsvExportSupport.Decimal(r.RemainingQuantityMt), r.Status
            }));
    }

    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> OpenShipmentsCsv()
    {
        var model = await BuildOpenShipmentsAsync();
        var rows = model.ShipmentsWithoutSales.Select(r => new[]
            {
                "ShipmentWithoutSales", r.ShipmentCode, r.ContractNumber, r.ContractUnitText, CsvExportSupport.Decimal(r.QuantityMt), r.Status, ""
            })
            .Concat(model.ShipmentsWithoutExpenses.Select(r => new[]
            {
                "ShipmentWithoutExpenses", r.ShipmentCode, r.ContractNumber, r.ContractUnitText, CsvExportSupport.Decimal(r.QuantityMt), r.Status, ""
            }))
            .Concat(model.DispatchesWithoutReceipt.Select(r => new[]
            {
                "DispatchWithoutReceipt", "#" + r.DispatchId, r.ContractNumber, r.ContractUnitText, CsvExportSupport.Decimal(r.LoadedQuantityMt), r.Status, r.Reason
            }));

        return CsvExportSupport.File(this, "open-shipments.csv",
            ["Kind", "Reference", "Contract", "ContractUnit", "QuantityMt", "Status", "Reason"],
            rows);
    }

    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> MissingLedgerCsv()
    {
        var model = await BuildMissingLedgerAsync();
        var rows = model.SalesWithoutLedger
            .Concat(model.ExpensesWithoutLedger)
            .Concat(model.PaymentsWithoutLedger)
            .Select(r => new[]
            {
                r.SourceType, r.SourceId.ToString(), CsvExportSupport.Date(r.Date), r.Reference,
                CsvExportSupport.Decimal(r.AmountUsd), r.Status
            });

        return CsvExportSupport.File(this, "missing-ledger.csv",
            ["SourceType", "SourceId", "Date", "Reference", "AmountUsd", "Status"],
            rows);
    }

    [EnableRateLimiting(RateLimitPolicies.CsvExport)]
    public async Task<IActionResult> NonZeroBalancesCsv()
    {
        var model = await BuildNonZeroBalancesAsync();
        var rows = model.ContractBalances.Concat(model.CustomerBalances).Concat(model.SupplierBalances)
            .Select(r => new[]
            {
                r.EntityType, r.EntityId.ToString(), r.Name, CsvExportSupport.Decimal(r.DebitUsd),
                CsvExportSupport.Decimal(r.CreditUsd), CsvExportSupport.Decimal(r.BalanceUsd), r.Status
            });

        return CsvExportSupport.File(this, "non-zero-balances.csv",
            ["EntityType", "EntityId", "Name", "DebitUsd", "CreditUsd", "BalanceUsd", "Status"],
            rows);
    }
}
