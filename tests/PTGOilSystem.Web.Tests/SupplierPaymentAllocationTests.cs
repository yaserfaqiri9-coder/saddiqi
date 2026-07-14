using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Models.Payments;
using PTGOilSystem.Web.Services;
using PTGOilSystem.Web.Services.Audit;
using PTGOilSystem.Web.Services.Exceptions;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class SupplierPaymentAllocationTests
{
    // 1) پرداخت 1,000,000 USD و تخصیص 650,000 USD.
    [Fact]
    public async Task Allocation_650k_Locks_Book_And_Contract_Currency_Amounts()
    {
        await using var db = await NewSeededDbAsync();
        var service = new SupplierPaymentAllocationService(db);

        var allocation = await service.CreateAsync(new SupplierPaymentAllocationCreateRequest(
            10, 1, new DateTime(2026, 2, 2), 650000m, 80m, "AL-1", null, "tester"));

        Assert.Equal(650000m, allocation.AllocatedBookAmountUsd);
        Assert.Equal(80m, allocation.ContractCurrencyPerUsdRate);
        Assert.Equal(0.0125m, allocation.ContractCurrencyFxRateToUsd);
        Assert.Equal(52000000m, allocation.AllocatedContractCurrencyAmount);
        Assert.Equal("RUB", allocation.ContractCurrencyCode);
        Assert.Equal(SupplierPaymentAllocationStatus.Active, allocation.Status);
    }

    // 2) مانده بعد از تخصیص برابر 350,000 USD.
    [Fact]
    public async Task Allocatable_Balance_After_650k_Is_350k()
    {
        await using var db = await NewSeededDbAsync();
        var service = new SupplierPaymentAllocationService(db);

        await service.CreateAsync(new SupplierPaymentAllocationCreateRequest(
            10, 1, new DateTime(2026, 2, 2), 650000m, 80m, null, null, null));

        Assert.Equal(350000m, await service.GetAllocatableBalanceUsdAsync(10));
    }

    // 3) تخصیص دوم 200,000 USD و مانده نهایی 150,000 USD.
    [Fact]
    public async Task Second_Allocation_200k_Leaves_150k()
    {
        await using var db = await NewSeededDbAsync();
        var service = new SupplierPaymentAllocationService(db);

        await service.CreateAsync(new SupplierPaymentAllocationCreateRequest(
            10, 1, new DateTime(2026, 2, 2), 650000m, 80m, null, null, null));
        var second = await service.CreateAsync(new SupplierPaymentAllocationCreateRequest(
            10, 2, new DateTime(2026, 2, 3), 200000m, 82.5m, null, null, null));

        Assert.Equal(200000m, second.AllocatedBookAmountUsd);
        Assert.Equal(16500000m, second.AllocatedContractCurrencyAmount);
        Assert.Equal(150000m, await service.GetAllocatableBalanceUsdAsync(10));
    }

    // 4) جلوگیری از تخصیص بیشتر از مانده.
    [Fact]
    public async Task Allocation_Exceeding_Balance_Is_Rejected()
    {
        await using var db = await NewSeededDbAsync();
        var service = new SupplierPaymentAllocationService(db);

        await service.CreateAsync(new SupplierPaymentAllocationCreateRequest(
            10, 1, new DateTime(2026, 2, 2), 900000m, 80m, null, null, null));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(
            new SupplierPaymentAllocationCreateRequest(10, 2, new DateTime(2026, 2, 3), 200000m, 80m, null, null, null)));

        Assert.Equal("SUPPLIER_PAYMENT_ALLOCATION_EXCEEDS_BALANCE", ex.Code);
        Assert.Equal(100000m, await service.GetAllocatableBalanceUsdAsync(10));
    }

    // 5) جلوگیری از قرارداد متعلق به تأمین‌کننده دیگر.
    [Fact]
    public async Task Allocation_To_Other_Suppliers_Contract_Is_Rejected()
    {
        await using var db = await NewSeededDbAsync();
        var service = new SupplierPaymentAllocationService(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(
            new SupplierPaymentAllocationCreateRequest(10, 3, new DateTime(2026, 2, 2), 100000m, 80m, null, null, null)));

        Assert.Equal("SUPPLIER_PAYMENT_ALLOCATION_SUPPLIER_MISMATCH", ex.Code);
        Assert.Empty(db.SupplierPaymentAllocations);
    }

    // 6) قفل شدن نرخ هر تخصیص — تخصیص دوم با نرخ متفاوت، تخصیص اول را تغییر نمی‌دهد.
    [Fact]
    public async Task Each_Allocation_Locks_Its_Own_Rate()
    {
        await using var db = await NewSeededDbAsync();
        var service = new SupplierPaymentAllocationService(db);

        var first = await service.CreateAsync(new SupplierPaymentAllocationCreateRequest(
            10, 1, new DateTime(2026, 2, 2), 650000m, 80m, null, null, null));
        await service.CreateAsync(new SupplierPaymentAllocationCreateRequest(
            10, 2, new DateTime(2026, 2, 3), 200000m, 82.5m, null, null, null));

        var firstReloaded = await db.SupplierPaymentAllocations.AsNoTracking().SingleAsync(a => a.Id == first.Id);
        Assert.Equal(80m, firstReloaded.ContractCurrencyPerUsdRate);
        Assert.Equal(0.0125m, firstReloaded.ContractCurrencyFxRateToUsd);
        Assert.Equal(52000000m, firstReloaded.AllocatedContractCurrencyAmount);
    }

    // 7) صفر بودن اثر خالص Ledger روی مانده کلی تأمین‌کننده.
    [Fact]
    public async Task Allocation_Net_Effect_On_Supplier_Total_Is_Zero()
    {
        await using var db = await NewSeededDbAsync();
        var service = new SupplierPaymentAllocationService(db);

        var allocation = await service.CreateAsync(new SupplierPaymentAllocationCreateRequest(
            10, 1, new DateTime(2026, 2, 2), 650000m, 80m, null, null, null));

        var entries = await db.LedgerEntries
            .Where(l => l.SourceType == SupplierPaymentAllocationService.LedgerSourceType && l.SourceId == allocation.Id)
            .ToListAsync();

        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.Equal(1, e.SupplierId));
        var supplierNet = entries.Sum(e => e.Side == LedgerSide.Credit ? e.AmountUsd : -e.AmountUsd);
        Assert.Equal(0m, supplierNet);
    }

    // 8) انتقال مبلغ از پیش‌پرداخت آزاد به مانده قرارداد.
    [Fact]
    public async Task Allocation_Moves_Amount_From_Free_Advance_To_Contract()
    {
        await using var db = await NewSeededDbAsync();
        var service = new SupplierPaymentAllocationService(db);

        var allocation = await service.CreateAsync(new SupplierPaymentAllocationCreateRequest(
            10, 1, new DateTime(2026, 2, 2), 650000m, 80m, null, null, null));

        var entries = await db.LedgerEntries
            .Where(l => l.SourceType == SupplierPaymentAllocationService.LedgerSourceType && l.SourceId == allocation.Id)
            .ToListAsync();

        var freeEntry = Assert.Single(entries, e => e.ContractId == null);
        Assert.Equal(LedgerSide.Credit, freeEntry.Side);
        Assert.Equal(650000m, freeEntry.AmountUsd);

        var contractEntry = Assert.Single(entries, e => e.ContractId == 1);
        Assert.Equal(LedgerSide.Debit, contractEntry.Side);
        Assert.Equal(650000m, contractEntry.AmountUsd);
    }

    // 9) برگشت تخصیص با دو LedgerEntry معکوس.
    [Fact]
    public async Task Reversal_Creates_Two_Reverse_Entries_And_Restores_Balance()
    {
        await using var db = await NewSeededDbAsync();
        var service = new SupplierPaymentAllocationService(db);

        var allocation = await service.CreateAsync(new SupplierPaymentAllocationCreateRequest(
            10, 1, new DateTime(2026, 2, 2), 650000m, 80m, null, null, null));

        var reversed = await service.ReverseAsync(new SupplierPaymentAllocationReverseRequest(
            allocation.Id, "ثبت اشتباه", "tester"));

        Assert.Equal(SupplierPaymentAllocationStatus.Reversed, reversed.Status);
        Assert.Equal("ثبت اشتباه", reversed.ReversalReason);

        var reverseEntries = await db.LedgerEntries
            .Where(l => l.SourceType == SupplierPaymentAllocationService.ReversalLedgerSourceType && l.SourceId == allocation.Id)
            .ToListAsync();

        Assert.Equal(2, reverseEntries.Count);
        // ثبت‌های اصلی دست‌نخورده باقی می‌مانند.
        Assert.Equal(2, await db.LedgerEntries.CountAsync(l => l.SourceType == SupplierPaymentAllocationService.LedgerSourceType && l.SourceId == allocation.Id));
        // مانده قابل تخصیص دوباره کامل می‌شود.
        Assert.Equal(1000000m, await service.GetAllocatableBalanceUsdAsync(10));
    }

    // 10) جلوگیری از برگشت دوباره.
    [Fact]
    public async Task Double_Reversal_Is_Rejected()
    {
        await using var db = await NewSeededDbAsync();
        var service = new SupplierPaymentAllocationService(db);

        var allocation = await service.CreateAsync(new SupplierPaymentAllocationCreateRequest(
            10, 1, new DateTime(2026, 2, 2), 650000m, 80m, null, null, null));
        await service.ReverseAsync(new SupplierPaymentAllocationReverseRequest(allocation.Id, "بار اول", "tester"));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReverseAsync(
            new SupplierPaymentAllocationReverseRequest(allocation.Id, "بار دوم", "tester")));

        Assert.Equal("SUPPLIER_PAYMENT_ALLOCATION_ALREADY_REVERSED", ex.Code);
    }

    // 11) جلوگیری از ویرایش پرداخت دارای تخصیص فعال.
    [Fact]
    public async Task Editing_Payment_With_Active_Allocation_Is_Blocked()
    {
        await using var db = await NewSeededDbAsync();
        await new SupplierPaymentAllocationService(db).CreateAsync(new SupplierPaymentAllocationCreateRequest(
            10, 1, new DateTime(2026, 2, 2), 500000m, 80m, null, null, null));

        var controller = new PaymentsController(db, new PricingService(db), new AuditService(db), NullLogger<PaymentsController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        var result = await controller.Edit(10, new PaymentCreateViewModel
        {
            Id = 10,
            PaymentDate = new DateTime(2026, 2, 1),
            Direction = PaymentDirection.Out,
            PaymentKind = PaymentKind.SupplierPayment,
            CashAccountId = 1,
            SupplierId = 1,
            Amount = 900000m, // تغییر مبلغ — باید مسدود شود
            Currency = "USD"
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Create", view.ViewName);
        Assert.False(controller.ModelState.IsValid);

        var payment = await db.PaymentTransactions.AsNoTracking().SingleAsync(p => p.Id == 10);
        Assert.Equal(1000000m, payment.Amount); // بدون تغییر
    }

    // 12) عدم ایجاد FX Gain/Loss صرفاً به دلیل متفاوت بودن نرخ قراردادها.
    [Fact]
    public async Task Different_Contract_Rates_Do_Not_Create_Fx_Gain_Loss()
    {
        await using var db = await NewSeededDbAsync();
        var service = new SupplierPaymentAllocationService(db);

        await service.CreateAsync(new SupplierPaymentAllocationCreateRequest(
            10, 1, new DateTime(2026, 2, 2), 650000m, 80m, null, null, null));
        await service.CreateAsync(new SupplierPaymentAllocationCreateRequest(
            10, 2, new DateTime(2026, 2, 3), 200000m, 82.5m, null, null, null));

        var ledgerSourceTypes = await db.LedgerEntries.Select(l => l.SourceType).Distinct().ToListAsync();
        Assert.Single(ledgerSourceTypes);
        Assert.Equal(SupplierPaymentAllocationService.LedgerSourceType, ledgerSourceTypes[0]);
        Assert.Equal(4, await db.LedgerEntries.CountAsync()); // فقط 2 ثبت برای هر تخصیص، بدون ثبت سود/زیان نرخ
        Assert.Empty(db.SarrafSettlements);
    }

    // 13) حفظ کامل رفتار فعلی: تخصیص نباید Stock/Inventory/Loading/پرداخت جدید بسازد.
    [Fact]
    public async Task Allocation_Does_Not_Create_Inventory_Loading_Or_New_Payment()
    {
        await using var db = await NewSeededDbAsync();
        var service = new SupplierPaymentAllocationService(db);

        await service.CreateAsync(new SupplierPaymentAllocationCreateRequest(
            10, 1, new DateTime(2026, 2, 2), 650000m, 80m, null, null, null));

        Assert.Empty(db.InventoryMovements);
        Assert.Empty(db.LoadingRegisters);
        Assert.Equal(1, await db.PaymentTransactions.CountAsync()); // هیچ پرداخت جدیدی ساخته نشد
        Assert.Equal(1000000m, await db.PaymentTransactions.Where(p => p.Id == 10).Select(p => p.AmountUsd).SingleAsync());
    }

    private static async Task<ApplicationDbContext> NewSeededDbAsync()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        db.Products.Add(new Product { Id = 1, Code = "G92", Name = "Gasoline 92", UnitOfMeasure = "MT", IsActive = true });
        db.Companies.Add(new Company { Id = 1, Code = "PTG", Name = "PTG", Country = "AF", IsActive = true });
        db.CashAccounts.Add(new CashAccount { Id = 1, Code = "CASH-USD", Name = "Cash USD", Currency = "USD", IsActive = true });
        db.Suppliers.Add(new Supplier { Id = 1, Code = "SUP1", Name = "Supplier One", IsActive = true });
        db.Suppliers.Add(new Supplier { Id = 2, Code = "SUP2", Name = "Supplier Two", IsActive = true });

        db.Contracts.AddRange(
            new Contract { Id = 1, ContractNumber = "P-1", ContractType = ContractType.Purchase, Status = ContractStatus.Active, CompanyId = 1, ProductId = 1, SupplierId = 1, ContractDate = new DateTime(2026, 1, 1), PricingMethod = PricingMethod.ManualFinalPrice, QuantityMt = 1000m, Currency = "RUB" },
            new Contract { Id = 2, ContractNumber = "P-2", ContractType = ContractType.Purchase, Status = ContractStatus.Active, CompanyId = 1, ProductId = 1, SupplierId = 1, ContractDate = new DateTime(2026, 1, 2), PricingMethod = PricingMethod.ManualFinalPrice, QuantityMt = 1000m, Currency = "RUB" },
            new Contract { Id = 3, ContractNumber = "P-OTHER", ContractType = ContractType.Purchase, Status = ContractStatus.Active, CompanyId = 1, ProductId = 1, SupplierId = 2, ContractDate = new DateTime(2026, 1, 2), PricingMethod = PricingMethod.ManualFinalPrice, QuantityMt = 1000m, Currency = "RUB" });

        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 10,
            PaymentDate = new DateTime(2026, 2, 1),
            Direction = PaymentDirection.Out,
            PaymentKind = PaymentKind.SupplierPayment,
            CashAccountId = 1,
            SupplierId = 1,
            Amount = 1000000m,
            Currency = "USD",
            AppliedFxRateToUsd = 1m,
            AmountUsd = 1000000m,
            Reference = "ADV-1",
            IsAdvancePayment = true
        });

        await db.SaveChangesAsync();
        return db;
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
