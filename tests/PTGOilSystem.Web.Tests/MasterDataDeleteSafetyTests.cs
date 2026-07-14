using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services.DeleteSafety;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class MasterDataDeleteSafetyTests
{
    [Fact]
    public async Task EvaluateProductAsync_Returns_Archive_When_Product_Is_Used_In_Operational_Data()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.Products.Add(new Product { Id = 10, Code = "GO", Name = "Gas Oil" });
        db.DailyPlattsPrices.Add(new DailyPlattsPrice
        {
            ProductId = 10,
            BenchmarkCode = "GO-PLATTS",
            PriceDate = new DateTime(2026, 4, 20),
            PriceUsdPerMt = 500m
        });
        db.StorageTanks.Add(new StorageTank
        {
            TerminalId = 1,
            TankCode = "TK-01",
            ProductId = 10,
            CapacityMt = 1000m
        });
        db.InventoryMovements.Add(new InventoryMovement
        {
            TerminalId = 1,
            ProductId = 10,
            Direction = MovementDirection.In,
            MovementDate = new DateTime(2026, 4, 21),
            QuantityMt = 50m
        });
        await db.SaveChangesAsync();

        var service = new MasterDataDeleteSafetyService(db);

        var result = await service.EvaluateProductAsync(10);

        Assert.False(result.CanDelete);
        Assert.True(result.ArchiveInsteadOfDelete);
        Assert.Contains("قیمت", result.DependencySummary);
        Assert.Contains("مخازن", result.DependencySummary);
        Assert.Contains("موجودی", result.DependencySummary);
    }

    [Fact]
    public async Task EvaluateSupplierAsync_Returns_Archive_When_Supplier_Is_Used_In_Contracts_And_Ledger()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.Suppliers.Add(new Supplier { Id = 11, Name = "PTG Supplier" });
        db.Contracts.Add(new Contract
        {
            ContractNumber = "C-100",
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 11,
            ContractType = ContractType.Purchase,
            ContractDate = new DateTime(2026, 4, 20),
            PricingMethod = PricingMethod.Fixed,
            QuantityMt = 100m,
            UnitPriceUsd = 500m
        });
        db.LedgerEntries.Add(new LedgerEntry
        {
            SupplierId = 11,
            EntryDate = new DateTime(2026, 4, 20),
            Side = LedgerSide.Debit,
            AmountUsd = 1000m,
            SourceType = "Expense",
            SourceId = 1
        });
        await db.SaveChangesAsync();

        var service = new MasterDataDeleteSafetyService(db);

        var result = await service.EvaluateSupplierAsync(11);

        Assert.False(result.CanDelete);
        Assert.True(result.ArchiveInsteadOfDelete);
        Assert.Contains("قراردادها", result.DependencySummary);
        Assert.Contains("دفتر کل", result.DependencySummary);
    }

    [Fact]
    public async Task EvaluateLocationAsync_Returns_Archive_When_Location_Is_Used_In_Operational_Flows()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.Locations.Add(new Location { Id = 12, Name = "Kabul Depot", Kind = "Destination" });
        db.Contracts.Add(new Contract
        {
            ContractNumber = "C-200",
            CompanyId = 1,
            ProductId = 1,
            DestinationLocationId = 12,
            ContractType = ContractType.Sale,
            ContractDate = new DateTime(2026, 4, 20),
            PricingMethod = PricingMethod.Fixed,
            QuantityMt = 100m,
            UnitPriceUsd = 600m
        });
        db.TruckDispatches.Add(new TruckDispatch
        {
            ContractId = 1,
            ProductId = 1,
            TruckId = 1,
            DestinationLocationId = 12,
            DispatchDate = new DateTime(2026, 4, 21),
            LoadedQuantityMt = 25m
        });
        db.Shipments.Add(new Shipment
        {
            ShipmentCode = "SHP-01",
            OriginLocationId = 12,
            QuantityMt = 25m
        });
        await db.SaveChangesAsync();

        var service = new MasterDataDeleteSafetyService(db);

        var result = await service.EvaluateLocationAsync(12);

        Assert.False(result.CanDelete);
        Assert.True(result.ArchiveInsteadOfDelete);
        Assert.Contains("قراردادها", result.DependencySummary);
        Assert.Contains("دیسپچ‌ها", result.DependencySummary);
        Assert.Contains("محموله‌ها", result.DependencySummary);
    }

    [Fact]
    public async Task EvaluateExpenseTypeAsync_Returns_Allow_When_Not_Used_Anywhere()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.ExpenseTypes.Add(new ExpenseType { Id = 13, Code = "STG", Name = "Storage" });
        await db.SaveChangesAsync();

        var service = new MasterDataDeleteSafetyService(db);

        var result = await service.EvaluateExpenseTypeAsync(13);

        Assert.True(result.CanDelete);
        Assert.False(result.ArchiveInsteadOfDelete);
        Assert.Empty(result.DependencySummary);
    }
}
