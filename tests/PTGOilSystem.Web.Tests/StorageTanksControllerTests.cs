using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Models.StorageTanks;
using PTGOilSystem.Web.Services;
using PTGOilSystem.Web.Services.DeleteSafety;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class StorageTanksControllerTests
{
    [Fact]
    public async Task Details_Shows_Real_Tank_Balance_And_Movement_Ledger()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.Companies.Add(new Company { Id = 1, Code = "PTG", Name = "PTG", IsActive = true });
        db.Suppliers.Add(new Supplier { Id = 1, Name = "Supplier A", IsActive = true });
        db.Terminals.Add(new Terminal { Id = 10, Code = "T1", Name = "Terminal 1", IsActive = true });
        db.Products.Add(new Product
        {
            Id = 22,
            Code = "GO",
            Name = "Gas Oil",
            UnitOfMeasure = "MT",
            IsActive = true
        });
        db.Contracts.Add(new Contract
        {
            Id = 7,
            ContractNumber = "PUR-007",
            ContractType = ContractType.Purchase,
            CompanyId = 1,
            SupplierId = 1,
            ProductId = 22,
            ContractDate = new DateTime(2026, 5, 1),
            QuantityMt = 200m,
            PricingMethod = PricingMethod.Fixed,
            UnitPriceUsd = 100m
        });
        db.StorageTanks.Add(new StorageTank
        {
            Id = 5,
            TerminalId = 10,
            TankCode = "A-01",
            DisplayName = "Main tank",
            ProductId = 22,
            CapacityMt = 1250m,
            IsActive = true
        });
        db.InventoryMovements.AddRange(
            new InventoryMovement
            {
                Id = 1,
                TerminalId = 10,
                StorageTankId = 5,
                ProductId = 22,
                ContractId = 7,
                Direction = MovementDirection.In,
                MovementDate = new DateTime(2026, 5, 1),
                QuantityMt = 100m,
                ReferenceDocument = "IN-1"
            },
            new InventoryMovement
            {
                Id = 2,
                TerminalId = 10,
                StorageTankId = 5,
                ProductId = 22,
                ContractId = 7,
                Direction = MovementDirection.Out,
                MovementDate = new DateTime(2026, 5, 2),
                QuantityMt = 50m,
                ReferenceDocument = "OUT-1"
            },
            new InventoryMovement
            {
                Id = 3,
                TerminalId = 10,
                StorageTankId = 5,
                ProductId = 22,
                ContractId = 7,
                Direction = MovementDirection.In,
                MovementDate = new DateTime(2026, 5, 3),
                QuantityMt = 20m,
                ReferenceDocument = "IN-2"
            });
        await db.SaveChangesAsync();
        var controller = BuildController(db);

        var result = await controller.Details(5);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<StorageTankDetailsViewModel>(view.Model);
        Assert.Equal("A-01", model.TankCode);
        Assert.Equal("MT", model.UnitOfMeasure);
        Assert.Equal(70m, model.CurrentQuantityMt);
        Assert.Equal(120m, model.TotalInQuantityMt);
        Assert.Equal(50m, model.TotalOutQuantityMt);
        Assert.Equal(3, model.MovementCount);
        Assert.Equal(5.6m, model.FillPercent);
        Assert.Equal(ContractType.Purchase, Assert.Single(model.ContractStockBreakdownRows).ContractType);
        Assert.Collection(
            model.Movements,
            row => Assert.Equal(100m, row.RunningBalanceMt),
            row => Assert.Equal(50m, row.RunningBalanceMt),
            row => Assert.Equal(70m, row.RunningBalanceMt));
        Assert.All(model.Movements, row => Assert.Equal(5, row.StorageTankId));
    }

    [Fact]
    public async Task Index_Populates_Create_Lookups_For_Modal()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.Terminals.Add(new Terminal { Id = 10, Code = "T1", Name = "Terminal 1", IsActive = true });
        db.Products.Add(new Product
        {
            Id = 22,
            Code = "GO",
            Name = "Gas Oil",
            UnitOfMeasure = "MT",
            IsActive = true
        });
        db.StorageTanks.Add(new StorageTank
        {
            Id = 5,
            TerminalId = 10,
            TankCode = "A-01",
            ProductId = 22,
            CapacityMt = 1250m
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.Index(null, null, null, null);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<StorageTankIndexViewModel>(view.Model);
        var tank = Assert.Single(model.Items);
        Assert.Equal("A-01", tank.TankCode);

        var createTerminals = Assert.IsType<SelectList>((object)controller.ViewBag.CreateTerminals);
        var createProducts = Assert.IsType<SelectList>((object)controller.ViewBag.CreateProducts);

        Assert.Single(createTerminals.Cast<SelectListItem>());
        Assert.Single(createProducts.Cast<SelectListItem>());
    }

    private static StorageTanksController BuildController(ApplicationDbContext db)
        => new(
            db,
            new AuditService(db),
            new MasterDataDeleteSafetyService(db),
            new StockService(db),
            new LossEventWorkflowService(db, new StockService(db), new AuditService(db)));
}
