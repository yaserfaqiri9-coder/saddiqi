using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Models.InventoryTransport;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class InventoryTransportLegModelTests
{
    [Fact]
    public void Inventory_Source_Quantity_Is_Nullable_For_Unselected_Rows()
    {
        var property = typeof(InventoryTransportSourceSelectionInput)
            .GetProperty(nameof(InventoryTransportSourceSelectionInput.QuantityMt));

        Assert.NotNull(property);
        Assert.Equal(typeof(decimal?), property!.PropertyType);
    }

    [Fact]
    public void InventoryTransportLeg_Is_Mapped_With_Core_Indexes_And_Precision()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=ptg_model_test;Username=ptg;Password=ptg")
            .Options;

        using var db = new ApplicationDbContext(options);

        var entity = db.Model.FindEntityType(typeof(InventoryTransportLeg));
        Assert.NotNull(entity);
        Assert.Equal("InventoryTransportLegs", entity!.GetTableName());
        Assert.Equal("numeric(18,4)", entity.FindProperty(nameof(InventoryTransportLeg.QuantityMt))!.GetColumnType());
        Assert.Equal("numeric(18,4)", entity.FindProperty(nameof(InventoryTransportLeg.ChargeableQuantityMt))!.GetColumnType());
        Assert.Equal(200, entity.FindProperty(nameof(InventoryTransportLeg.WagonNumber))!.GetMaxLength());
        Assert.Equal(100, entity.FindProperty(nameof(InventoryTransportLeg.RwbNo))!.GetMaxLength());
        Assert.Equal(100, entity.FindProperty(nameof(InventoryTransportLeg.BillOfLadingNumber))!.GetMaxLength());
        Assert.Equal(200, entity.FindProperty(nameof(InventoryTransportLeg.RouteDescription))!.GetMaxLength());
        Assert.Equal(1000, entity.FindProperty(nameof(InventoryTransportLeg.Notes))!.GetMaxLength());

        Assert.Contains(entity.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(InventoryTransportLeg.SourcePurchaseContractId)]));
        Assert.Contains(entity.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(InventoryTransportLeg.ProductId)]));
        Assert.Contains(entity.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(InventoryTransportLeg.SourceTerminalId), nameof(InventoryTransportLeg.SourceStorageTankId)]));
        Assert.Contains(entity.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(InventoryTransportLeg.Status)]));
        Assert.Contains(entity.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(InventoryTransportLeg.LoadedDate)]));
        Assert.Contains(entity.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(InventoryTransportLeg.WagonNumber)]));
        Assert.Contains(entity.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(InventoryTransportLeg.RwbNo)]));
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual([nameof(InventoryTransportLeg.OutboundInventoryMovementId)]));

        var batch = db.Model.FindEntityType(typeof(InventoryTransportBatch));
        Assert.NotNull(batch);
        Assert.Equal("numeric(18,4)", batch!.FindProperty(nameof(InventoryTransportBatch.TotalQuantityMt))!.GetColumnType());
        Assert.Contains(batch.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual([nameof(InventoryTransportBatch.BatchNumber)]));

        var allocation = db.Model.FindEntityType(typeof(InventoryTransportLegAllocation));
        Assert.NotNull(allocation);
        Assert.Equal("numeric(18,4)", allocation!.FindProperty(nameof(InventoryTransportLegAllocation.QuantityMt))!.GetColumnType());
        Assert.Contains(allocation.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual([nameof(InventoryTransportLegAllocation.OutboundInventoryMovementId)]));
    }
}
