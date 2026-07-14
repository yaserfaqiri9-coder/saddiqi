using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class InventoryTransportPnlServiceTests
{
    // Regression: فروشِ سطح‌محموله/کشتی نباید روی legِ پایین‌دستیِ «حمل از موجودی» ظاهر شود.
    // سناریو: محموله + تخصیص از دو قرارداد → legهای مبدأ. بخشی از داخل کشتی فروش شد (فروشِ سطح‌محموله).
    // باقیمانده به موجودیِ کشور ثالث رسید و بعداً یک leg «حمل از موجودی» (با واگن) از همان موجودی ساخته شد.
    [Fact]
    public async Task ShipmentSale_Does_Not_Leak_Into_Downstream_FromInventory_Leg()
    {
        await using var db = CreateDb();
        await SeedReferenceDataAsync(db);

        db.Shipments.Add(new Shipment { Id = 1, ShipmentCode = "VESSEL-1", QuantityMt = 100m });

        // legهای مبدأِ محموله از دو قرارداد (باید فروشِ کشتی را جذب کنند).
        var sourceLegA = new InventoryTransportLeg
        {
            Id = 1,
            ShipmentId = 1,
            SourcePurchaseContractId = 1,
            ProductId = 1,
            SourceTerminalId = 1,
            SourceStorageTankId = 1,
            TransportType = LoadingTransportType.Unspecified,
            LoadedDate = new DateTime(2026, 5, 1),
            QuantityMt = 60m,
            PurchaseUnitCostUsd = 500m,
            Status = InventoryTransportLegStatus.Loaded
        };
        var sourceLegB = new InventoryTransportLeg
        {
            Id = 2,
            ShipmentId = 1,
            SourcePurchaseContractId = 2,
            ProductId = 1,
            SourceTerminalId = 1,
            SourceStorageTankId = 1,
            TransportType = LoadingTransportType.Unspecified,
            LoadedDate = new DateTime(2026, 5, 1),
            QuantityMt = 40m,
            PurchaseUnitCostUsd = 500m,
            Status = InventoryTransportLegStatus.Loaded
        };

        // legِ «حمل از موجودی»: از موجودیِ کشور ثالث ساخته شده، ShipmentId والد را حفظ کرده،
        // و LoadedDate آن پیش از تاریخِ فروش است (تا ثابت شود گاردِ تاریخِ <= کافی نیست و
        // فیلترِ واقعی، پرچمِ downstream است).
        var fromInventoryLeg = new InventoryTransportLeg
        {
            Id = 3,
            ShipmentId = 1,
            InventoryTransportBatchId = 1,
            SourcePurchaseContractId = 1,
            ProductId = 1,
            SourceTerminalId = 2,
            SourceStorageTankId = 2,
            TransportType = LoadingTransportType.Wagon,
            LoadedDate = new DateTime(2026, 5, 2),
            QuantityMt = 30m,
            PurchaseUnitCostUsd = 500m,
            Status = InventoryTransportLegStatus.Loaded
        };

        db.InventoryTransportLegs.AddRange(sourceLegA, sourceLegB, fromInventoryLeg);

        // فروشِ سطح‌محموله (از داخل کشتی)؛ به هیچ رسید/legِ مشخصی گره نخورده.
        db.SalesTransactions.Add(new SalesTransaction
        {
            Id = 1,
            CompanyId = 1,
            CustomerId = 1,
            ProductId = 1,
            ShipmentId = 1,
            SaleStage = SaleStage.InTransit,
            InvoiceNumber = "VESSEL-SALE-1",
            SaleDate = new DateTime(2026, 5, 10),
            QuantityMt = 40m,
            UnitPriceUsd = 750m,
            TotalUsd = 30_000m
        });
        await db.SaveChangesAsync();

        var pnl = await new InventoryTransportPnlService(db)
            .BuildForLegsAsync([sourceLegA.Id, sourceLegB.Id, fromInventoryLeg.Id]);

        // legِ حمل از موجودی هیچ فروشی ندارد.
        var fromInventory = pnl[fromInventoryLeg.Id];
        Assert.Empty(fromInventory.Sales);
        Assert.Equal(0m, fromInventory.SoldQuantityMt);
        Assert.Equal(0m, fromInventory.SalesUsd);

        // legهای مبدأِ محموله همچنان کلِ فروشِ کشتی را (به نسبت مقدار) دریافت می‌کنند.
        var totalSoldOnSource = pnl[sourceLegA.Id].SoldQuantityMt + pnl[sourceLegB.Id].SoldQuantityMt;
        var totalSalesUsdOnSource = pnl[sourceLegA.Id].SalesUsd + pnl[sourceLegB.Id].SalesUsd;
        Assert.Equal(40m, totalSoldOnSource);
        Assert.Equal(30_000m, totalSalesUsdOnSource);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task SeedReferenceDataAsync(ApplicationDbContext db)
    {
        db.Companies.Add(new Company { Id = 1, Code = "PTG", Name = "PTG" });
        db.Suppliers.Add(new Supplier { Id = 1, Name = "Supplier A" });
        db.Customers.Add(new Customer { Id = 1, Name = "Customer A" });
        db.Products.Add(new Product { Id = 1, Code = "GO", Name = "Gas Oil" });
        db.Terminals.AddRange(
            new Terminal { Id = 1, Code = "SRC", Name = "Source Terminal" },
            new Terminal { Id = 2, Code = "DST", Name = "Destination Terminal" });
        db.StorageTanks.AddRange(
            new StorageTank { Id = 1, TerminalId = 1, TankCode = "SRC-TK", ProductId = 1, CapacityMt = 500m },
            new StorageTank { Id = 2, TerminalId = 2, TankCode = "DST-TK", ProductId = 1, CapacityMt = 500m });
        db.Contracts.AddRange(
            new Contract
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
            },
            new Contract
            {
                Id = 2,
                ContractNumber = "PUR-002",
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
}
