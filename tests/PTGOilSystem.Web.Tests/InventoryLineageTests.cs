using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public sealed class InventoryLineageTests
{
    [Fact]
    public async Task Pnl_BuildAsync_Returns_Null_When_Shipment_Has_No_Lineage()
    {
        await using var db = NewDb();

        var result = await new InventoryLineagePnlService(db).BuildAsync(10);

        Assert.Null(result);
    }

    [Fact]
    public async Task Pnl_BuildAsync_Uses_Only_Root_Shipment_And_Counts_Allocations_Once()
    {
        await using var db = NewDb();
        db.InventoryLots.AddRange(
            new InventoryLot
            {
                Id = 1, ProductId = 1, TerminalId = 1, RootShipmentId = 10,
                QuantityMt = 20m, RemainingQuantityMt = 11m,
                SourceType = InventoryLotSourceType.VesselInbound,
                Status = InventoryLotStatus.Open,
                LineageConfidence = LineageConfidence.Verified
            },
            new InventoryLot
            {
                Id = 2, ProductId = 1, TerminalId = 1, RootShipmentId = 20,
                QuantityMt = 100m, RemainingQuantityMt = 100m,
                SourceType = InventoryLotSourceType.VesselInbound,
                Status = InventoryLotStatus.Open,
                LineageConfidence = LineageConfidence.NeedsReview
            });
        db.SaleLotAllocations.Add(new SaleLotAllocation
        {
            Id = 1, SalesTransactionId = 101, LotId = 1, QuantityMt = 5m, AmountUsd = 2500m,
            LineageConfidence = LineageConfidence.Verified
        });
        db.LossLotAllocations.Add(new LossLotAllocation
        {
            Id = 1, LossEventId = 201, LotId = 1, QuantityMt = 2m,
            LineageConfidence = LineageConfidence.Estimated
        });
        db.ExpenseLotAllocations.Add(new ExpenseLotAllocation
        {
            Id = 1, ExpenseTransactionId = 301, LotId = 1, AmountUsd = 300m,
            LineageConfidence = LineageConfidence.Legacy
        });
        await db.SaveChangesAsync();

        var result = await new InventoryLineagePnlService(db).BuildAsync(10);

        Assert.NotNull(result);
        Assert.Equal(20m, result.CargoInVesselMt);
        Assert.Equal(11m, result.InStockMt);
        Assert.Equal(5m, result.SoldMt);
        Assert.Equal(2500m, result.SoldUsd);
        Assert.Equal(2m, result.LossMt);
        Assert.Equal(300m, result.ExpenseUsd);
        Assert.Equal(LineageConfidence.Legacy, result.OverallConfidence);
        Assert.False(result.NeedsReviewCount > 0);
    }

    [Fact]
    public async Task Pnl_BuildAsync_Excludes_Unrelated_Movement_And_Includes_Backfilled_Leg_Customs()
    {
        await using var db = NewDb();
        db.InventoryLots.AddRange(
            new InventoryLot
            {
                Id = 1, ProductId = 1, TerminalId = 1, RootShipmentId = 10,
                QuantityMt = 10m, RemainingQuantityMt = 10m,
                SourceType = InventoryLotSourceType.VesselInbound, Status = InventoryLotStatus.Open
            },
            new InventoryLot
            {
                Id = 2, ProductId = 1, TerminalId = 1, RootShipmentId = 20,
                QuantityMt = 90m, RemainingQuantityMt = 90m,
                SourceType = InventoryLotSourceType.VesselInbound, Status = InventoryLotStatus.Open
            });
        db.InventoryLotMovements.AddRange(
            new InventoryLotMovement
            {
                Id = 1, FromLotId = 1, LoadedQuantityMt = 10m, ReceivedQuantityMt = 10m,
                Status = InventoryLotMovementStatus.Received,
                SourceReferenceType = InventoryLineageWriter.ReceiptRef,
                SourceReferenceId = 80,
                VehicleRefType = InventoryLineageWriter.LegRef,
                VehicleRefId = 77
            },
            new InventoryLotMovement
            {
                Id = 2, FromLotId = 2, ShipmentId = 10, LoadedQuantityMt = 90m,
                Status = InventoryLotMovementStatus.InTransit,
                SourceReferenceType = InventoryLineageWriter.LegRef,
                SourceReferenceId = 88
            });
        db.CustomsDeclarations.Add(new CustomsDeclaration
        {
            Id = 1, TransportLegId = 77, DeclarationDate = new DateTime(2026, 3, 1), TotalUsd = 75m
        });
        await db.SaveChangesAsync();

        var result = await new InventoryLineagePnlService(db).BuildAsync(10);

        Assert.NotNull(result);
        Assert.Equal(1, result.MovementCount);
        Assert.Equal(10m, result.ArrivedReceivedMt);
        Assert.Equal(0m, result.InTransitMt);
        Assert.Equal(75m, result.CustomsUsd);
    }

    [Theory]
    [InlineData(LineageConfidence.Verified)]
    [InlineData(LineageConfidence.Estimated)]
    [InlineData(LineageConfidence.Legacy)]
    [InlineData(LineageConfidence.NeedsReview)]
    public async Task Pnl_BuildAsync_Maps_Overall_Confidence(LineageConfidence confidence)
    {
        await using var db = NewDb();
        db.InventoryLots.Add(new InventoryLot
        {
            Id = 1, ProductId = 1, TerminalId = 1, RootShipmentId = 10,
            QuantityMt = 1m, RemainingQuantityMt = 1m,
            SourceType = InventoryLotSourceType.VesselInbound,
            Status = InventoryLotStatus.Open,
            LineageConfidence = confidence
        });
        await db.SaveChangesAsync();

        var result = await new InventoryLineagePnlService(db).BuildAsync(10);

        Assert.NotNull(result);
        Assert.Equal(confidence, result.OverallConfidence);
        Assert.Equal(confidence == LineageConfidence.NeedsReview ? 1 : 0, result.NeedsReviewCount);
    }

    [Fact]
    public async Task Writer_AllocateSale_Consumes_Preferred_Oldest_Lots_Without_Negative_Balance()
    {
        await using var db = NewDb();
        db.Contracts.AddRange(
            new Contract { Id = 1, ContractNumber = "OLD", ContractDate = new DateTime(2026, 1, 1) },
            new Contract { Id = 2, ContractNumber = "NEW", ContractDate = new DateTime(2026, 2, 1) });
        db.InventoryLots.AddRange(
            new InventoryLot
            {
                Id = 1, ProductId = 1, TerminalId = 1, RootContractId = 1,
                QuantityMt = 5m, RemainingQuantityMt = 5m, CreatedAt = new DateTime(2026, 1, 1),
                SourceType = InventoryLotSourceType.LegacyOpening, Status = InventoryLotStatus.Open
            },
            new InventoryLot
            {
                Id = 2, ProductId = 1, TerminalId = 1, RootContractId = 2,
                QuantityMt = 10m, RemainingQuantityMt = 10m, CreatedAt = new DateTime(2026, 2, 1),
                SourceType = InventoryLotSourceType.LegacyOpening, Status = InventoryLotStatus.Open
            });
        await db.SaveChangesAsync();
        var writer = EnabledWriter(db);
        var sale = new SalesTransaction
        {
            Id = 50, ProductId = 1, QuantityMt = 12m, TotalUsd = 6000m,
            SaleDate = new DateTime(2026, 3, 1), InvoiceNumber = "FIFO-1"
        };

        await writer.AllocateSaleAsync(sale, sourcePurchaseContractId: null, terminalId: 1, storageTankId: null);

        var allocations = await db.SaleLotAllocations.OrderBy(a => a.LotId).ToListAsync();
        Assert.Equal(12m, allocations.Sum(a => a.QuantityMt));
        Assert.Equal(5m, allocations[0].QuantityMt);
        Assert.Equal(7m, allocations[1].QuantityMt);
        Assert.Equal(0m, (await db.InventoryLots.FindAsync(1))!.RemainingQuantityMt);
        Assert.Equal(3m, (await db.InventoryLots.FindAsync(2))!.RemainingQuantityMt);
        Assert.All(await db.InventoryLots.ToListAsync(), lot => Assert.True(lot.RemainingQuantityMt >= 0m));
    }

    [Fact]
    public async Task Writer_AllocateSale_Records_Shortfall_As_NeedsReview_Without_Negative_Balance()
    {
        await using var db = NewDb();
        db.InventoryLots.Add(new InventoryLot
        {
            Id = 1, ProductId = 1, TerminalId = 1,
            QuantityMt = 2m, RemainingQuantityMt = 2m,
            SourceType = InventoryLotSourceType.LegacyOpening, Status = InventoryLotStatus.Open
        });
        await db.SaveChangesAsync();
        var sale = new SalesTransaction
        {
            Id = 60, ProductId = 1, ShipmentId = 10, QuantityMt = 5m, TotalUsd = 500m,
            SaleDate = new DateTime(2026, 3, 1), InvoiceNumber = "SHORT-1"
        };

        await EnabledWriter(db).AllocateSaleAsync(sale, null, 1, null);

        var allocations = await db.SaleLotAllocations.ToListAsync();
        Assert.Equal(5m, allocations.Sum(a => a.QuantityMt));
        Assert.Equal(500m, allocations.Sum(a => a.AmountUsd));
        Assert.Contains(allocations, a => a.LineageConfidence == LineageConfidence.NeedsReview && a.QuantityMt == 3m);
        Assert.All(await db.InventoryLots.ToListAsync(), lot => Assert.True(lot.RemainingQuantityMt >= 0m));
    }

    [Fact]
    public async Task Writer_Load_And_Receipt_Preserve_All_Source_Lots_And_Are_Idempotent()
    {
        await using var db = NewDb();
        db.Contracts.AddRange(
            new Contract { Id = 1, ContractNumber = "A", ContractDate = new DateTime(2026, 1, 1) },
            new Contract { Id = 2, ContractNumber = "B", ContractDate = new DateTime(2026, 2, 1) });
        db.InventoryLots.AddRange(
            new InventoryLot
            {
                Id = 1, ProductId = 1, TerminalId = 1, RootShipmentId = 10, RootContractId = 1,
                QuantityMt = 5m, RemainingQuantityMt = 5m, SourceType = InventoryLotSourceType.VesselInbound,
                Status = InventoryLotStatus.Open, LineageConfidence = LineageConfidence.Verified,
                CreatedAt = new DateTime(2026, 1, 1)
            },
            new InventoryLot
            {
                Id = 2, ProductId = 1, TerminalId = 1, RootShipmentId = 10, RootContractId = 2,
                QuantityMt = 10m, RemainingQuantityMt = 10m, SourceType = InventoryLotSourceType.VesselInbound,
                Status = InventoryLotStatus.Open, LineageConfidence = LineageConfidence.Verified,
                CreatedAt = new DateTime(2026, 2, 1)
            });
        await db.SaveChangesAsync();
        var writer = EnabledWriter(db);
        var leg = new InventoryTransportLeg
        {
            Id = 40, ShipmentId = 10, SourcePurchaseContractId = 1, ProductId = 1,
            SourceTerminalId = 1, DestinationTerminalId = 2, TransportType = LoadingTransportType.Truck,
            LoadedDate = new DateTime(2026, 3, 1), QuantityMt = 12m
        };
        var outbound = new InventoryMovement { Id = 70, ProductId = 1, TerminalId = 1, QuantityMt = 12m };

        await writer.OnLegLoadedAsync(leg, outbound);
        await writer.OnLegLoadedAsync(leg, outbound);

        var loaded = await db.InventoryLotMovements.OrderBy(m => m.Id).ToListAsync();
        Assert.Equal(2, loaded.Count);
        Assert.Equal(12m, loaded.Sum(m => m.LoadedQuantityMt));

        var receipt = new InventoryTransportReceipt
        {
            Id = 80, InventoryTransportLegId = leg.Id, ReceiptDate = new DateTime(2026, 3, 2),
            ReceivedQuantityMt = 10m, ShortageQuantityMt = 2m,
            ReceiptDestination = InventoryTransportReceiptDestination.ToInventory,
            DestinationTerminalId = 2
        };
        var inbound = new InventoryMovement { Id = 71, ProductId = 1, TerminalId = 2, QuantityMt = 10m };
        var loss = new LossEvent { Id = 90, ShipmentId = 10, ProductId = 1, DifferenceQuantityMt = 2m };

        await writer.OnLegReceiptAsync(leg, receipt, inbound, loss);
        await writer.OnLegReceiptAsync(leg, receipt, inbound, loss);

        var received = await db.InventoryLotMovements.OrderBy(m => m.Id).ToListAsync();
        Assert.Equal(10m, received.Sum(m => m.ReceivedQuantityMt));
        Assert.Equal(2m, received.Sum(m => m.ShortageQuantityMt));
        Assert.All(received, movement => Assert.Equal(InventoryLotMovementStatus.Received, movement.Status));
        Assert.Equal(2, await db.InventoryLots.CountAsync(l => l.SourceReferenceType == InventoryLineageWriter.ReceiptRef && l.SourceReferenceId == receipt.Id));
        Assert.Equal(2m, await db.LossLotAllocations.SumAsync(a => a.QuantityMt));
    }

    [Fact]
    public async Task Backfill_Dispatch_Uses_StockOut_Source_And_Preserves_MultiLot_Split()
    {
        await using var db = NewDb();
        db.Contracts.AddRange(
            new Contract { Id = 1, ContractNumber = "A", ContractDate = new DateTime(2026, 1, 1) },
            new Contract { Id = 2, ContractNumber = "B", ContractDate = new DateTime(2026, 2, 1) });
        db.InventoryLots.AddRange(
            new InventoryLot
            {
                Id = 1, ProductId = 1, TerminalId = 1, RootShipmentId = 10, RootContractId = 1,
                QuantityMt = 5m, RemainingQuantityMt = 5m, SourceType = InventoryLotSourceType.VesselInbound,
                Status = InventoryLotStatus.Open, CreatedAt = new DateTime(2026, 1, 1)
            },
            new InventoryLot
            {
                Id = 2, ProductId = 1, TerminalId = 1, RootShipmentId = 20, RootContractId = 2,
                QuantityMt = 10m, RemainingQuantityMt = 10m, SourceType = InventoryLotSourceType.VesselInbound,
                Status = InventoryLotStatus.Open, CreatedAt = new DateTime(2026, 2, 1)
            });
        db.TruckDispatches.Add(new TruckDispatch
        {
            Id = 40, ContractId = 1, ProductId = 1, TruckId = 1, DestinationLocationId = 99,
            DispatchDate = new DateTime(2026, 3, 1), LoadedQuantityMt = 12m, Status = DispatchStatus.Loaded
        });
        db.InventoryMovements.Add(new InventoryMovement
        {
            Id = 70, ProductId = 1, ContractId = 1, TerminalId = 1,
            Direction = MovementDirection.Out, MovementDate = new DateTime(2026, 3, 1),
            QuantityMt = 12m, ReferenceDocument = "TRUCK-DISPATCH:40"
        });
        await db.SaveChangesAsync();
        var backfill = new InventoryLineageBackfillService(db, EnabledWriter(db));

        var first = await backfill.RunAsync();
        var second = await backfill.RunAsync();

        var movements = await db.InventoryLotMovements.Where(m => m.SourceReferenceType == InventoryLineageBackfillService.DispatchRef).ToListAsync();
        Assert.True(
            movements.Count == 2,
            $"movements={movements.Count}; firstCreated={first.MovementsCreated}; secondCreated={second.MovementsCreated}; needsReview={string.Join(" | ", first.NeedsReviewItems)}");
        Assert.Equal(12m, movements.Sum(m => m.LoadedQuantityMt));
        Assert.All(movements, movement => Assert.Equal(1, movement.FromTerminalId));
        Assert.Equal(2, first.MovementsCreated);
        Assert.Equal(0, second.MovementsCreated);
    }

    [Fact]
    public async Task Writer_With_WriteLots_Off_Does_Not_Persist_Or_Consume_Lots()
    {
        await using var db = NewDb();
        db.InventoryLots.Add(new InventoryLot
        {
            Id = 1, ProductId = 1, TerminalId = 1, QuantityMt = 10m, RemainingQuantityMt = 10m,
            SourceType = InventoryLotSourceType.LegacyOpening, Status = InventoryLotStatus.Open
        });
        await db.SaveChangesAsync();
        var writer = new InventoryLineageWriter(db, Options.Create(new LineageOptions { WriteLots = false }));

        await writer.CreateLotAsync(new LotCreationRequest(
            1, 1, null, 4m, InventoryLotSourceType.FreeStock, LineageConfidence.Legacy));
        var result = await writer.ConsumeFifoAsync(new LotConsumeRequest(1, 1, null, null, 3m, DateTime.UtcNow));

        Assert.Equal(1, await db.InventoryLots.CountAsync());
        Assert.Equal(10m, (await db.InventoryLots.FindAsync(1))!.RemainingQuantityMt);
        Assert.Equal(3m, result.Shortfall);
        Assert.Empty(result.Consumptions);
    }

    private static ApplicationDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static InventoryLineageWriter EnabledWriter(ApplicationDbContext db)
        => new(db, Options.Create(new LineageOptions { WriteLots = true }));
}
