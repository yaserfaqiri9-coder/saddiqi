using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class TransportMasterDataControllerTests
{
    [Fact]
    public async Task Trucks_Create_Persists_Record_And_Audit()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        var controller = new TrucksController(db, new AuditService(db))
        {
            TempData = BuildTempData()
        };

        var result = await controller.Create(new Truck
        {
            PlateNumber = "BLK-100",
            Owner = "PTG Fleet",
            MaxLoadMt = 38.5m,
            IsActive = true
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var truck = await db.Trucks.SingleAsync();
        Assert.Equal("BLK-100", truck.PlateNumber);

        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal(nameof(Truck), audit.EntityName);
        Assert.Equal("Insert", audit.Action);
    }

    [Fact]
    public async Task Drivers_Create_Persists_Record_And_Audit()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        var controller = new DriversController(db, new AuditService(db))
        {
            TempData = BuildTempData()
        };

        var result = await controller.Create(new Driver
        {
            FullName = "Ahmad Wali",
            LicenseNumber = "DRV-123",
            Phone = "0700123456",
            IsActive = true
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var driver = await db.Drivers.SingleAsync();
        Assert.Equal("Ahmad Wali", driver.FullName);

        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal(nameof(Driver), audit.EntityName);
        Assert.Equal("Insert", audit.Action);
    }

    [Fact]
    public async Task Wagons_Create_Persists_Record_And_Audit()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        var controller = new WagonsController(db, new AuditService(db))
        {
            TempData = BuildTempData()
        };

        var result = await controller.Create(new Wagon
        {
            WagonNumber = "WGN-100",
            WagonType = "Tank",
            Owner = "Rail Fleet",
            CapacityMt = 62.5m,
            IsActive = true
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var wagon = await db.Wagons.SingleAsync();
        Assert.Equal("WGN-100", wagon.WagonNumber);

        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal(nameof(Wagon), audit.EntityName);
        Assert.Equal("Insert", audit.Action);
    }

    [Fact]
    public async Task Vessels_Edit_Updates_Record_And_Audit()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.Vessels.Add(new Vessel
        {
            Id = 7,
            Name = "VOLGA",
            Imo = "IMO-1",
            Flag = "RU",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = new VesselsController(db, new AuditService(db))
        {
            TempData = BuildTempData()
        };

        var result = await controller.Edit(7, new Vessel
        {
            Id = 7,
            Name = "VOLGA II",
            Imo = "IMO-2",
            Flag = "TM",
            IsActive = false
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var vessel = await db.Vessels.SingleAsync();
        Assert.Equal("VOLGA II", vessel.Name);
        Assert.False(vessel.IsActive);

        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal(nameof(Vessel), audit.EntityName);
        Assert.Equal("Update", audit.Action);
    }

    private static TempDataDictionary BuildTempData()
        => new(new DefaultHttpContext(), new InMemoryTempDataProvider());

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        private IDictionary<string, object> _data = new Dictionary<string, object>();

        public IDictionary<string, object> LoadTempData(HttpContext context) => _data;

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
            => _data = new Dictionary<string, object>(values);
    }
}
