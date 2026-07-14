using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Models.InventoryReports;
using PTGOilSystem.Web.Services;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class InventoryReportsControllerTests
{
    [Fact]
    public async Task IlinkaStock_Uses_StockService_Running_Balance_And_Splits_Movement_Columns()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        db.InventoryMovements.AddRange(
            new InventoryMovement
            {
                Id = 1,
                ProductId = 1,
                ContractId = 1,
                TerminalId = 1,
                StorageTankId = 1,
                Direction = MovementDirection.In,
                MovementDate = new DateTime(2026, 1, 1),
                QuantityMt = 100m,
                ReferenceDocument = "RWB-1"
            },
            new InventoryMovement
            {
                Id = 2,
                ProductId = 1,
                ContractId = 1,
                TerminalId = 1,
                StorageTankId = 1,
                Direction = MovementDirection.Out,
                MovementDate = new DateTime(2026, 1, 2),
                QuantityMt = 30m,
                ReferenceDocument = "DSP-1"
            },
            new InventoryMovement
            {
                Id = 3,
                ProductId = 1,
                ContractId = 1,
                TerminalId = 1,
                StorageTankId = 1,
                Direction = MovementDirection.Adjustment,
                MovementDate = new DateTime(2026, 1, 3),
                QuantityMt = 5m,
                ReferenceDocument = "ADJ-1"
            },
            new InventoryMovement
            {
                Id = 4,
                ProductId = 1,
                ContractId = 1,
                TerminalId = 1,
                StorageTankId = 1,
                Direction = MovementDirection.Transfer,
                MovementDate = new DateTime(2026, 1, 4),
                QuantityMt = 10m,
                ReferenceDocument = "TRF-1"
            });
        await db.SaveChangesAsync();

        var controller = new InventoryReportsController(db, new StockService(db));

        var result = await controller.IlinkaStock(new IlinkaStockReportFilterViewModel
        {
            ProductId = 1,
            ContractId = 1,
            TerminalId = 1,
            StorageTankId = 1,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 4)
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<IlinkaStockReportViewModel>(view.Model);
        Assert.Equal(4, model.Rows.Count);
        Assert.Equal(100m, model.Rows[0].InQuantityMt);
        Assert.Equal(100m, model.Rows[0].RunningBalanceMt);
        Assert.Equal(30m, model.Rows[1].OutQuantityMt);
        Assert.Equal(70m, model.Rows[1].RunningBalanceMt);
        Assert.Equal(5m, model.Rows[2].AdjustmentQuantityMt);
        Assert.Equal(75m, model.Rows[2].RunningBalanceMt);
        Assert.Equal(-10m, model.Rows[3].TransferQuantityMt);
        Assert.Equal(65m, model.Rows[3].RunningBalanceMt);
        Assert.All(model.Rows, row => Assert.Equal("مخزن ایلینکا شماره ۱", row.StorageTankCode));
    }

    [Fact]
    public async Task IlinkaStock_StorageTank_Filter_Reconstructs_Selected_Tank_Only()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        db.StorageTanks.Add(new StorageTank { Id = 2, TerminalId = 1, TankCode = "TANK-2" });
        db.InventoryMovements.AddRange(
            new InventoryMovement
            {
                Id = 1,
                ProductId = 1,
                TerminalId = 1,
                StorageTankId = 1,
                Direction = MovementDirection.In,
                MovementDate = new DateTime(2026, 1, 1),
                QuantityMt = 80m
            },
            new InventoryMovement
            {
                Id = 2,
                ProductId = 1,
                TerminalId = 1,
                StorageTankId = 2,
                Direction = MovementDirection.In,
                MovementDate = new DateTime(2026, 1, 1),
                QuantityMt = 50m
            });
        await db.SaveChangesAsync();

        var controller = new InventoryReportsController(db, new StockService(db));

        var result = await controller.IlinkaStock(new IlinkaStockReportFilterViewModel
        {
            ProductId = 1,
            TerminalId = 1,
            StorageTankId = 1
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<IlinkaStockReportViewModel>(view.Model);
        var row = Assert.Single(model.Rows);
        Assert.Equal("مخزن ایلینکا شماره ۱", row.StorageTankCode);
        Assert.Equal(80m, row.RunningBalanceMt);
    }

    private static void SeedReferenceData(ApplicationDbContext db)
    {
        db.Products.Add(new Product { Id = 1, Code = "GAS", Name = "Gasoline" });
        db.Companies.Add(new Company { Id = 1, Code = "PTG", Name = "PTG" });
        db.Terminals.Add(new Terminal { Id = 1, Code = "ILK", Name = "Ilinka" });
        db.StorageTanks.Add(new StorageTank
        {
            Id = 1,
            TerminalId = 1,
            TankCode = "TANK-1",
            DisplayName = "مخزن ایلینکا شماره ۱"
        });
        db.Contracts.Add(new Contract
        {
            Id = 1,
            ContractNumber = "ILINKA-2026",
            ContractType = ContractType.Purchase,
            CompanyId = 1,
            ProductId = 1,
            ContractDate = new DateTime(2026, 1, 1),
            QuantityMt = 100m,
            PricingMethod = PricingMethod.Fixed,
            UnitPriceUsd = 500m
        });
    }
}
