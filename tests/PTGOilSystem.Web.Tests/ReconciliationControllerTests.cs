using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Models.Reconciliation;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class ReconciliationControllerTests
{
    [Fact]
    public async Task OpenShipments_Finds_Shipments_Without_Sales_And_Expenses_And_Dispatches_Without_Receipt()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        db.Shipments.AddRange(
            new Shipment { Id = 1, ShipmentCode = "SHIP-OPEN", ContractId = 1, QuantityMt = 20m },
            new Shipment { Id = 2, ShipmentCode = "SHIP-OK", ContractId = 1, QuantityMt = 10m });
        db.SalesTransactions.Add(new SalesTransaction
        {
            Id = 1,
            CompanyId = 1,
            ContractId = 1,
            CustomerId = 1,
            ProductId = 1,
            ShipmentId = 2,
            InvoiceNumber = "INV-OK",
            SaleDate = new DateTime(2026, 4, 1),
            QuantityMt = 10m,
            UnitPriceUsd = 500m,
            TotalUsd = 5000m
        });
        db.ExpenseTypes.Add(new ExpenseType { Id = 1, Code = "FRT", Name = "Freight" });
        db.ExpenseTransactions.Add(new ExpenseTransaction
        {
            Id = 1,
            ExpenseTypeId = 1,
            ShipmentId = 2,
            ExpenseDate = new DateTime(2026, 4, 2),
            AmountUsd = 100m
        });
        db.Trucks.Add(new Truck { Id = 1, PlateNumber = "TRK-1" });
        db.TruckDispatches.Add(new TruckDispatch
        {
            Id = 1,
            ContractId = 1,
            ProductId = 1,
            TruckId = 1,
            DispatchDate = new DateTime(2026, 4, 3),
            LoadedQuantityMt = 5m
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.OpenShipments();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OpenShipmentsViewModel>(view.Model);
        Assert.Contains(model.ShipmentsWithoutSales, s => s.ShipmentCode == "SHIP-OPEN");
        Assert.Contains(model.ShipmentsWithoutExpenses, s => s.ShipmentCode == "SHIP-OPEN");
        Assert.Contains(model.DispatchesWithoutReceipt, d => d.DispatchId == 1 && d.Status == "Needs Review");
    }

    [Fact]
    public async Task MissingLedger_Finds_Sales_And_Expenses_Without_Source_Ledger()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        db.SalesTransactions.AddRange(
            new SalesTransaction
            {
                Id = 1,
                CompanyId = 1,
                ContractId = 1,
                CustomerId = 1,
                ProductId = 1,
                InvoiceNumber = "INV-MISSING",
                SaleDate = new DateTime(2026, 4, 1),
                QuantityMt = 10m,
                UnitPriceUsd = 500m,
                TotalUsd = 5000m
            },
            new SalesTransaction
            {
                Id = 2,
                CompanyId = 1,
                ContractId = 1,
                CustomerId = 1,
                ProductId = 1,
                InvoiceNumber = "INV-OK",
                SaleDate = new DateTime(2026, 4, 2),
                QuantityMt = 5m,
                UnitPriceUsd = 500m,
                TotalUsd = 2500m
            });
        db.ExpenseTypes.Add(new ExpenseType { Id = 1, Code = "FRT", Name = "Freight" });
        db.ExpenseTransactions.Add(new ExpenseTransaction
        {
            Id = 1,
            ExpenseTypeId = 1,
            ExpenseDate = new DateTime(2026, 4, 3),
            AmountUsd = 100m,
            Description = "Missing ledger"
        });
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = 1,
            EntryDate = new DateTime(2026, 4, 2),
            Side = LedgerSide.Credit,
            AmountUsd = 2500m,
            SourceType = "Sale",
            SourceId = 2,
            Description = "Sale ledger"
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        Assert.Contains(model.SalesWithoutLedger, s => s.Reference == "INV-MISSING");
        Assert.DoesNotContain(model.SalesWithoutLedger, s => s.Reference == "INV-OK");
        Assert.Contains(model.ExpensesWithoutLedger, e => e.SourceId == 1);
    }

    [Fact]
    public async Task MissingLedger_Flags_ServiceProvider_Missing_And_Mismatched_Ledger_Issues()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        db.ServiceProviders.Add(new PTGOilSystem.Web.Models.Entities.ServiceProvider
        {
            Id = 1,
            Name = "Railway Services A",
            ProviderType = ServiceProviderType.RailwayService,
            IsActive = true
        });
        db.CashAccounts.Add(new CashAccount
        {
            Id = 1,
            Code = "BANK-USD",
            Name = "Main USD Bank",
            AccountType = CashAccountType.Bank,
            Currency = "USD",
            IsActive = true
        });
        db.ExpenseTypes.Add(new ExpenseType { Id = 1, Code = "WAGON", Name = "Wagon Rent" });
        db.ExpenseTransactions.AddRange(
            new ExpenseTransaction
            {
                Id = 10,
                ExpenseTypeId = 1,
                ServiceProviderId = 1,
                ExpenseDate = new DateTime(2026, 4, 1),
                AmountUsd = 100m,
                Description = "Missing provider ledger"
            },
            new ExpenseTransaction
            {
                Id = 11,
                ExpenseTypeId = 1,
                ServiceProviderId = 1,
                ExpenseDate = new DateTime(2026, 4, 2),
                AmountUsd = 200m,
                Description = "Mismatched provider ledger"
            },
            new ExpenseTransaction
            {
                Id = 12,
                ExpenseTypeId = 1,
                ServiceProviderId = 1,
                ExpenseDate = new DateTime(2026, 4, 3),
                AmountUsd = 50m,
                Description = "Cancelled provider ledger",
                IsCancelled = true
            });
        db.PaymentTransactions.AddRange(
            new PaymentTransaction
            {
                Id = 20,
                PaymentDate = new DateTime(2026, 4, 4),
                Direction = PaymentDirection.Out,
                PaymentKind = PaymentKind.ServiceProviderPayment,
                CashAccountId = 1,
                ServiceProviderId = 1,
                Amount = 75m,
                Currency = "USD",
                AmountUsd = 75m,
                Reference = "SP-MISSING"
            },
            new PaymentTransaction
            {
                Id = 21,
                PaymentDate = new DateTime(2026, 4, 5),
                Direction = PaymentDirection.Out,
                PaymentKind = PaymentKind.ServiceProviderPayment,
                CashAccountId = 1,
                ServiceProviderId = 1,
                Amount = 80m,
                Currency = "USD",
                AmountUsd = 80m,
                Reference = "SP-MISMATCH",
                LedgerEntryId = 101
            });
        db.LedgerEntries.AddRange(
            new LedgerEntry
            {
                Id = 100,
                EntryDate = new DateTime(2026, 4, 2),
                Side = LedgerSide.Credit,
                AmountUsd = 125m,
                Description = "Wrong provider expense amount",
                SourceType = "Expense",
                SourceId = 11,
                Reference = "EXP-11",
                ServiceProviderId = 1
            },
            new LedgerEntry
            {
                Id = 101,
                EntryDate = new DateTime(2026, 4, 5),
                Side = LedgerSide.Debit,
                AmountUsd = 70m,
                Description = "Wrong provider payment amount",
                SourceType = nameof(PaymentKind.ServiceProviderPayment),
                SourceId = 21,
                Reference = "SP-MISMATCH",
                ServiceProviderId = 1
            },
            new LedgerEntry
            {
                Id = 102,
                EntryDate = new DateTime(2026, 4, 6),
                Side = LedgerSide.Credit,
                AmountUsd = 10m,
                Description = "Incomplete trace",
                SourceType = "",
                SourceId = 0,
                ServiceProviderId = 1
            },
            new LedgerEntry
            {
                Id = 103,
                EntryDate = new DateTime(2026, 4, 3),
                Side = LedgerSide.Credit,
                AmountUsd = 50m,
                Description = "Cancelled provider expense not reversed",
                SourceType = "Expense",
                SourceId = 12,
                Reference = "EXP-12",
                ServiceProviderId = 1
            });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        Assert.Contains(model.ServiceProviderExpensesWithoutLedger, row => row.SourceId == 10);
        Assert.Contains(model.ServiceProviderPaymentsWithoutLedger, row => row.SourceId == 20);
        Assert.Contains(model.ServiceProviderExpenseLedgerMismatches, row => row.SourceId == 11);
        Assert.Contains(model.ServiceProviderPaymentLedgerMismatches, row => row.SourceId == 21);
        Assert.Contains(model.ServiceProviderLedgerMissingSource, row => row.LedgerEntryId == 102);
        Assert.Contains(model.CancelledServiceProviderExpensesWithBalanceImpact, row => row.SourceId == 12);
    }

    [Fact]
    public async Task MissingLedger_Does_Not_Flag_DirectSale_With_Ledger_And_No_InventoryMovement()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        db.LoadingRegisters.Add(new LoadingRegister
        {
            Id = 1,
            ContractId = 1,
            ProductId = 1,
            LoadingDate = new DateTime(2026, 4, 1),
            LoadedQuantityMt = 10m
        });
        db.LoadingReceipts.Add(new LoadingReceipt
        {
            Id = 1,
            LoadingRegisterId = 1,
            TerminalId = 1,
            ReceiptDate = new DateTime(2026, 4, 2),
            ReceivedQuantityMt = 10m
        });
        db.SalesTransactions.Add(new SalesTransaction
        {
            Id = 1,
            CompanyId = 1,
            CustomerId = 1,
            ProductId = 1,
            SaleStage = SaleStage.InTransit,
            InvoiceNumber = "DS-OK",
            SaleDate = new DateTime(2026, 4, 3),
            QuantityMt = 10m,
            UnitPriceUsd = 500m,
            TotalUsd = 5000m
        });
        db.LoadingReceiptAllocations.Add(new LoadingReceiptAllocation
        {
            Id = 1,
            LoadingReceiptId = 1,
            Destination = LoadingReceiptAllocationDestination.DirectSale,
            Status = LoadingReceiptAllocationStatus.Completed,
            QuantityMt = 10m,
            SourcePurchaseContractId = 1,
            TerminalId = 1,
            SalesTransactionId = 1
        });
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = 1,
            EntryDate = new DateTime(2026, 4, 3),
            Side = LedgerSide.Credit,
            AmountUsd = 5000m,
            SourceType = "Sale",
            SourceId = 1,
            Description = "Direct sale ledger"
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        Assert.DoesNotContain(model.SalesWithoutLedger, s => s.Reference == "DS-OK");
        Assert.Empty(await db.InventoryMovements.ToListAsync());
    }

    [Fact]
    public async Task MissingLedger_Shows_DirectSale_Allocation_Without_Sale()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedLoadingReceipt(db);
        db.LoadingReceiptAllocations.Add(new LoadingReceiptAllocation
        {
            Id = 10,
            LoadingReceiptId = 1,
            Destination = LoadingReceiptAllocationDestination.DirectSale,
            Status = LoadingReceiptAllocationStatus.TraceOnly,
            QuantityMt = 12m,
            SourcePurchaseContractId = 1,
            TerminalId = 1,
            ReferenceDocument = "ALLOC-NO-SALE"
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        var row = Assert.Single(model.DirectSaleAllocationsWithoutSale);
        Assert.Equal(10, row.AllocationId);
        Assert.Equal(1, row.LoadingReceiptId);
        Assert.Equal("CON-1", row.ContractNumber);
        Assert.Equal(12m, row.AllocationQuantityMt);
        Assert.Equal("Missing SalesTransaction", row.Issue);
    }

    [Fact]
    public async Task MissingLedger_Shows_DirectSale_Sale_Without_Ledger()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedLoadingReceipt(db);
        db.SalesTransactions.Add(new SalesTransaction
        {
            Id = 20,
            CompanyId = 1,
            CustomerId = 1,
            ProductId = 1,
            SaleStage = SaleStage.InTransit,
            InvoiceNumber = "DS-NO-LEDGER",
            SaleDate = new DateTime(2026, 4, 3),
            QuantityMt = 8m,
            UnitPriceUsd = 500m,
            TotalUsd = 4000m
        });
        db.LoadingReceiptAllocations.Add(new LoadingReceiptAllocation
        {
            Id = 20,
            LoadingReceiptId = 1,
            Destination = LoadingReceiptAllocationDestination.DirectSale,
            Status = LoadingReceiptAllocationStatus.Completed,
            QuantityMt = 8m,
            SourcePurchaseContractId = 1,
            TerminalId = 1,
            SalesTransactionId = 20
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        var row = Assert.Single(model.DirectSaleSalesWithoutLedger);
        Assert.Equal(20, row.AllocationId);
        Assert.Equal(20, row.SalesTransactionId);
        Assert.Equal("Customer A", row.CustomerName);
        Assert.Equal("DS-NO-LEDGER", row.InvoiceNumber);
        Assert.Equal(8m, row.SaleQuantityMt);
        Assert.Equal(4000m, row.SaleTotalUsd);
        Assert.Equal("Missing Sale Ledger", row.Issue);
    }

    [Fact]
    public async Task MissingLedger_Shows_DirectSale_Quantity_Mismatch()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedLoadingReceipt(db);
        db.SalesTransactions.Add(new SalesTransaction
        {
            Id = 30,
            CompanyId = 1,
            CustomerId = 1,
            ProductId = 1,
            SaleStage = SaleStage.InTransit,
            InvoiceNumber = "DS-QTY-MISMATCH",
            SaleDate = new DateTime(2026, 4, 3),
            QuantityMt = 9m,
            UnitPriceUsd = 500m,
            TotalUsd = 4500m
        });
        db.LoadingReceiptAllocations.Add(new LoadingReceiptAllocation
        {
            Id = 30,
            LoadingReceiptId = 1,
            Destination = LoadingReceiptAllocationDestination.DirectSale,
            Status = LoadingReceiptAllocationStatus.Completed,
            QuantityMt = 10m,
            SourcePurchaseContractId = 1,
            TerminalId = 1,
            SalesTransactionId = 30
        });
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = 30,
            EntryDate = new DateTime(2026, 4, 3),
            Side = LedgerSide.Credit,
            AmountUsd = 4500m,
            SourceType = "Sale",
            SourceId = 30,
            Description = "Direct sale ledger"
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        var row = Assert.Single(model.DirectSaleQuantityMismatches);
        Assert.Equal(30, row.AllocationId);
        Assert.Equal("DS-QTY-MISMATCH", row.InvoiceNumber);
        Assert.Equal(10m, row.AllocationQuantityMt);
        Assert.Equal(9m, row.SaleQuantityMt);
        Assert.Equal("Quantity mismatch", row.Issue);
    }

    [Fact]
    public async Task MissingLedger_Shows_DirectSale_Ledger_Amount_Mismatch_And_Still_Does_Not_Require_InventoryMovement()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedLoadingReceipt(db);
        db.SalesTransactions.Add(new SalesTransaction
        {
            Id = 40,
            CompanyId = 1,
            CustomerId = 1,
            ProductId = 1,
            SaleStage = SaleStage.InTransit,
            InvoiceNumber = "DS-LEDGER-MISMATCH",
            SaleDate = new DateTime(2026, 4, 3),
            QuantityMt = 10m,
            UnitPriceUsd = 500m,
            TotalUsd = 5000m
        });
        db.LoadingReceiptAllocations.Add(new LoadingReceiptAllocation
        {
            Id = 40,
            LoadingReceiptId = 1,
            Destination = LoadingReceiptAllocationDestination.DirectSale,
            Status = LoadingReceiptAllocationStatus.Completed,
            QuantityMt = 10m,
            SourcePurchaseContractId = 1,
            TerminalId = 1,
            SalesTransactionId = 40
        });
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = 40,
            EntryDate = new DateTime(2026, 4, 3),
            Side = LedgerSide.Credit,
            AmountUsd = 4900m,
            SourceType = "Sale",
            SourceId = 40,
            Description = "Direct sale ledger"
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        var row = Assert.Single(model.DirectSaleLedgerAmountMismatches);
        Assert.Equal(40, row.AllocationId);
        Assert.Equal("DS-LEDGER-MISMATCH", row.InvoiceNumber);
        Assert.Equal(5000m, row.SaleTotalUsd);
        Assert.Equal(4900m, row.LedgerAmountUsd);
        Assert.Equal("Ledger amount mismatch", row.Issue);
        Assert.Empty(await db.InventoryMovements.ToListAsync());
        Assert.DoesNotContain(model.SalesWithoutLedger, s => s.Reference == "DS-LEDGER-MISMATCH");
    }

    [Fact]
    public async Task MissingLedger_Shows_DirectDispatch_Allocation_Without_Dispatch()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedLoadingReceipt(db);
        db.LoadingReceiptAllocations.Add(new LoadingReceiptAllocation
        {
            Id = 50,
            LoadingReceiptId = 1,
            Destination = LoadingReceiptAllocationDestination.DirectDispatchToTruck,
            Status = LoadingReceiptAllocationStatus.TraceOnly,
            QuantityMt = 12m,
            SourcePurchaseContractId = 1,
            TerminalId = 1,
            DestinationName = "Kabul Yard"
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        var row = Assert.Single(model.DirectDispatchAllocationsWithoutDispatch);
        Assert.Equal(50, row.AllocationId);
        Assert.Null(row.TruckDispatchId);
        Assert.Equal(1, row.LoadingReceiptId);
        Assert.Equal("CON-1", row.ContractNumber);
        Assert.Equal("Kabul Yard", row.DestinationName);
        Assert.Equal(12m, row.AllocationQuantityMt);
        Assert.Equal(0m, row.DispatchedQuantityMt);
        Assert.Equal(12m, row.RemainingQuantityMt);
        Assert.Equal("Missing DirectFromReceipt TruckDispatch", row.Issue);
    }

    [Fact]
    public async Task MissingLedger_Shows_DirectDispatch_Quantity_And_Status_Mismatches()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedLoadingReceipt(db);
        db.Trucks.Add(new Truck { Id = 1, PlateNumber = "TRK-1" });
        db.Drivers.Add(new Driver { Id = 1, FullName = "Driver A" });
        db.LoadingReceiptAllocations.Add(new LoadingReceiptAllocation
        {
            Id = 60,
            LoadingReceiptId = 1,
            Destination = LoadingReceiptAllocationDestination.DirectDispatchToTruck,
            Status = LoadingReceiptAllocationStatus.Completed,
            QuantityMt = 20m,
            SourcePurchaseContractId = 1,
            TerminalId = 1,
            DestinationName = "Kabul Yard"
        });
        db.TruckDispatches.Add(new TruckDispatch
        {
            Id = 60,
            DispatchMode = TruckDispatchMode.DirectFromReceipt,
            LoadingReceiptAllocationId = 60,
            ContractId = 1,
            ProductId = 1,
            TruckId = 1,
            DriverId = 1,
            DispatchDate = new DateTime(2026, 4, 4),
            Status = DispatchStatus.InTransit,
            LoadedQuantityMt = 15m
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        var quantityRow = Assert.Single(model.DirectDispatchQuantityMismatches);
        Assert.Equal(60, quantityRow.AllocationId);
        Assert.Equal(20m, quantityRow.AllocationQuantityMt);
        Assert.Equal(15m, quantityRow.DispatchedQuantityMt);
        Assert.Equal(5m, quantityRow.RemainingQuantityMt);
        Assert.Equal("Quantity mismatch", quantityRow.Issue);

        var statusRow = Assert.Single(model.DirectDispatchStatusMismatches);
        Assert.Equal(60, statusRow.AllocationId);
        Assert.Equal("Status mismatch", statusRow.Issue);
    }

    [Fact]
    public async Task MissingLedger_Shows_DirectDispatch_Without_Allocation_And_Unexpected_InventoryMovement()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedLoadingReceipt(db);
        db.Trucks.Add(new Truck { Id = 1, PlateNumber = "TRK-1" });
        db.LoadingReceiptAllocations.Add(new LoadingReceiptAllocation
        {
            Id = 70,
            LoadingReceiptId = 1,
            Destination = LoadingReceiptAllocationDestination.DirectDispatchToTruck,
            Status = LoadingReceiptAllocationStatus.Completed,
            QuantityMt = 10m,
            SourcePurchaseContractId = 1,
            TerminalId = 1,
            DestinationName = "Kabul Yard"
        });
        db.TruckDispatches.AddRange(
            new TruckDispatch
            {
                Id = 70,
                DispatchMode = TruckDispatchMode.DirectFromReceipt,
                LoadingReceiptAllocationId = 70,
                ContractId = 1,
                ProductId = 1,
                TruckId = 1,
                DispatchDate = new DateTime(2026, 4, 4),
                Status = DispatchStatus.Loaded,
                LoadedQuantityMt = 10m
            },
            new TruckDispatch
            {
                Id = 71,
                DispatchMode = TruckDispatchMode.DirectFromReceipt,
                LoadingReceiptAllocationId = null,
                ContractId = 1,
                ProductId = 1,
                TruckId = 1,
                DispatchDate = new DateTime(2026, 4, 5),
                Status = DispatchStatus.Loaded,
                LoadedQuantityMt = 3m
            });
        db.InventoryMovements.Add(new InventoryMovement
        {
            Id = 1,
            ContractId = 1,
            ProductId = 1,
            TerminalId = 1,
            MovementDate = new DateTime(2026, 4, 4),
            Direction = MovementDirection.Out,
            QuantityMt = 10m,
            ReferenceDocument = "TRUCK-DISPATCH:70"
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        var withoutAllocation = Assert.Single(model.DirectDispatchesWithoutAllocation);
        Assert.Equal(71, withoutAllocation.TruckDispatchId);
        Assert.Equal("Missing LoadingReceiptAllocation link", withoutAllocation.Issue);

        var withMovement = Assert.Single(model.DirectDispatchesWithInventoryMovement);
        Assert.Equal(70, withMovement.TruckDispatchId);
        Assert.Equal(70, withMovement.AllocationId);
        Assert.Equal("Unexpected InventoryMovement", withMovement.Issue);
    }

    [Fact]
    public async Task MissingLedger_Does_Not_Flag_DirectFromReceipt_Without_InventoryMovement_When_Allocation_Is_Fully_Dispatched()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedLoadingReceipt(db);
        db.Trucks.Add(new Truck { Id = 1, PlateNumber = "TRK-1" });
        db.LoadingReceiptAllocations.Add(new LoadingReceiptAllocation
        {
            Id = 80,
            LoadingReceiptId = 1,
            Destination = LoadingReceiptAllocationDestination.DirectDispatchToTruck,
            Status = LoadingReceiptAllocationStatus.Completed,
            QuantityMt = 10m,
            SourcePurchaseContractId = 1,
            TerminalId = 1,
            DestinationName = "Kabul Yard"
        });
        db.TruckDispatches.Add(new TruckDispatch
        {
            Id = 80,
            DispatchMode = TruckDispatchMode.DirectFromReceipt,
            LoadingReceiptAllocationId = 80,
            ContractId = 1,
            ProductId = 1,
            TruckId = 1,
            DispatchDate = new DateTime(2026, 4, 4),
            Status = DispatchStatus.Loaded,
            LoadedQuantityMt = 10m
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        Assert.Empty(model.DirectDispatchAllocationsWithoutDispatch);
        Assert.Empty(model.DirectDispatchQuantityMismatches);
        Assert.Empty(model.DirectDispatchesWithoutAllocation);
        Assert.Empty(model.DirectDispatchesWithInventoryMovement);
        Assert.Empty(model.DirectDispatchStatusMismatches);
        Assert.Empty(await db.InventoryMovements.ToListAsync());
    }

    [Fact]
    public async Task MissingLedger_Shows_DirectFromReceipt_Linked_Sale_Without_Ledger()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedLoadingReceipt(db);
        db.Trucks.Add(new Truck { Id = 1, PlateNumber = "TRK-1" });
        db.SalesTransactions.Add(new SalesTransaction
        {
            Id = 90,
            CompanyId = 1,
            CustomerId = 1,
            ProductId = 1,
            SaleStage = SaleStage.InTransit,
            InvoiceNumber = "DDS-NO-LEDGER",
            SaleDate = new DateTime(2026, 4, 4),
            QuantityMt = 10m,
            UnitPriceUsd = 500m,
            TotalUsd = 5000m
        });
        db.LoadingReceiptAllocations.Add(new LoadingReceiptAllocation
        {
            Id = 90,
            LoadingReceiptId = 1,
            Destination = LoadingReceiptAllocationDestination.DirectDispatchToTruck,
            Status = LoadingReceiptAllocationStatus.Completed,
            QuantityMt = 10m,
            SourcePurchaseContractId = 1,
            TerminalId = 1
        });
        db.TruckDispatches.Add(new TruckDispatch
        {
            Id = 90,
            DispatchMode = TruckDispatchMode.DirectFromReceipt,
            LoadingReceiptAllocationId = 90,
            SalesTransactionId = 90,
            ContractId = 1,
            ProductId = 1,
            TruckId = 1,
            DispatchDate = new DateTime(2026, 4, 4),
            Status = DispatchStatus.Loaded,
            LoadedQuantityMt = 10m
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        var row = Assert.Single(model.DirectDispatchSalesWithoutLedger);
        Assert.Equal(90, row.TruckDispatchId);
        Assert.Equal(90, row.SalesTransactionId);
        Assert.Equal("DDS-NO-LEDGER", row.InvoiceNumber);
        Assert.Equal("Missing Sale Ledger", row.Issue);
        Assert.DoesNotContain(model.SalesWithoutLedger, s => s.Reference == "DDS-NO-LEDGER");
        Assert.Empty(await db.InventoryMovements.ToListAsync());
    }

    [Fact]
    public async Task MissingLedger_Shows_DirectFromReceipt_Dispatch_Sale_Quantity_Mismatch()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedLoadingReceipt(db);
        db.Trucks.Add(new Truck { Id = 1, PlateNumber = "TRK-1" });
        db.SalesTransactions.Add(new SalesTransaction
        {
            Id = 91,
            CompanyId = 1,
            CustomerId = 1,
            ProductId = 1,
            SaleStage = SaleStage.InTransit,
            InvoiceNumber = "DDS-QTY",
            SaleDate = new DateTime(2026, 4, 4),
            QuantityMt = 9m,
            UnitPriceUsd = 500m,
            TotalUsd = 4500m
        });
        db.LoadingReceiptAllocations.Add(new LoadingReceiptAllocation
        {
            Id = 91,
            LoadingReceiptId = 1,
            Destination = LoadingReceiptAllocationDestination.DirectDispatchToTruck,
            Status = LoadingReceiptAllocationStatus.Completed,
            QuantityMt = 10m,
            SourcePurchaseContractId = 1,
            TerminalId = 1
        });
        db.TruckDispatches.Add(new TruckDispatch
        {
            Id = 91,
            DispatchMode = TruckDispatchMode.DirectFromReceipt,
            LoadingReceiptAllocationId = 91,
            SalesTransactionId = 91,
            ContractId = 1,
            ProductId = 1,
            TruckId = 1,
            DispatchDate = new DateTime(2026, 4, 4),
            Status = DispatchStatus.Loaded,
            LoadedQuantityMt = 10m
        });
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = 91,
            EntryDate = new DateTime(2026, 4, 4),
            Side = LedgerSide.Credit,
            AmountUsd = 4500m,
            SourceType = "Sale",
            SourceId = 91,
            Description = "Direct dispatch sale ledger"
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        var row = Assert.Single(model.DirectDispatchSaleQuantityMismatches);
        Assert.Equal(91, row.TruckDispatchId);
        Assert.Equal(10m, row.DispatchedQuantityMt);
        Assert.Equal(9m, row.SaleQuantityMt);
        Assert.Equal("Dispatch/Sale quantity mismatch", row.Issue);
        Assert.Empty(model.DirectDispatchesWithInventoryMovement);
    }

    [Fact]
    public async Task MissingLedger_Reports_InventoryMovement_With_NonPurchase_Contract()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        db.Terminals.Add(new Terminal { Id = 1, Code = "T1", Name = "Terminal 1" });
        db.InventoryMovements.AddRange(
            new InventoryMovement
            {
                Id = 100,
                ContractId = 1, // Sale contract — invalid for stock-out
                ProductId = 1,
                TerminalId = 1,
                MovementDate = new DateTime(2026, 4, 4),
                Direction = MovementDirection.Out,
                QuantityMt = 5m
            },
            new InventoryMovement
            {
                Id = 101,
                ContractId = 2, // Purchase contract — valid
                ProductId = 1,
                TerminalId = 1,
                MovementDate = new DateTime(2026, 4, 5),
                Direction = MovementDirection.Out,
                QuantityMt = 3m
            });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);
        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        var row = Assert.Single(model.InventoryMovementsWithNonPurchaseContract);
        Assert.Equal(100, row.MovementId);
        Assert.Equal("CON-1", row.ContractNumber);
        Assert.Equal(nameof(ContractType.Sale), row.ContractType);
        Assert.Equal("Inventory movement uses non-purchase contract", row.Issue);
    }

    [Fact]
    public async Task MissingLedger_Ignores_InventoryMovement_With_Purchase_Contract()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        db.Terminals.Add(new Terminal { Id = 1, Code = "T1", Name = "Terminal 1" });
        db.InventoryMovements.Add(new InventoryMovement
        {
            Id = 200,
            ContractId = 2,
            ProductId = 1,
            TerminalId = 1,
            MovementDate = new DateTime(2026, 4, 6),
            Direction = MovementDirection.Out,
            QuantityMt = 4m
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);
        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        Assert.Empty(model.InventoryMovementsWithNonPurchaseContract);
    }

    [Fact]
    public async Task MissingLedger_Reports_ToInventory_Allocation_Without_InventoryMovement()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        SeedLoadingReceipt(db);
        db.InventoryMovements.Add(new InventoryMovement
        {
            Id = 300,
            ContractId = 2,
            ProductId = 1,
            TerminalId = 1,
            MovementDate = new DateTime(2026, 4, 2),
            Direction = MovementDirection.In,
            QuantityMt = 10m
        });
        db.LoadingReceiptAllocations.AddRange(
            new LoadingReceiptAllocation
            {
                Id = 90,
                LoadingReceiptId = 1,
                Destination = LoadingReceiptAllocationDestination.ToInventory,
                Status = LoadingReceiptAllocationStatus.Completed,
                QuantityMt = 5m,
                SourcePurchaseContractId = 2,
                TerminalId = 1,
                InventoryMovementId = null // missing
            },
            new LoadingReceiptAllocation
            {
                Id = 91,
                LoadingReceiptId = 1,
                Destination = LoadingReceiptAllocationDestination.ToInventory,
                Status = LoadingReceiptAllocationStatus.Completed,
                QuantityMt = 5m,
                SourcePurchaseContractId = 2,
                TerminalId = 1,
                InventoryMovementId = 300 // valid link
            },
            new LoadingReceiptAllocation
            {
                Id = 92,
                LoadingReceiptId = 1,
                Destination = LoadingReceiptAllocationDestination.ToInventory,
                Status = LoadingReceiptAllocationStatus.Completed,
                QuantityMt = 2m,
                SourcePurchaseContractId = 2,
                TerminalId = 1,
                InventoryMovementId = 9999 // dangling reference
            });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);
        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        Assert.Equal(2, model.ToInventoryAllocationsWithoutMovement.Count);
        Assert.Contains(model.ToInventoryAllocationsWithoutMovement, r => r.AllocationId == 90 && r.Issue == "ToInventory allocation has no inventory movement");
        Assert.Contains(model.ToInventoryAllocationsWithoutMovement, r => r.AllocationId == 92 && r.Issue == "ToInventory allocation references missing InventoryMovement");
        Assert.DoesNotContain(model.ToInventoryAllocationsWithoutMovement, r => r.AllocationId == 91);
    }

    [Fact]
    public async Task MissingLedger_Reports_ToInventory_Receipt_Without_Allocation()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        db.Terminals.Add(new Terminal { Id = 1, Code = "T1", Name = "Terminal 1" });
        db.LoadingRegisters.Add(new LoadingRegister
        {
            Id = 1,
            ContractId = 1,
            ProductId = 1,
            LoadingDate = new DateTime(2026, 4, 1),
            LoadedQuantityMt = 30m
        });
        db.LoadingReceipts.AddRange(
            new LoadingReceipt
            {
                Id = 1,
                LoadingRegisterId = 1,
                TerminalId = 1,
                ReceiptDate = new DateTime(2026, 4, 2),
                ReceiptDestination = LoadingReceiptDestination.ToInventory,
                ReceivedQuantityMt = 10m
            }, // no allocations — should be flagged
            new LoadingReceipt
            {
                Id = 2,
                LoadingRegisterId = 1,
                TerminalId = 1,
                ReceiptDate = new DateTime(2026, 4, 3),
                ReceiptDestination = LoadingReceiptDestination.ToInventory,
                ReceivedQuantityMt = 10m
            }, // has allocation — should not be flagged
            new LoadingReceipt
            {
                Id = 3,
                LoadingRegisterId = 1,
                TerminalId = 1,
                ReceiptDate = new DateTime(2026, 4, 4),
                ReceiptDestination = LoadingReceiptDestination.DirectDispatch,
                ReceivedQuantityMt = 10m
            }); // DirectDispatch — out of scope, must not be flagged
        db.LoadingReceiptAllocations.Add(new LoadingReceiptAllocation
        {
            Id = 1,
            LoadingReceiptId = 2,
            Destination = LoadingReceiptAllocationDestination.ToInventory,
            Status = LoadingReceiptAllocationStatus.Completed,
            QuantityMt = 10m,
            TerminalId = 1
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);
        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        var row = Assert.Single(model.ToInventoryReceiptsWithoutAllocation);
        Assert.Equal(1, row.LoadingReceiptId);
        Assert.Equal("ToInventory receipt has no allocation", row.Issue);
    }

    [Fact]
    public async Task MissingLedger_Reports_DuplicateCustomsCandidates_When_Both_Paths_Exist()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        db.Terminals.Add(new Terminal { Id = 1, Code = "T1", Name = "Terminal 1" });
        db.LoadingRegisters.Add(new LoadingRegister
        {
            Id = 200,
            ContractId = 2, // Purchase from SeedPurchaseContract
            ProductId = 1,
            LoadingDate = new DateTime(2026, 4, 1),
            LoadedQuantityMt = 10m
        });
        db.CustomsDeclarations.Add(new CustomsDeclaration
        {
            Id = 1,
            LoadingRegisterId = 200,
            DeclarationDate = new DateTime(2026, 4, 2),
            TotalAfn = 200_000m,
            TotalUsd = 2_500m
        });
        db.ExpenseTypes.AddRange(
            new ExpenseType { Id = 200, Code = "CUSTOMS-DUTY", Name = "Customs Duty", Category = "Customs" },
            new ExpenseType { Id = 201, Code = "FUEL", Name = "Fuel", Category = "Trucking" });
        db.ExpenseTransactions.AddRange(
            new ExpenseTransaction
            {
                Id = 100,
                ExpenseTypeId = 200,
                ContractId = 2,
                ExpenseDate = new DateTime(2026, 4, 3),
                Amount = 1500m,
                Currency = "USD",
                AmountUsd = 1500m
            },
            new ExpenseTransaction
            {
                Id = 101,
                ExpenseTypeId = 201,
                ContractId = 2,
                ExpenseDate = new DateTime(2026, 4, 3),
                Amount = 100m,
                Currency = "USD",
                AmountUsd = 100m
            });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);
        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        var row = Assert.Single(model.DuplicateCustomsCandidates);
        Assert.Equal(2, row.ContractId);
        Assert.Equal("PUR-1", row.ContractNumber);
        Assert.Equal(2_500m, row.CustomsDeclarationTotalUsd);
        Assert.Equal(1, row.CustomsDeclarationCount);
        Assert.Equal(1500m, row.CustomsExpenseTotalUsd); // only the customs ExpenseType — Fuel is excluded
        Assert.Equal(1, row.CustomsExpenseCount);
    }

    [Fact]
    public async Task MissingLedger_Does_Not_Flag_DuplicateCustoms_When_Only_CustomsDeclaration_Exists()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        db.Terminals.Add(new Terminal { Id = 1, Code = "T1", Name = "Terminal 1" });
        db.LoadingRegisters.Add(new LoadingRegister
        {
            Id = 210,
            ContractId = 2,
            ProductId = 1,
            LoadingDate = new DateTime(2026, 4, 1),
            LoadedQuantityMt = 10m
        });
        db.CustomsDeclarations.Add(new CustomsDeclaration
        {
            Id = 2,
            LoadingRegisterId = 210,
            DeclarationDate = new DateTime(2026, 4, 2),
            TotalAfn = 100_000m,
            TotalUsd = 1_000m
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);
        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        Assert.Empty(model.DuplicateCustomsCandidates);
    }

    [Fact]
    public async Task MissingLedger_Does_Not_Flag_DuplicateCustoms_When_Only_CustomsExpense_Exists()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        db.ExpenseTypes.Add(new ExpenseType { Id = 200, Code = "CUSTOMS-DUTY", Name = "Customs Duty", Category = "Customs" });
        db.ExpenseTransactions.Add(new ExpenseTransaction
        {
            Id = 100,
            ExpenseTypeId = 200,
            ContractId = 2,
            ExpenseDate = new DateTime(2026, 4, 3),
            Amount = 500m,
            Currency = "USD",
            AmountUsd = 500m
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);
        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        Assert.Empty(model.DuplicateCustomsCandidates);
    }

    [Fact]
    public async Task MissingLedger_Does_Not_Flag_DuplicateCustoms_When_ExpenseType_Is_NotCustoms()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        db.Terminals.Add(new Terminal { Id = 1, Code = "T1", Name = "Terminal 1" });
        db.LoadingRegisters.Add(new LoadingRegister
        {
            Id = 220,
            ContractId = 2,
            ProductId = 1,
            LoadingDate = new DateTime(2026, 4, 1),
            LoadedQuantityMt = 10m
        });
        db.CustomsDeclarations.Add(new CustomsDeclaration
        {
            Id = 3,
            LoadingRegisterId = 220,
            DeclarationDate = new DateTime(2026, 4, 2),
            TotalAfn = 100_000m,
            TotalUsd = 1_000m
        });
        db.ExpenseTypes.Add(new ExpenseType { Id = 201, Code = "FUEL", Name = "Fuel", Category = "Trucking" });
        db.ExpenseTransactions.Add(new ExpenseTransaction
        {
            Id = 101,
            ExpenseTypeId = 201,
            ContractId = 2,
            ExpenseDate = new DateTime(2026, 4, 3),
            Amount = 200m,
            Currency = "USD",
            AmountUsd = 200m
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);
        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        Assert.Empty(model.DuplicateCustomsCandidates);
    }

    [Fact]
    public async Task MissingLedger_Reports_DuplicateCustomsCandidates_For_TransportLeg_Customs()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        SeedTransportLegLookups(db);
        db.InventoryTransportLegs.Add(new InventoryTransportLeg
        {
            Id = 230,
            SourcePurchaseContractId = 2,
            ProductId = 1,
            SourceTerminalId = 1,
            TransportType = LoadingTransportType.Wagon,
            LoadedDate = new DateTime(2026, 4, 2),
            QuantityMt = 20m,
            Status = InventoryTransportLegStatus.Loaded
        });
        db.CustomsDeclarations.Add(new CustomsDeclaration
        {
            Id = 230,
            TransportLegId = 230,
            DeclarationDate = new DateTime(2026, 4, 3),
            TotalUsd = 2_000m
        });
        db.ExpenseTypes.Add(new ExpenseType { Id = 230, Code = "CUSTOMS-DUTY", Name = "Customs Duty", Category = "Customs" });
        db.ExpenseTransactions.Add(new ExpenseTransaction
        {
            Id = 230,
            ExpenseTypeId = 230,
            ContractId = 2,
            ExpenseDate = new DateTime(2026, 4, 4),
            Amount = 500m,
            Currency = "USD",
            AmountUsd = 500m
        });
        await db.SaveChangesAsync();

        var model = await GetMissingLedgerModelAsync(db);

        var row = Assert.Single(model.DuplicateCustomsCandidates);
        Assert.Equal(2, row.ContractId);
        Assert.Equal(2_000m, row.CustomsDeclarationTotalUsd);
        Assert.Equal(1, row.CustomsDeclarationCount);
        Assert.Equal(500m, row.CustomsExpenseTotalUsd);
    }

    [Fact]
    public async Task MissingLedger_Flags_CustomsDeclaration_With_Both_Sources()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        SeedTransportLegLookups(db);
        db.LoadingRegisters.Add(new LoadingRegister
        {
            Id = 240,
            ContractId = 2,
            ProductId = 1,
            LoadingDate = new DateTime(2026, 4, 1),
            LoadedQuantityMt = 10m
        });
        db.InventoryTransportLegs.Add(new InventoryTransportLeg
        {
            Id = 240,
            SourcePurchaseContractId = 2,
            ProductId = 1,
            SourceTerminalId = 1,
            TransportType = LoadingTransportType.Wagon,
            LoadedDate = new DateTime(2026, 4, 2),
            QuantityMt = 20m
        });
        db.CustomsDeclarations.Add(new CustomsDeclaration
        {
            Id = 240,
            LoadingRegisterId = 240,
            TransportLegId = 240,
            DeclarationDate = new DateTime(2026, 4, 3)
        });
        await db.SaveChangesAsync();

        var model = await GetMissingLedgerModelAsync(db);

        var row = Assert.Single(model.CustomsSourceIssues);
        Assert.Equal(240, row.CustomsDeclarationId);
        Assert.Equal(240, row.LoadingRegisterId);
        Assert.Equal(240, row.TransportLegId);
        Assert.Equal("Customs declaration must reference exactly one source.", row.Issue);
    }

    [Fact]
    public async Task MissingLedger_Flags_CustomsDeclaration_With_No_Source()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        db.CustomsDeclarations.Add(new CustomsDeclaration
        {
            Id = 250,
            DeclarationDate = new DateTime(2026, 4, 3)
        });
        await db.SaveChangesAsync();

        var model = await GetMissingLedgerModelAsync(db);

        var row = Assert.Single(model.CustomsSourceIssues);
        Assert.Equal(250, row.CustomsDeclarationId);
        Assert.Null(row.LoadingRegisterId);
        Assert.Null(row.TransportLegId);
        Assert.Equal("Customs declaration must reference exactly one source.", row.Issue);
    }

    [Fact]
    public async Task MissingLedger_Flags_Loaded_TransportLeg_Without_OutboundMovement()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        SeedTransportLegLookups(db);
        db.InventoryTransportLegs.Add(new InventoryTransportLeg
        {
            Id = 800,
            SourcePurchaseContractId = 2,
            ProductId = 1,
            SourceTerminalId = 1,
            SourceStorageTankId = 1,
            TransportType = LoadingTransportType.Wagon,
            LoadedDate = new DateTime(2026, 5, 2),
            QuantityMt = 20m,
            Status = InventoryTransportLegStatus.Loaded
        });
        await db.SaveChangesAsync();

        var model = await GetMissingLedgerModelAsync(db);

        var issue = Assert.Single(model.InventoryTransportLegIssues);
        Assert.Equal(800, issue.LegId);
        Assert.Equal("Transport leg is loaded but has no outbound inventory movement.", issue.Issue);
    }

    [Fact]
    public async Task MissingLedger_Flags_TransportLeg_With_Missing_OutboundMovement()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        SeedTransportLegLookups(db);
        db.InventoryTransportLegs.Add(new InventoryTransportLeg
        {
            Id = 801,
            SourcePurchaseContractId = 2,
            ProductId = 1,
            SourceTerminalId = 1,
            SourceStorageTankId = 1,
            TransportType = LoadingTransportType.Wagon,
            LoadedDate = new DateTime(2026, 5, 2),
            QuantityMt = 20m,
            Status = InventoryTransportLegStatus.Loaded,
            OutboundInventoryMovementId = 999
        });
        await db.SaveChangesAsync();

        var model = await GetMissingLedgerModelAsync(db);

        var issue = Assert.Single(model.InventoryTransportLegIssues);
        Assert.Equal(801, issue.LegId);
        Assert.Equal(999, issue.MovementId);
        Assert.Equal("Transport leg references missing outbound inventory movement.", issue.Issue);
    }

    [Fact]
    public async Task MissingLedger_Flags_TransportLeg_Movement_Quantity_Mismatch()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        SeedTransportLegLookups(db);
        db.InventoryMovements.Add(new InventoryMovement
        {
            Id = 8020,
            ProductId = 1,
            ContractId = 2,
            TerminalId = 1,
            StorageTankId = 1,
            Direction = MovementDirection.Out,
            MovementDate = new DateTime(2026, 5, 2),
            QuantityMt = 19m,
            ReferenceDocument = "TRANSPORT-LEG:802"
        });
        db.InventoryTransportLegs.Add(new InventoryTransportLeg
        {
            Id = 802,
            SourcePurchaseContractId = 2,
            ProductId = 1,
            SourceTerminalId = 1,
            SourceStorageTankId = 1,
            TransportType = LoadingTransportType.Wagon,
            LoadedDate = new DateTime(2026, 5, 2),
            QuantityMt = 20m,
            Status = InventoryTransportLegStatus.Loaded,
            OutboundInventoryMovementId = 8020
        });
        await db.SaveChangesAsync();

        var model = await GetMissingLedgerModelAsync(db);

        var issue = Assert.Single(model.InventoryTransportLegIssues);
        Assert.Equal(802, issue.LegId);
        Assert.Equal("Transport leg quantity does not match outbound movement quantity.", issue.Issue);
    }

    [Fact]
    public async Task MissingLedger_Flags_TransportLeg_Movement_Source_Mismatch()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        SeedTransportLegLookups(db);
        db.InventoryMovements.Add(new InventoryMovement
        {
            Id = 8030,
            ProductId = 1,
            ContractId = 2,
            TerminalId = 2,
            StorageTankId = 1,
            Direction = MovementDirection.Out,
            MovementDate = new DateTime(2026, 5, 2),
            QuantityMt = 20m,
            ReferenceDocument = "TRANSPORT-LEG:803"
        });
        db.InventoryTransportLegs.Add(new InventoryTransportLeg
        {
            Id = 803,
            SourcePurchaseContractId = 2,
            ProductId = 1,
            SourceTerminalId = 1,
            SourceStorageTankId = 1,
            TransportType = LoadingTransportType.Wagon,
            LoadedDate = new DateTime(2026, 5, 2),
            QuantityMt = 20m,
            Status = InventoryTransportLegStatus.Loaded,
            OutboundInventoryMovementId = 8030
        });
        await db.SaveChangesAsync();

        var model = await GetMissingLedgerModelAsync(db);

        var issue = Assert.Single(model.InventoryTransportLegIssues);
        Assert.Equal(803, issue.LegId);
        Assert.Equal("Outbound movement does not match transport leg source.", issue.Issue);
    }

    [Fact]
    public async Task MissingLedger_Does_Not_Flag_Healthy_Loaded_TransportLeg()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        SeedTransportLegLookups(db);
        db.InventoryMovements.Add(new InventoryMovement
        {
            Id = 8040,
            ProductId = 1,
            ContractId = 2,
            TerminalId = 1,
            StorageTankId = 1,
            Direction = MovementDirection.Out,
            MovementDate = new DateTime(2026, 5, 2),
            QuantityMt = 20m,
            ReferenceDocument = "TRANSPORT-LEG:804"
        });
        db.InventoryTransportLegs.Add(new InventoryTransportLeg
        {
            Id = 804,
            SourcePurchaseContractId = 2,
            ProductId = 1,
            SourceTerminalId = 1,
            SourceStorageTankId = 1,
            TransportType = LoadingTransportType.Wagon,
            LoadedDate = new DateTime(2026, 5, 2),
            QuantityMt = 20m,
            Status = InventoryTransportLegStatus.Loaded,
            OutboundInventoryMovementId = 8040
        });
        await db.SaveChangesAsync();

        var model = await GetMissingLedgerModelAsync(db);

        Assert.Empty(model.InventoryTransportLegIssues);
    }

    [Fact]
    public async Task MissingLedger_Warns_When_Cancelled_TransportLeg_Has_OutboundMovement()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        SeedTransportLegLookups(db);
        db.InventoryMovements.Add(new InventoryMovement
        {
            Id = 8050,
            ProductId = 1,
            ContractId = 2,
            TerminalId = 1,
            StorageTankId = 1,
            Direction = MovementDirection.Out,
            MovementDate = new DateTime(2026, 5, 2),
            QuantityMt = 20m,
            ReferenceDocument = "TRANSPORT-LEG:805"
        });
        db.InventoryTransportLegs.Add(new InventoryTransportLeg
        {
            Id = 805,
            SourcePurchaseContractId = 2,
            ProductId = 1,
            SourceTerminalId = 1,
            SourceStorageTankId = 1,
            TransportType = LoadingTransportType.Wagon,
            LoadedDate = new DateTime(2026, 5, 2),
            QuantityMt = 20m,
            Status = InventoryTransportLegStatus.Cancelled,
            OutboundInventoryMovementId = 8050
        });
        await db.SaveChangesAsync();

        var model = await GetMissingLedgerModelAsync(db);

        var issue = Assert.Single(model.InventoryTransportLegIssues);
        Assert.Equal(805, issue.LegId);
        Assert.Equal("Warning", issue.Status);
        Assert.Equal("Cancelled transport leg still has outbound movement.", issue.Issue);
    }

    [Fact]
    public async Task MissingLedger_Flags_TransportLeg_Receipt_Expense_Loss_And_Cancelled_Link_Issues()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        SeedTransportLegLookups(db);
        db.ExpenseTypes.Add(new ExpenseType { Id = 900, Code = "LEG", Name = "Leg expense" });
        db.InventoryTransportLegs.AddRange(
            new InventoryTransportLeg
            {
                Id = 900,
                SourcePurchaseContractId = 2,
                ProductId = 1,
                SourceTerminalId = 1,
                SourceStorageTankId = 1,
                TransportType = LoadingTransportType.Wagon,
                LoadedDate = new DateTime(2026, 5, 2),
                QuantityMt = 20m,
                Status = InventoryTransportLegStatus.Received
            },
            new InventoryTransportLeg
            {
                Id = 901,
                SourcePurchaseContractId = 2,
                ProductId = 1,
                SourceTerminalId = 1,
                SourceStorageTankId = 1,
                TransportType = LoadingTransportType.Wagon,
                LoadedDate = new DateTime(2026, 5, 2),
                QuantityMt = 20m,
                Status = InventoryTransportLegStatus.Received
            },
            new InventoryTransportLeg
            {
                Id = 902,
                SourcePurchaseContractId = 2,
                ProductId = 1,
                SourceTerminalId = 1,
                SourceStorageTankId = 1,
                TransportType = LoadingTransportType.Wagon,
                LoadedDate = new DateTime(2026, 5, 2),
                QuantityMt = 20m,
                Status = InventoryTransportLegStatus.Loaded
            },
            new InventoryTransportLeg
            {
                Id = 903,
                SourcePurchaseContractId = 2,
                ProductId = 1,
                SourceTerminalId = 1,
                SourceStorageTankId = 1,
                TransportType = LoadingTransportType.Wagon,
                LoadedDate = new DateTime(2026, 5, 2),
                QuantityMt = 20m,
                Status = InventoryTransportLegStatus.Received
            },
            new InventoryTransportLeg
            {
                Id = 904,
                SourcePurchaseContractId = 2,
                ProductId = 1,
                SourceTerminalId = 1,
                SourceStorageTankId = 1,
                TransportType = LoadingTransportType.Wagon,
                LoadedDate = new DateTime(2026, 5, 2),
                QuantityMt = 20m,
                Status = InventoryTransportLegStatus.Cancelled
            });
        db.InventoryTransportReceipts.AddRange(
            new InventoryTransportReceipt
            {
                Id = 901,
                InventoryTransportLegId = 901,
                ReceiptDate = new DateTime(2026, 5, 4),
                ReceivedQuantityMt = 18m,
                ShortageQuantityMt = 1m,
                ReceiptDestination = InventoryTransportReceiptDestination.ToInventory
            },
            new InventoryTransportReceipt
            {
                Id = 904,
                InventoryTransportLegId = 904,
                ReceiptDate = new DateTime(2026, 5, 4),
                ReceivedQuantityMt = 20m,
                ReceiptDestination = InventoryTransportReceiptDestination.ToInventory
            });
        db.ExpenseTransactions.AddRange(
            new ExpenseTransaction
            {
                Id = 902,
                ExpenseTypeId = 900,
                TransportLegId = 902,
                ExpenseDate = new DateTime(2026, 5, 3),
                Amount = 50m,
                AmountUsd = 50m,
                Currency = "USD"
            },
            new ExpenseTransaction
            {
                Id = 904,
                ExpenseTypeId = 900,
                ContractId = 2,
                TransportLegId = 904,
                ExpenseDate = new DateTime(2026, 5, 3),
                Amount = 50m,
                AmountUsd = 50m,
                Currency = "USD"
            });
        db.LossEvents.AddRange(
            new LossEvent
            {
                Id = 903,
                ProductId = 1,
                ContractId = 2,
                TransportLegId = 903,
                Stage = LossEventStage.ReceiptShortage,
                EventDate = new DateTime(2026, 5, 5),
                ChargeableLossMt = 2m
            },
            new LossEvent
            {
                Id = 904,
                ProductId = 1,
                ContractId = 2,
                TransportLegId = 904,
                Stage = LossEventStage.ReceiptShortage,
                EventDate = new DateTime(2026, 5, 5),
                ChargeableLossMt = 1m
            });
        db.CustomsDeclarations.Add(new CustomsDeclaration
        {
            Id = 904,
            TransportLegId = 904,
            DeclarationDate = new DateTime(2026, 5, 4)
        });
        await db.SaveChangesAsync();

        var model = await GetMissingLedgerModelAsync(db);

        Assert.Contains(model.InventoryTransportLegIssues, i => i.LegId == 900 && i.Issue == "Transport leg is received but has no destination receipt.");
        Assert.Contains(model.InventoryTransportLegIssues, i => i.LegId == 901 && i.Issue == "Destination receipt quantity does not match transport leg quantity.");
        Assert.Contains(model.InventoryTransportLegIssues, i => i.LegId == 902 && i.Issue == "Transport leg expense has no purchase contract for P&L.");
        Assert.Contains(model.InventoryTransportLegIssues, i => i.LegId == 903 && i.Issue == "Transport leg loss cannot be valued from source purchase price.");
        Assert.Contains(model.InventoryTransportLegIssues, i => i.LegId == 904 && i.Issue == "Cancelled transport leg has active linked records.");
    }

    [Fact]
    public void MissingLedger_View_Shows_InventoryTransportLeg_Issue_Section()
    {
        var viewPath = GetProjectFilePath("src", "PTGOilSystem.Web", "Views", "Reconciliation", "MissingLedger.cshtml");
        var content = File.ReadAllText(viewPath);

        Assert.Contains("مشکلات انتقال از موجودی", content);
        Assert.Contains("InventoryTransportLegIssues", content);
    }

    [Fact]
    public async Task Reconciliation_LoadedByContract_Remains_The_Same()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        db.LoadingRegisters.AddRange(
            new LoadingRegister
            {
                Id = 700,
                ContractId = 2,
                ProductId = 1,
                LoadingDate = new DateTime(2026, 5, 1),
                LoadedQuantityMt = 30m
            },
            new LoadingRegister
            {
                Id = 701,
                ContractId = 2,
                ProductId = 1,
                LoadingDate = new DateTime(2026, 5, 2),
                LoadedQuantityMt = 20m
            });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.OpenContracts();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OpenContractsViewModel>(view.Model);
        var row = Assert.Single(model.Rows, r => r.ContractId == 2);
        Assert.Equal(50m, row.LoadedQuantityMt);
        Assert.Equal(50m, row.RemainingQuantityMt);
    }

    [Fact]
    public async Task OpenContracts_LoadedByContract_Ignores_InventoryTransportLeg()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        db.LoadingRegisters.Add(new LoadingRegister
        {
            Id = 710,
            ContractId = 2,
            ProductId = 1,
            LoadingDate = new DateTime(2026, 5, 1),
            LoadedQuantityMt = 30m
        });
        db.InventoryTransportLegs.Add(new InventoryTransportLeg
        {
            Id = 7100,
            SourcePurchaseContractId = 2,
            ProductId = 1,
            SourceTerminalId = 1,
            TransportType = LoadingTransportType.Wagon,
            WagonNumber = "WGN-7100",
            RwbNo = "RWB-7100",
            LoadedDate = new DateTime(2026, 5, 2),
            QuantityMt = 20m,
            Status = InventoryTransportLegStatus.InTransit
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.OpenContracts();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OpenContractsViewModel>(view.Model);
        var row = Assert.Single(model.Rows, r => r.ContractId == 2);
        Assert.Equal(30m, row.LoadedQuantityMt);
        Assert.Equal(70m, row.RemainingQuantityMt);
    }

    [Fact]
    public async Task Roznamcha_Reconciliation_Flags_Cash_Ledger_Profile_And_Expense_Risks()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        db.CashAccounts.Add(new CashAccount
        {
            Id = 1,
            Code = "CASH-USD",
            Name = "Main USD Cash",
            AccountType = CashAccountType.Cash,
            Currency = "USD",
            IsActive = true
        });
        db.ExpenseTypes.Add(new ExpenseType { Id = 1, Code = "OFFICE", Name = "Office" });
        db.ExpenseTransactions.Add(new ExpenseTransaction
        {
            Id = 1,
            ExpenseTypeId = 1,
            ExpenseDate = new DateTime(2026, 4, 20),
            Amount = 30m,
            Currency = "USD",
            AppliedFxRateToUsd = 1m,
            AmountUsd = 30m,
            Description = "Office rent"
        });
        db.PaymentTransactions.AddRange(
            new PaymentTransaction
            {
                Id = 1,
                PaymentDate = new DateTime(2026, 4, 20),
                Direction = PaymentDirection.In,
                PaymentKind = PaymentKind.CustomerReceipt,
                CashAccountId = 1,
                Amount = 100m,
                Currency = "USD",
                AppliedFxRateToUsd = 1m,
                AmountUsd = 100m,
                Reference = "NO-CUSTOMER",
                LedgerEntryId = 10
            },
            new PaymentTransaction
            {
                Id = 2,
                PaymentDate = new DateTime(2026, 4, 21),
                Direction = PaymentDirection.Out,
                PaymentKind = PaymentKind.SupplierPayment,
                CashAccountId = 0,
                Amount = 50m,
                Currency = "USD",
                AppliedFxRateToUsd = 1m,
                AmountUsd = 50m,
                Reference = "NO-CASH-SUPPLIER"
            },
            new PaymentTransaction
            {
                Id = 3,
                PaymentDate = new DateTime(2026, 4, 22),
                Direction = PaymentDirection.Out,
                PaymentKind = PaymentKind.ExpensePayment,
                CashAccountId = 1,
                ExpenseTransactionId = 1,
                Amount = 30m,
                Currency = "USD",
                AppliedFxRateToUsd = 1m,
                AmountUsd = 30m,
                Reference = "EXP-PAID",
                LedgerEntryId = 20
            });
        db.LedgerEntries.AddRange(
            new LedgerEntry
            {
                Id = 10,
                EntryDate = new DateTime(2026, 4, 20),
                Side = LedgerSide.Debit,
                AmountUsd = 90m,
                SourceType = nameof(PaymentKind.CustomerReceipt),
                SourceId = 1,
                Description = "Mismatch",
                Reference = "NO-CUSTOMER"
            },
            new LedgerEntry
            {
                Id = 20,
                EntryDate = new DateTime(2026, 4, 22),
                Side = LedgerSide.Debit,
                AmountUsd = 30m,
                SourceType = nameof(PaymentKind.ExpensePayment),
                SourceId = 3,
                Description = "Expense payment",
                Reference = "EXP-PAID"
            },
            new LedgerEntry
            {
                Id = 21,
                EntryDate = new DateTime(2026, 4, 20),
                Side = LedgerSide.Debit,
                AmountUsd = 30m,
                SourceType = "Expense",
                SourceId = 1,
                Description = "Expense ledger"
            });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);
        var result = await controller.Roznamcha();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<RoznamchaReconciliationViewModel>(view.Model);
        Assert.Contains(model.SupplierCustomerPaymentsWithoutProfileLink, row => row.PaymentTransactionId == 1);
        Assert.Contains(model.SupplierCustomerPaymentsWithoutProfileLink, row => row.PaymentTransactionId == 2);
        Assert.Contains(model.PaymentsWithoutCashAccount, row => row.PaymentTransactionId == 2);
        Assert.Contains(model.LedgerAmountMismatches, row => row.PaymentTransactionId == 1);
        Assert.Contains(model.ExpenseDoubleCountingRisks, row => row.PaymentTransactionId == 3);
    }

    [Fact]
    public async Task MissingLedger_Flags_Supplier_Payment_And_Ledger_Mismatches()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        db.Suppliers.Add(new Supplier { Id = 2, Name = "Supplier B" });
        db.Contracts.Add(new Contract
        {
            Id = 3,
            ContractNumber = "PUR-SUP-B",
            ContractType = ContractType.Purchase,
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 2,
            ContractDate = new DateTime(2026, 1, 2),
            QuantityMt = 20m,
            PricingMethod = PricingMethod.Fixed,
            UnitPriceUsd = 400m
        });
        db.PaymentTransactions.AddRange(
            new PaymentTransaction
            {
                Id = 1,
                PaymentDate = new DateTime(2026, 4, 1),
                Direction = PaymentDirection.Out,
                PaymentKind = PaymentKind.SupplierPayment,
                CashAccountId = 1,
                Amount = 100m,
                Currency = "USD",
                AppliedFxRateToUsd = 1m,
                AmountUsd = 100m,
                Reference = "SUP-NO-SUPPLIER"
            },
            new PaymentTransaction
            {
                Id = 2,
                PaymentDate = new DateTime(2026, 4, 2),
                Direction = PaymentDirection.Out,
                PaymentKind = PaymentKind.SupplierPayment,
                CashAccountId = 1,
                SupplierId = 1,
                ContractId = 3,
                Amount = 150m,
                Currency = "USD",
                AppliedFxRateToUsd = 1m,
                AmountUsd = 150m,
                Reference = "SUP-MISMATCH"
            },
            new PaymentTransaction
            {
                Id = 3,
                PaymentDate = new DateTime(2026, 4, 3),
                Direction = PaymentDirection.Out,
                PaymentKind = PaymentKind.SupplierPayment,
                CashAccountId = 1,
                SupplierId = 1,
                ContractId = 2,
                Amount = 200m,
                Currency = "USD",
                AppliedFxRateToUsd = 1m,
                AmountUsd = 200m,
                Reference = "SUP-LEDGER-MISMATCH",
                LedgerEntryId = 30
            },
            new PaymentTransaction
            {
                Id = 4,
                PaymentDate = new DateTime(2026, 4, 4),
                Direction = PaymentDirection.Out,
                PaymentKind = PaymentKind.SupplierPayment,
                CashAccountId = 1,
                SupplierId = 1,
                ContractId = 2,
                Amount = 250m,
                Currency = "USD",
                AppliedFxRateToUsd = 1m,
                AmountUsd = 250m,
                Reference = "SUP-NO-LEDGER",
                LedgerEntryId = 999
            });
        db.LedgerEntries.AddRange(
            new LedgerEntry
            {
                Id = 30,
                EntryDate = new DateTime(2026, 4, 3),
                Side = LedgerSide.Debit,
                AmountUsd = 200m,
                Currency = "USD",
                SourceAmount = 200m,
                SourceCurrencyCode = "USD",
                AppliedFxRateToUsd = 1m,
                SourceType = "SupplierPayment",
                SourceId = 3,
                Description = "Ledger without supplier and contract"
            },
            new LedgerEntry
            {
                Id = 40,
                EntryDate = new DateTime(2026, 4, 5),
                Side = LedgerSide.Credit,
                AmountUsd = 0m,
                Currency = "USD",
                SourceAmount = 100m,
                SourceCurrencyCode = "EUR",
                SourceType = "Adjustment",
                SourceId = 40,
                Description = "Bad FX",
                SupplierId = 1
            });
        await db.SaveChangesAsync();

        var model = await GetMissingLedgerModelAsync(db);

        Assert.Contains(model.SupplierPaymentsWithoutSupplier, row => row.PaymentId == 1);
        Assert.Contains(model.SupplierPaymentContractSupplierMismatches, row => row.PaymentId == 2);
        Assert.Contains(model.SupplierPaymentLedgerMissingSupplierOrContract, row => row.PaymentId == 3);
        Assert.Contains(model.SupplierPaymentsWithoutLedger, row => row.PaymentId == 4);
        Assert.Contains(model.SupplierLedgerFxIssues, row => row.LedgerEntryId == 40);
    }

    [Fact]
    public async Task SuspenseMoney_Flags_Unlinked_Payments_Ledger_And_Undocumented_Sarraf_Difference()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        db.Sarrafs.Add(new Sarraf { Id = 1, Name = "Sarraf A" });

        // پرداخت بدون طرف حساب و بدون سند → Critical
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 1,
            PaymentDate = new DateTime(2026, 5, 1),
            Direction = PaymentDirection.Out,
            PaymentKind = PaymentKind.ManualPayment,
            CashAccountId = 1,
            Amount = 100m,
            Currency = "USD",
            AmountUsd = 100m
        });
        // پرداخت تأمین‌کننده بدون قرارداد → Warning
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 2,
            PaymentDate = new DateTime(2026, 5, 2),
            Direction = PaymentDirection.Out,
            PaymentKind = PaymentKind.SupplierPayment,
            SupplierId = 1,
            CashAccountId = 1,
            Amount = 200m,
            Currency = "USD",
            AmountUsd = 200m
        });
        // پرداخت تأمین‌کننده سالم (طرف حساب + قرارداد) → نباید معلق شمرده شود
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 3,
            PaymentDate = new DateTime(2026, 5, 3),
            Direction = PaymentDirection.Out,
            PaymentKind = PaymentKind.SupplierPayment,
            SupplierId = 1,
            ContractId = 1,
            CashAccountId = 1,
            Amount = 300m,
            Currency = "USD",
            AmountUsd = 300m
        });
        // دفتر کل با منبع نامشخص → Critical
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = 1,
            EntryDate = new DateTime(2026, 5, 4),
            Side = LedgerSide.Debit,
            AmountUsd = 10m,
            Currency = "USD",
            Description = "Unlinked entry",
            SourceType = "",
            SourceId = 0
        });
        // تسویه صراف با تفاوت اما بدون دلیل → Warning
        db.SarrafSettlements.Add(new SarrafSettlement
        {
            Id = 1,
            SettlementDate = new DateTime(2026, 5, 5),
            SarrafId = 1,
            SupplierId = 1,
            Status = SarrafSettlementStatus.Posted,
            DifferenceType = SarrafSettlementDifferenceType.Loss,
            DifferenceAmountUsd = 20m,
            Description = null
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.SuspenseMoney();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SuspenseMoneyViewModel>(view.Model);

        Assert.Contains(model.Items, i =>
            i.IssueSource == "طرف حساب و سند نامشخص" && i.Severity == SuspenseSeverity.Critical);
        Assert.Contains(model.Items, i =>
            i.IssueSource == "قرارداد مشخص نیست" && i.Severity == SuspenseSeverity.Warning);
        Assert.Contains(model.Items, i =>
            i.IssueSource == "سند منبع نامشخص" && i.Severity == SuspenseSeverity.Critical);
        Assert.Contains(model.Items, i =>
            i.IssueSource == "دلیل تفاوت نوشته نشده" && i.Severity == SuspenseSeverity.Warning);

        // پرداخت سالم نباید معلق شمرده شود.
        Assert.DoesNotContain(model.Items, i =>
            i.DetailsController == "Payments" && i.DetailsRouteId == 3);

        // پرداخت‌های مشکل‌دار لینک «وصل کن» به فرم ویرایش دارند.
        Assert.Contains(model.Items, i =>
            i.DetailsRouteId == 1 && i.ConnectController == "Payments" && i.ConnectAction == "Edit");
    }

    [Fact]
    public async Task SuspenseMoney_Does_Not_Flag_Sarraf_Difference_When_Reason_Is_Recorded()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        db.Sarrafs.Add(new Sarraf { Id = 1, Name = "Sarraf A" });
        // تفاوت با دلیل ثبت‌شده → نباید معلق باشد.
        db.SarrafSettlements.Add(new SarrafSettlement
        {
            Id = 1,
            SettlementDate = new DateTime(2026, 5, 5),
            SarrafId = 1,
            SupplierId = 1,
            Status = SarrafSettlementStatus.Posted,
            DifferenceType = SarrafSettlementDifferenceType.Loss,
            DifferenceAmountUsd = 20m,
            DifferenceReason = DifferenceReason.Commission,
            Description = null
        });
        // تفاوت بدون دلیل و بدون شرح → باید معلق باشد.
        db.SarrafSettlements.Add(new SarrafSettlement
        {
            Id = 2,
            SettlementDate = new DateTime(2026, 5, 6),
            SarrafId = 1,
            SupplierId = 1,
            Status = SarrafSettlementStatus.Posted,
            DifferenceType = SarrafSettlementDifferenceType.Loss,
            DifferenceAmountUsd = 30m,
            DifferenceReason = null,
            Description = null
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.SuspenseMoney();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SuspenseMoneyViewModel>(view.Model);

        var sarrafItems = model.Items
            .Where(i => i.DetailsController == "SarrafSettlements")
            .ToList();
        var flagged = Assert.Single(sarrafItems);
        Assert.Equal(2, flagged.DetailsRouteId);
    }

    [Fact]
    public async Task SuspenseMoney_Does_Not_Flag_Valid_ThreeWaySettlement_Cancellation_Trace()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        SeedPurchaseContract(db);
        db.LedgerEntries.AddRange(
            new LedgerEntry
            {
                Id = 300,
                EntryDate = new DateTime(2026, 6, 6),
                Side = LedgerSide.Debit,
                AmountUsd = 1000m,
                Currency = "USD",
                SourceType = ThreeWaySettlementController.LedgerSourceType,
                SourceId = 300,
                Reference = "HW-300",
                Description = "Three-way customer",
                CustomerId = 1,
                ContractId = 1
            },
            new LedgerEntry
            {
                Id = 301,
                EntryDate = new DateTime(2026, 6, 6),
                Side = LedgerSide.Debit,
                AmountUsd = 950m,
                Currency = "USD",
                SourceType = ThreeWaySettlementController.LedgerSourceType,
                SourceId = 300,
                Reference = "HW-300",
                Description = "Three-way supplier",
                SupplierId = 1,
                ContractId = 2
            },
            new LedgerEntry
            {
                Id = 302,
                EntryDate = new DateTime(2026, 6, 7),
                Side = LedgerSide.Credit,
                AmountUsd = 1000m,
                Currency = "USD",
                SourceType = ThreeWaySettlementController.CancellationLedgerSourceType,
                SourceId = 300,
                Reference = "HW-300",
                Description = "Three-way customer cancellation",
                CustomerId = 1,
                ContractId = 1
            },
            new LedgerEntry
            {
                Id = 303,
                EntryDate = new DateTime(2026, 6, 7),
                Side = LedgerSide.Credit,
                AmountUsd = 950m,
                Currency = "USD",
                SourceType = ThreeWaySettlementController.CancellationLedgerSourceType,
                SourceId = 300,
                Reference = "HW-300",
                Description = "Three-way supplier cancellation",
                SupplierId = 1,
                ContractId = 2
            });
        db.ThreeWaySettlements.Add(new ThreeWaySettlement
        {
            Id = 300,
            SettlementDate = new DateTime(2026, 6, 6),
            Status = ThreeWaySettlementStatus.Cancelled,
            PayeeType = ThreeWayPayeeType.Supplier,
            CustomerId = 1,
            SupplierId = 1,
            CustomerPaidAmount = 1000m,
            SupplierAcceptedAmount = 950m,
            Currency = "USD",
            FxRateToUsd = 1m,
            CustomerPaidUsd = 1000m,
            SupplierAcceptedUsd = 950m,
            DifferenceUsd = 50m,
            DifferenceReason = DifferenceReason.Commission,
            CustomerSaleContractId = 1,
            SupplierPurchaseContractId = 2,
            HawalaReference = "HW-300",
            CustomerLedgerEntryId = 300,
            SupplierLedgerEntryId = 301,
            CancelledAtUtc = new DateTime(2026, 6, 7),
            CancelledByUserName = "tester",
            CancellationReason = "Valid reversal"
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.SuspenseMoney();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SuspenseMoneyViewModel>(view.Model);
        Assert.DoesNotContain(model.Items, i => i.DetailsController == "ThreeWaySettlement");
    }

    private static void SeedReferenceData(ApplicationDbContext db)
    {
        db.Products.Add(new Product { Id = 1, Code = "GAS", Name = "Gasoline" });
        db.Companies.Add(new Company { Id = 1, Code = "PTG", Name = "PTG" });
        db.Customers.Add(new Customer { Id = 1, Name = "Customer A" });
        db.Suppliers.Add(new Supplier { Id = 1, Name = "Supplier A" });
        db.Contracts.Add(new Contract
        {
            Id = 1,
            ContractNumber = "CON-1",
            ContractType = ContractType.Sale,
            CompanyId = 1,
            ProductId = 1,
            CustomerId = 1,
            SupplierId = 1,
            ContractDate = new DateTime(2026, 1, 1),
            QuantityMt = 100m,
            PricingMethod = PricingMethod.Fixed,
            UnitPriceUsd = 500m
        });
    }

    private static void SeedPurchaseContract(ApplicationDbContext db)
    {
        db.Contracts.Add(new Contract
        {
            Id = 2,
            ContractNumber = "PUR-1",
            ContractType = ContractType.Purchase,
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 1,
            ContractDate = new DateTime(2026, 1, 1),
            QuantityMt = 100m,
            PricingMethod = PricingMethod.Fixed,
            UnitPriceUsd = 500m
        });
    }

    private static void SeedTransportLegLookups(ApplicationDbContext db)
    {
        db.Terminals.AddRange(
            new Terminal { Id = 1, Code = "T1", Name = "Terminal 1" },
            new Terminal { Id = 2, Code = "T2", Name = "Terminal 2" });
        db.StorageTanks.Add(new StorageTank { Id = 1, TerminalId = 1, TankCode = "TK-1", ProductId = 1, CapacityMt = 500m });
    }

    private static async Task<MissingLedgerViewModel> GetMissingLedgerModelAsync(ApplicationDbContext db)
    {
        var controller = new ReconciliationController(db);
        var result = await controller.MissingLedger();
        var view = Assert.IsType<ViewResult>(result);
        return Assert.IsType<MissingLedgerViewModel>(view.Model);
    }

    private static string GetProjectFilePath(params string[] segments)
    {
        var relativePath = Path.Combine(segments);
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath));
    }

    private static void SeedLoadingReceipt(ApplicationDbContext db)
    {
        db.Terminals.Add(new Terminal { Id = 1, Code = "T1", Name = "Terminal 1" });
        db.LoadingRegisters.Add(new LoadingRegister
        {
            Id = 1,
            ContractId = 1,
            ProductId = 1,
            LoadingDate = new DateTime(2026, 4, 1),
            LoadedQuantityMt = 20m
        });
        db.LoadingReceipts.Add(new LoadingReceipt
        {
            Id = 1,
            LoadingRegisterId = 1,
            TerminalId = 1,
            ReceiptDate = new DateTime(2026, 4, 2),
            ReceivedQuantityMt = 20m
        });
    }
}
