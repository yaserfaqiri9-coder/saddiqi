using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Models.LossEvents;
using PTGOilSystem.Web.Models.Reconciliation;
using PTGOilSystem.Web.Models.Reports;
using PTGOilSystem.Web.Models.StorageTanks;
using PTGOilSystem.Web.Services;
using PTGOilSystem.Web.Services.DeleteSafety;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class DeferredTankSettlementTests
{
    // A) سرویس: مرحلهٔ TankFinalSettlement با AffectsInventory باید Out بسازد و موجودی را کم کند.
    [Fact]
    public async Task LossWorkflow_TankFinalSettlement_Creates_OutMovement_And_Reduces_Stock()
    {
        await using var db = NewDb();
        SeedTankWithContract(db, contractId: 7, inQty: 100m);
        await db.SaveChangesAsync();

        var workflow = new LossEventWorkflowService(db, new StockService(db), new AuditService(db));
        var submission = new LossEventSubmission
        {
            Stage = LossEventStage.TankFinalSettlement,
            ProductId = 22,
            ContractId = 7,
            TerminalId = 10,
            StorageTankId = 5,
            EventDate = new DateTime(2026, 6, 1),
            ExpectedQuantityMt = 100m,
            ActualQuantityMt = 0m,
            ToleranceQuantityMt = 0m,
            AffectsInventory = true
        };

        var errors = new List<string>();
        await workflow.ValidateAsync(submission, (_, e) => errors.Add(e));
        Assert.Empty(errors);

        var result = await workflow.CreateAsync(submission);

        Assert.Equal(LossEventStage.TankFinalSettlement, result.LossEvent.Stage);
        Assert.Equal(100m, result.LossEvent.ChargeableLossMt);
        Assert.True(result.LossEvent.AffectsInventory);
        Assert.NotNull(result.InventoryMovement);
        Assert.Equal(MovementDirection.Out, result.InventoryMovement!.Direction);
        Assert.Equal(100m, result.InventoryMovement.QuantityMt);

        var remaining = await new StockService(db).GetFreeQuantityMtAsync(
            productId: 22, contractId: 7, storageTankId: 5);
        Assert.Equal(0m, remaining);
    }

    // B) تسویهٔ نهایی تک‌قراردادی با مخزن کاملاً خالی: ضایعه = موجودی همان قرارداد.
    [Fact]
    public async Task SettleFinal_SingleContract_FullyEmpty_Creates_Loss_Equal_To_Balance()
    {
        await using var db = NewDb();
        // In 100, Out 40 → balance 60
        SeedTankWithContract(db, contractId: 7, inQty: 100m, outQty: 40m);
        await db.SaveChangesAsync();

        var controller = BuildTanksController(db);
        var result = await controller.SettleFinal(new StorageTankSettlementViewModel
        {
            TankId = 5,
            EventDate = new DateTime(2026, 6, 1),
            ActualRemainingMt = 0m
        });

        Assert.IsType<RedirectToActionResult>(result);

        var loss = Assert.Single(await db.LossEvents.Where(e => e.Stage == LossEventStage.TankFinalSettlement).ToListAsync());
        Assert.Equal(7, loss.ContractId);
        Assert.Equal(60m, loss.ChargeableLossMt);
        Assert.True(loss.AffectsInventory);

        var remaining = await new StockService(db).GetFreeQuantityMtAsync(productId: 22, contractId: 7, storageTankId: 5);
        Assert.Equal(0m, remaining);
    }

    // C) چند قرارداد + باقیماندهٔ جزئی: تقسیم نسبتی درست.
    [Fact]
    public async Task SettleFinal_MultiContract_PartialRemaining_Splits_Loss_Proportionally()
    {
        await using var db = NewDb();
        SeedReference(db);
        db.Contracts.Add(NewPurchaseContract(7, "PUR-7"));
        db.Contracts.Add(NewPurchaseContract(8, "PUR-8"));
        db.InventoryMovements.AddRange(
            InMovement(1, contractId: 7, qty: 60m),
            InMovement(2, contractId: 8, qty: 40m));
        await db.SaveChangesAsync();

        var controller = BuildTanksController(db);
        var result = await controller.SettleFinal(new StorageTankSettlementViewModel
        {
            TankId = 5,
            EventDate = new DateTime(2026, 6, 1),
            ActualRemainingMt = 20m // settleable=100 → totalLoss=80
        });

        Assert.IsType<RedirectToActionResult>(result);

        var losses = await db.LossEvents
            .Where(e => e.Stage == LossEventStage.TankFinalSettlement)
            .ToListAsync();
        Assert.Equal(2, losses.Count);
        // X: 60 - 20*0.6 = 48 ، Y: 40 - 20*0.4 = 32
        Assert.Equal(48m, losses.Single(l => l.ContractId == 7).ChargeableLossMt);
        Assert.Equal(32m, losses.Single(l => l.ContractId == 8).ChargeableLossMt);

        var stock = new StockService(db);
        Assert.Equal(12m, await stock.GetFreeQuantityMtAsync(productId: 22, contractId: 7, storageTankId: 5));
        Assert.Equal(8m, await stock.GetFreeQuantityMtAsync(productId: 22, contractId: 8, storageTankId: 5));
    }

    // C-2) تقسیم نسبتی باید ضایعه را با سطح قطعیت «تخمینی» علامت بزند.
    [Fact]
    public async Task SettleFinal_Proportional_Marks_Losses_As_Estimated()
    {
        await using var db = NewDb();
        SeedTankWithContract(db, contractId: 7, inQty: 100m);
        await db.SaveChangesAsync();

        var controller = BuildTanksController(db);
        var result = await controller.SettleFinal(new StorageTankSettlementViewModel
        {
            TankId = 5,
            AllocationMode = TankLossAllocationMode.Proportional,
            EventDate = new DateTime(2026, 6, 1),
            ActualRemainingMt = 0m
        });

        Assert.IsType<RedirectToActionResult>(result);
        var loss = Assert.Single(await db.LossEvents.Where(e => e.Stage == LossEventStage.TankFinalSettlement).ToListAsync());
        Assert.Equal(100m, loss.ChargeableLossMt);
        Assert.Equal(LossCertaintyLevel.Estimated, loss.LossCertainty);
    }

    // C-3) ورود دستی: ضایعهٔ هر قرارداد دقیقاً همان مقدار وارد‌شده و با سطح قطعیت «اندازه‌گیری‌شده».
    [Fact]
    public async Task SettleFinal_Manual_Uses_Entered_PerContract_Loss_And_Marks_Measured()
    {
        await using var db = NewDb();
        SeedReference(db);
        db.Contracts.Add(NewPurchaseContract(7, "PUR-7"));
        db.Contracts.Add(NewPurchaseContract(8, "PUR-8"));
        db.InventoryMovements.AddRange(
            InMovement(1, contractId: 7, qty: 60m),
            InMovement(2, contractId: 8, qty: 40m));
        await db.SaveChangesAsync();

        var controller = BuildTanksController(db);
        // مقدار واقعی اندازه‌گیری‌شده: قرارداد ۷ → ۵۰ ضایعه، قرارداد ۸ → ۱۰ ضایعه.
        var result = await controller.SettleFinal(new StorageTankSettlementViewModel
        {
            TankId = 5,
            AllocationMode = TankLossAllocationMode.Manual,
            EventDate = new DateTime(2026, 6, 1),
            ManualLosses =
            [
                new StorageTankSettlementManualLossInput { ContractId = 7, ProductId = 22, LossMt = 50m },
                new StorageTankSettlementManualLossInput { ContractId = 8, ProductId = 22, LossMt = 10m }
            ]
        });

        Assert.IsType<RedirectToActionResult>(result);

        var losses = await db.LossEvents
            .Where(e => e.Stage == LossEventStage.TankFinalSettlement)
            .ToListAsync();
        Assert.Equal(2, losses.Count);
        Assert.Equal(50m, losses.Single(l => l.ContractId == 7).ChargeableLossMt);
        Assert.Equal(10m, losses.Single(l => l.ContractId == 8).ChargeableLossMt);
        Assert.All(losses, l => Assert.Equal(LossCertaintyLevel.Measured, l.LossCertainty));

        var stock = new StockService(db);
        // باقیمانده پس از ضایعهٔ دستی: ۷ → ۱۰، ۸ → ۳۰.
        Assert.Equal(10m, await stock.GetFreeQuantityMtAsync(productId: 22, contractId: 7, storageTankId: 5));
        Assert.Equal(30m, await stock.GetFreeQuantityMtAsync(productId: 22, contractId: 8, storageTankId: 5));
    }

    // C-4) ورود دستی بیشتر از موجودی دفتری باید رد شود (بدون ساخت ضایعه).
    [Fact]
    public async Task SettleFinal_Manual_Rejects_Loss_Greater_Than_Book_Balance()
    {
        await using var db = NewDb();
        SeedTankWithContract(db, contractId: 7, inQty: 60m);
        await db.SaveChangesAsync();

        var controller = BuildTanksController(db);
        var result = await controller.SettleFinal(new StorageTankSettlementViewModel
        {
            TankId = 5,
            AllocationMode = TankLossAllocationMode.Manual,
            EventDate = new DateTime(2026, 6, 1),
            ManualLosses =
            [
                new StorageTankSettlementManualLossInput { ContractId = 7, ProductId = 22, LossMt = 80m }
            ]
        });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(await db.LossEvents.ToListAsync());
    }

    // D) P&L موقت: رسید Deferred با موجودی باقی در مخزن → HasPendingTankSettlement.
    [Fact]
    public async Task ContractPnl_Provisional_When_Deferred_Receipt_Still_In_Tank()
    {
        await using var db = NewDb();
        SeedReference(db);
        db.Contracts.Add(NewPurchaseContract(7, "PUR-7"));
        db.LoadingRegisters.Add(new LoadingRegister
        {
            Id = 1, ContractId = 7, ProductId = 22,
            LoadingDate = new DateTime(2026, 5, 1), LoadedQuantityMt = 100m, LoadingPriceUsd = 200m
        });
        db.LoadingReceipts.Add(new LoadingReceipt
        {
            Id = 1, LoadingRegisterId = 1, TerminalId = 10, StorageTankId = 5,
            ReceiptDestination = LoadingReceiptDestination.ToInventory,
            LossMode = ReceiptLossMode.DeferredTankSettlement,
            ReceiptDate = new DateTime(2026, 5, 2), ReceivedQuantityMt = 90m
        });
        db.InventoryMovements.Add(InMovement(1, contractId: 7, qty: 90m));
        await db.SaveChangesAsync();

        var controller = new ReportsController(db);
        var result = await controller.ContractPnl(new ManagementReportFilterViewModel { ContractId = 7 });

        var model = Assert.IsType<ContractPnlReportViewModel>(Assert.IsType<ViewResult>(result).Model);
        var row = Assert.Single(model.PurchaseRows);
        Assert.True(row.HasPendingTankSettlement);
        Assert.Equal(90m, row.PendingSettlementQuantityMt);
        Assert.True(model.HasPendingTankSettlement);
    }

    // E) P&L نهایی: بعد از تسویه (موجودی صفر) دیگر موقت نیست.
    [Fact]
    public async Task ContractPnl_Final_When_Tank_Emptied()
    {
        await using var db = NewDb();
        SeedReference(db);
        db.Contracts.Add(NewPurchaseContract(7, "PUR-7"));
        db.LoadingRegisters.Add(new LoadingRegister
        {
            Id = 1, ContractId = 7, ProductId = 22,
            LoadingDate = new DateTime(2026, 5, 1), LoadedQuantityMt = 100m, LoadingPriceUsd = 200m
        });
        db.LoadingReceipts.Add(new LoadingReceipt
        {
            Id = 1, LoadingRegisterId = 1, TerminalId = 10, StorageTankId = 5,
            ReceiptDestination = LoadingReceiptDestination.ToInventory,
            LossMode = ReceiptLossMode.DeferredTankSettlement,
            ReceiptDate = new DateTime(2026, 5, 2), ReceivedQuantityMt = 90m
        });
        // In 90 then Out 90 → balance 0 (settled/consumed)
        db.InventoryMovements.AddRange(
            InMovement(1, contractId: 7, qty: 90m),
            new InventoryMovement
            {
                Id = 2, TerminalId = 10, StorageTankId = 5, ProductId = 22, ContractId = 7,
                Direction = MovementDirection.Out, MovementDate = new DateTime(2026, 5, 5), QuantityMt = 90m
            });
        await db.SaveChangesAsync();

        var controller = new ReportsController(db);
        var result = await controller.ContractPnl(new ManagementReportFilterViewModel { ContractId = 7 });

        var model = Assert.IsType<ContractPnlReportViewModel>(Assert.IsType<ViewResult>(result).Model);
        var row = Assert.Single(model.PurchaseRows);
        Assert.False(row.HasPendingTankSettlement);
        Assert.Equal(0m, row.PendingSettlementQuantityMt);
    }

    // F) Reconciliation رسید Deferred در انتظار تسویه را نشان دهد.
    [Fact]
    public async Task Reconciliation_Lists_Deferred_Receipt_Pending_Tank_Settlement()
    {
        await using var db = NewDb();
        SeedReference(db);
        db.Contracts.Add(NewPurchaseContract(7, "PUR-7"));
        db.LoadingRegisters.Add(new LoadingRegister
        {
            Id = 1, ContractId = 7, ProductId = 22,
            LoadingDate = new DateTime(2026, 5, 1), LoadedQuantityMt = 100m
        });
        db.LoadingReceipts.Add(new LoadingReceipt
        {
            Id = 1, LoadingRegisterId = 1, TerminalId = 10, StorageTankId = 5,
            ReceiptDestination = LoadingReceiptDestination.ToInventory,
            LossMode = ReceiptLossMode.DeferredTankSettlement,
            ReceiptDate = new DateTime(2026, 5, 2), ReceivedQuantityMt = 90m
        });
        db.InventoryMovements.Add(InMovement(1, contractId: 7, qty: 90m));
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);
        var result = await controller.IncompleteAfterReceipt();

        var model = Assert.IsType<IncompleteAfterReceiptViewModel>(Assert.IsType<ViewResult>(result).Model);
        var item = Assert.Single(model.PendingTankSettlements);
        Assert.Equal("PUR-7", item.ContractNumber);
        Assert.Equal(90m, item.QuantityMt);
        Assert.Equal("StorageTanks", item.DetailsControllerName);
        Assert.Equal("SettleFinal", item.DetailsActionName);
        Assert.Equal(5, item.DetailsRouteId);
    }

    // ---- helpers -----------------------------------------------------------

    private static ApplicationDbContext NewDb()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void SeedReference(ApplicationDbContext db)
    {
        db.Companies.Add(new Company { Id = 1, Code = "PTG", Name = "PTG", IsActive = true });
        db.Suppliers.Add(new Supplier { Id = 1, Name = "Supplier A", IsActive = true });
        db.Terminals.Add(new Terminal { Id = 10, Code = "T1", Name = "Terminal 1", IsActive = true });
        db.Products.Add(new Product { Id = 22, Code = "GO", Name = "Gas Oil", UnitOfMeasure = "MT", IsActive = true });
        db.StorageTanks.Add(new StorageTank
        {
            Id = 5, TerminalId = 10, TankCode = "A-01", ProductId = 22, CapacityMt = 1000m, IsActive = true
        });
    }

    private static Contract NewPurchaseContract(int id, string number)
        => new()
        {
            Id = id, ContractNumber = number, ContractType = ContractType.Purchase,
            CompanyId = 1, SupplierId = 1, ProductId = 22,
            ContractDate = new DateTime(2026, 5, 1), QuantityMt = 500m,
            PricingMethod = PricingMethod.Fixed, UnitPriceUsd = 100m
        };

    private static InventoryMovement InMovement(int id, int contractId, decimal qty)
        => new()
        {
            Id = id, TerminalId = 10, StorageTankId = 5, ProductId = 22, ContractId = contractId,
            Direction = MovementDirection.In, MovementDate = new DateTime(2026, 5, 1), QuantityMt = qty
        };

    private static void SeedTankWithContract(ApplicationDbContext db, int contractId, decimal inQty, decimal outQty = 0m)
    {
        SeedReference(db);
        db.Contracts.Add(NewPurchaseContract(contractId, "PUR-" + contractId));
        db.InventoryMovements.Add(InMovement(1, contractId, inQty));
        if (outQty > 0m)
        {
            db.InventoryMovements.Add(new InventoryMovement
            {
                Id = 2, TerminalId = 10, StorageTankId = 5, ProductId = 22, ContractId = contractId,
                Direction = MovementDirection.Out, MovementDate = new DateTime(2026, 5, 2), QuantityMt = outQty
            });
        }
    }

    private static StorageTanksController BuildTanksController(ApplicationDbContext db)
    {
        var controller = new StorageTanksController(
            db,
            new AuditService(db),
            new MasterDataDeleteSafetyService(db),
            new StockService(db),
            new LossEventWorkflowService(db, new StockService(db), new AuditService(db)))
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new NullTempDataProvider())
        };
        return controller;
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
