using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Models.TruckSettlements;
using PTGOilSystem.Web.Services;
using Xunit;

namespace PTGOilSystem.Web.Tests;

// «تسویهٔ کرایه موترها»: فقط تسویهٔ کرایه است — تخلیهٔ موجودی/فروش ندارد.
// این تست‌ها تضمین می‌کنند: (۱) leg با SettlementOnly بدون InventoryMovement تسویه می‌شود؛
// (۲) dispatch بدون DeliveryReceipt/حرکت موجودی/Delivered تسویه می‌شود؛ (۳) ردیف تسویه‌شده از لیست خارج می‌شود.
public class TruckSettlementsControllerTests
{
    [Fact]
    public async Task Settle_Leg_Books_Freight_Without_InventoryMovement_And_Marks_Settled()
    {
        await using var db = CreateDb();
        await SeedReferenceDataAsync(db);
        var leg = await SeedLoadedTruckLegAsync(db, quantityMt: 30m);
        var controller = BuildController(db);

        var result = await controller.Settle(new TruckSettlementIndexViewModel
        {
            Inputs =
            [
                new TruckSettlementRowInputViewModel
                {
                    Selected = true,
                    Kind = TruckSettlementSourceKind.Leg,
                    SourceId = leg.Id,
                    OperationDate = new DateTime(2026, 5, 5),
                    QuantityMt = 28m,               // وزن تخلیه ⇒ کسری = 30 − 28 = 2
                    FreightRateUsdPerMt = 5m,       // کرایه کلی = 5 × 30 = 150
                    ShortageRateUsd = 10m,          // خسارت کسری = 2 × 10 = 20 ⇒ کرایه نهایی = 130
                    FreightParty = "driver:1"
                }
            ]
        });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(controller.ModelState.IsValid);

        var reloaded = await db.InventoryTransportLegs.SingleAsync(l => l.Id == leg.Id);
        Assert.True(reloaded.IsFreightSettled);
        Assert.Equal(new DateTime(2026, 5, 5), reloaded.FreightSettledDate);
        // حمل هنوز تخلیه نشده — بار برای مرحلهٔ بعدی می‌ماند (کسری از باقیمانده کم شده).
        Assert.Equal(InventoryTransportLegStatus.Loaded, reloaded.Status);

        var receipt = await db.InventoryTransportReceipts.SingleAsync();
        Assert.Equal(0m, receipt.ReceivedQuantityMt);
        Assert.Equal(2m, receipt.ShortageQuantityMt);
        Assert.Null(receipt.InventoryMovementId);
        Assert.DoesNotContain(
            await db.InventoryMovements.ToListAsync(),
            m => m.ReferenceDocument != null && m.ReferenceDocument.StartsWith("TRANSPORT-RECEIPT:"));

        var expense = await db.ExpenseTransactions.Include(e => e.ExpenseType).SingleAsync();
        Assert.Equal("TRANSPORT-RECEIPT-FREIGHT", expense.ExpenseType?.Code);
        Assert.Equal(1, expense.DriverId);
        Assert.Equal(130m, expense.AmountUsd);

        var ledger = await db.LedgerEntries.SingleAsync(l => l.SourceType == "Expense");
        Assert.Equal(LedgerSide.Credit, ledger.Side);
        Assert.Equal(1, ledger.DriverId);
        Assert.Equal(130m, ledger.AmountUsd);
    }

    [Fact]
    public async Task Settle_Dispatch_Books_Freight_Without_Discharge_And_Marks_Settled()
    {
        await using var db = CreateDb();
        await SeedReferenceDataAsync(db);
        var dispatch = await SeedLoadedDispatchAsync(db, loadedMt: 30m);
        var controller = BuildController(db);

        var result = await controller.Settle(new TruckSettlementIndexViewModel
        {
            Inputs =
            [
                new TruckSettlementRowInputViewModel
                {
                    Selected = true,
                    Kind = TruckSettlementSourceKind.Dispatch,
                    SourceId = dispatch.Id,
                    OperationDate = new DateTime(2026, 5, 6),
                    QuantityMt = 28m,
                    FreightRateUsdPerMt = 5m,
                    ShortageRateUsd = 10m,
                    FreightParty = "driver:1"
                }
            ]
        });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(controller.ModelState.IsValid);

        var reloaded = await db.TruckDispatches.SingleAsync(d => d.Id == dispatch.Id);
        Assert.True(reloaded.IsFreightSettled);
        Assert.Equal(new DateTime(2026, 5, 6), reloaded.FreightSettledDate);
        Assert.Equal(DispatchStatus.Loaded, reloaded.Status);       // تخلیه نشده — Status دست‌نخورده
        Assert.Equal(28m, reloaded.DischargedQuantityMt);           // وزن مؤثر = وزن تخلیه، نه بارگیری
        Assert.Equal(2m, reloaded.ShortageMt);
        Assert.Equal(130m, reloaded.FreightPayableUsd);

        Assert.Empty(await db.DeliveryReceipts.ToListAsync());
        Assert.DoesNotContain(
            await db.InventoryMovements.ToListAsync(),
            m => m.ReferenceDocument != null && m.ReferenceDocument.StartsWith("TRUCK-UNLOAD:"));

        var expense = await db.ExpenseTransactions.Include(e => e.ExpenseType).SingleAsync();
        Assert.Equal("TRUCK-DISPATCH-FREIGHT", expense.ExpenseType?.Code);
        Assert.Equal(1, expense.DriverId);
        Assert.Equal(130m, expense.AmountUsd);
    }

    [Fact]
    public async Task Index_Excludes_FreightSettled_Rows()
    {
        await using var db = CreateDb();
        await SeedReferenceDataAsync(db);
        var leg = await SeedLoadedTruckLegAsync(db, quantityMt: 30m);

        var before = Assert.IsType<TruckSettlementIndexViewModel>(
            Assert.IsType<ViewResult>(await BuildController(db).Index(null, null)).Model);
        Assert.Contains(before.Rows, r => r.Kind == TruckSettlementSourceKind.Leg && r.SourceId == leg.Id);

        leg.IsFreightSettled = true;
        leg.FreightSettledDate = new DateTime(2026, 5, 5);
        await db.SaveChangesAsync();

        var after = Assert.IsType<TruckSettlementIndexViewModel>(
            Assert.IsType<ViewResult>(await BuildController(db).Index(null, null)).Model);
        Assert.DoesNotContain(after.Rows, r => r.Kind == TruckSettlementSourceKind.Leg && r.SourceId == leg.Id);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static TruckSettlementsController BuildController(ApplicationDbContext db)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = "ptg-ui-lang=fa";

        return new(
            db,
            new CurrencyConversionService(new PricingService(db)),
            InventoryLineageWriterFactory.Disabled(db),
            new LossEventWorkflowService(db, new StockService(db), new AuditService(db)))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new InMemoryTempDataProvider())
        };
    }

    private static async Task SeedReferenceDataAsync(ApplicationDbContext db)
    {
        db.Companies.Add(new Company { Id = 1, Code = "PTG", Name = "PTG" });
        db.Suppliers.Add(new Supplier { Id = 1, Name = "Supplier A" });
        db.Products.Add(new Product { Id = 1, Code = "GO", Name = "Gas Oil" });
        db.Trucks.Add(new Truck { Id = 1, PlateNumber = "TRK-1" });
        db.Drivers.Add(new Driver { Id = 1, FullName = "Driver A", IsActive = true });
        db.Terminals.AddRange(
            new Terminal { Id = 1, Code = "SRC", Name = "Source Terminal" },
            new Terminal { Id = 2, Code = "DST", Name = "Destination Terminal" });
        db.Contracts.Add(new Contract
        {
            Id = 1,
            ContractNumber = "PUR-001",
            ContractType = ContractType.Purchase,
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 1,
            ContractDate = new DateTime(2026, 5, 1),
            QuantityMt = 100m,
            PricingMethod = PricingMethod.Fixed,
            UnitPriceUsd = 500m
        });
        await db.SaveChangesAsync();
    }

    private static async Task<InventoryTransportLeg> SeedLoadedTruckLegAsync(ApplicationDbContext db, decimal quantityMt)
    {
        var leg = new InventoryTransportLeg
        {
            Id = 1,
            SourcePurchaseContractId = 1,
            ProductId = 1,
            SourceTerminalId = 1,
            DestinationTerminalId = 2,
            TransportType = LoadingTransportType.Truck,
            TruckId = 1,
            DriverId = 1,
            LoadedDate = new DateTime(2026, 5, 2),
            QuantityMt = quantityMt,
            Status = InventoryTransportLegStatus.Loaded
        };
        db.InventoryTransportLegs.Add(leg);
        await db.SaveChangesAsync();
        return leg;
    }

    private static async Task<TruckDispatch> SeedLoadedDispatchAsync(ApplicationDbContext db, decimal loadedMt)
    {
        var dispatch = new TruckDispatch
        {
            Id = 1,
            ContractId = 1,
            ProductId = 1,
            TruckId = 1,
            DriverId = 1,
            DispatchDate = new DateTime(2026, 5, 2),
            Status = DispatchStatus.Loaded,
            LoadedQuantityMt = loadedMt
        };
        db.TruckDispatches.Add(dispatch);
        await db.SaveChangesAsync();
        return dispatch;
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        private IDictionary<string, object> _data = new Dictionary<string, object>();

        public IDictionary<string, object> LoadTempData(HttpContext context) => _data;

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
            => _data = new Dictionary<string, object>(values);
    }
}
