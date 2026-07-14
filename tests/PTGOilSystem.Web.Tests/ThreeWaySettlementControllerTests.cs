using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Models.Reconciliation;
using PTGOilSystem.Web.Models.ThreeWaySettlement;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class ThreeWaySettlementControllerTests
{
    private static DbContextOptions<ApplicationDbContext> NewDbOptions()
        => new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task Preview_ForSupplier_ComputesEffects_AndCreatesNoFinancialRows()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        var controller = new ThreeWaySettlementController(db);

        var model = await PreviewAsync(controller, new ThreeWaySettlementPreviewViewModel
        {
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Supplier,
            SupplierId = 2,
            CustomerPaidAmount = 1000m,
            PayeeAcceptedAmount = 950m,
            Currency = "USD",
            FxRateToUsd = 1m,
            DifferenceReason = DifferenceReason.Commission
        });

        Assert.True(model.ShowPreview);
        Assert.Equal(1000m, model.CustomerPaidUsd);
        Assert.Equal(950m, model.PayeeAcceptedUsd);
        Assert.Equal(50m, model.DifferenceUsd);
        Assert.Equal("Customer A", model.CustomerName);
        Assert.Equal("Supplier A", model.PayeeName);
        Assert.Contains("تأمین‌کننده", model.PayeeImpactText);
        Assert.Equal(0m, model.CompanyCashBankDeltaUsd);
        Assert.Contains("پول وارد صندوق شرکت نمی‌شود", model.CompanyCashBankImpactText);
        Assert.True(model.CanPost);

        await AssertNoFinancialRowsAsync(db);
    }

    [Fact]
    public async Task Preview_ForSarraf_ShowsSarrafEffect_AndCreatesNoFinancialRows()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        var controller = new ThreeWaySettlementController(db);

        var model = await PreviewAsync(controller, new ThreeWaySettlementPreviewViewModel
        {
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Sarraf,
            SarrafId = 3,
            SupplierId = 2,
            CustomerPaidAmount = 500m,
            PayeeAcceptedAmount = 500m,
            Currency = "USD",
            FxRateToUsd = 1m
        });

        Assert.Equal("Sarraf A", model.PayeeName);
        Assert.True(model.SarrafInvolved);
        Assert.Contains("صراف", model.PayeeImpactText);
        // فاز A: صراف-به‌عنوان-واسطه با تأمین‌کننده نهایی مشخص، آماده ثبت است.
        Assert.True(model.CanPost);
        Assert.True(model.CanConfirmSarraf);
        Assert.Equal(0m, model.CompanyCashBankDeltaUsd);

        await AssertNoFinancialRowsAsync(db);
    }

    [Fact]
    public async Task Preview_ForOtherPayee_ShowsOtherAccountEffect_AndCreatesNoFinancialRows()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        var controller = new ThreeWaySettlementController(db);

        var model = await PreviewAsync(controller, new ThreeWaySettlementPreviewViewModel
        {
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.OtherAccount,
            OtherPayeeName = "Other Account",
            CustomerPaidAmount = 700m,
            PayeeAcceptedAmount = 700m,
            Currency = "USD",
            FxRateToUsd = 1m
        });

        Assert.Equal("Other Account", model.PayeeName);
        Assert.True(model.OtherAccountInvolved);
        Assert.Contains("حساب/طرف دیگر", model.PayeeImpactText);
        Assert.True(model.CanPost);
        Assert.Equal(0m, model.CompanyCashBankDeltaUsd);

        await AssertNoFinancialRowsAsync(db);
    }

    [Fact]
    public async Task Preview_DifferenceWithoutReason_IsNotReady()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        var controller = new ThreeWaySettlementController(db);

        var model = await PreviewAsync(controller, new ThreeWaySettlementPreviewViewModel
        {
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Supplier,
            SupplierId = 2,
            CustomerPaidAmount = 1000m,
            PayeeAcceptedAmount = 900m,
            Currency = "USD",
            FxRateToUsd = 1m,
            DifferenceReason = null
        });

        Assert.True(model.HasDifference);
        Assert.True(model.DifferenceReasonMissing);
        Assert.False(model.CanPost);
        Assert.Equal("برای ثبت نهایی آماده نیست", model.ReadinessText);
        Assert.Contains(model.PostBlockers, blocker => blocker.Contains("DifferenceReason"));

        await AssertNoFinancialRowsAsync(db);
    }

    [Fact]
    public async Task Index_PrefillsCustomerAndSupplier_FromQueryString()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        var controller = new ThreeWaySettlementController(db);

        var result = await controller.Index(customerId: 1, supplierId: 2, amount: 123.45m, currency: "afn");
        var model = AssertViewModel(result);

        Assert.Equal(1, model.CustomerId);
        Assert.Equal(2, model.SupplierId);
        Assert.Equal(ThreeWayPayeeType.Supplier, model.PayeeType);
        Assert.Equal(123.45m, model.CustomerPaidAmount);
        Assert.Equal(123.45m, model.PayeeAcceptedAmount);
        Assert.Equal("AFN", model.Currency);
        Assert.Equal("Customer A", model.CustomerName);
        Assert.Equal("Supplier A", model.PayeeName);
    }

    [Fact]
    public async Task Index_PrefillsFromPaymentTransaction_WhenSuspenseMoneySourceIsClear()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 44,
            PaymentDate = DateTime.UtcNow.Date,
            Direction = PaymentDirection.In,
            PaymentKind = PaymentKind.CustomerReceipt,
            CashAccountId = 99,
            CustomerId = 1,
            SupplierId = 2,
            Amount = 2500m,
            Currency = "AFN",
            AppliedFxRateToUsd = 0.014m,
            AmountUsd = 35m
        });
        await db.SaveChangesAsync();

        var controller = new ThreeWaySettlementController(db);

        var result = await controller.Index(paymentTransactionId: 44);
        var model = AssertViewModel(result);

        Assert.Equal(44, model.SourcePaymentTransactionId);
        Assert.Equal(1, model.CustomerId);
        Assert.Equal(2, model.SupplierId);
        Assert.Equal(ThreeWayPayeeType.Supplier, model.PayeeType);
        Assert.Equal(2500m, model.CustomerPaidAmount);
        Assert.Equal(2500m, model.PayeeAcceptedAmount);
        Assert.Equal("AFN", model.Currency);
        Assert.Equal(0.014m, model.FxRateToUsd);
        Assert.Equal("Customer A", model.CustomerName);
        Assert.Equal("Supplier A", model.PayeeName);

        await AssertNoFinancialRowsAsync(db, expectedPayments: 1);
    }

    [Fact]
    public async Task Confirm_ForSupplier_CreatesSettlementAndExactlyTwoLedgerEntries()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        await SeedContractsAsync(db);
        db.CashAccounts.Add(new CashAccount { Id = 20, Code = "BANK", Name = "Main bank", Currency = "USD" });
        await db.SaveChangesAsync();

        var controller = new ThreeWaySettlementController(db);

        var result = await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            SettlementDate = new DateTime(2026, 6, 6),
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Supplier,
            SupplierId = 2,
            CustomerPaidAmount = 1000m,
            PayeeAcceptedAmount = 950m,
            Currency = "USD",
            FxRateToUsd = 1m,
            CustomerSaleContractId = 10,
            SupplierPurchaseContractId = 11,
            DifferenceReason = DifferenceReason.Commission,
            ReferenceNumber = "HW-100",
            Description = "D1 test"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ThreeWaySettlementController.Details), redirect.ActionName);

        var settlement = await db.ThreeWaySettlements.SingleAsync();
        Assert.Equal(ThreeWaySettlementStatus.Posted, settlement.Status);
        Assert.Equal(ThreeWayPayeeType.Supplier, settlement.PayeeType);
        Assert.Equal(1, settlement.CustomerId);
        Assert.Equal(2, settlement.SupplierId);
        Assert.Equal(10, settlement.CustomerSaleContractId);
        Assert.Equal(11, settlement.SupplierPurchaseContractId);
        Assert.Equal(1000m, settlement.CustomerPaidUsd);
        Assert.Equal(950m, settlement.SupplierAcceptedUsd);
        Assert.Equal(50m, settlement.DifferenceUsd);
        Assert.Equal(DifferenceReason.Commission, settlement.DifferenceReason);
        Assert.Equal("HW-100", settlement.HawalaReference);
        Assert.Equal(DateTimeKind.Utc, settlement.SettlementDate.Kind);

        var ledgers = await db.LedgerEntries.OrderBy(l => l.Id).ToListAsync();
        Assert.Equal(2, ledgers.Count);

        var customerLedger = ledgers.Single(l => l.CustomerId == 1);
        Assert.Equal(LedgerSide.Debit, customerLedger.Side);
        Assert.Equal(DateTimeKind.Utc, customerLedger.EntryDate.Kind);
        Assert.Equal(DateTimeKind.Utc, customerLedger.AppliedFxRateDate?.Kind);
        Assert.Equal(1000m, customerLedger.AmountUsd);
        Assert.Equal(1000m, customerLedger.SourceAmount);
        Assert.Equal("USD", customerLedger.SourceCurrencyCode);
        Assert.Equal(1m, customerLedger.AppliedFxRateToUsd);
        Assert.Equal(ThreeWaySettlementController.LedgerSourceType, customerLedger.SourceType);
        Assert.Equal(settlement.Id, customerLedger.SourceId);
        Assert.Equal(10, customerLedger.ContractId);
        Assert.Equal("تسویه سه‌طرفه: پرداخت مشتری به تأمین‌کننده", customerLedger.Description);

        var supplierLedger = ledgers.Single(l => l.SupplierId == 2);
        Assert.Equal(LedgerSide.Debit, supplierLedger.Side);
        Assert.Equal(DateTimeKind.Utc, supplierLedger.EntryDate.Kind);
        Assert.Equal(DateTimeKind.Utc, supplierLedger.AppliedFxRateDate?.Kind);
        Assert.Equal(950m, supplierLedger.AmountUsd);
        Assert.Equal(950m, supplierLedger.SourceAmount);
        Assert.Equal("USD", supplierLedger.SourceCurrencyCode);
        Assert.Equal(ThreeWaySettlementController.LedgerSourceType, supplierLedger.SourceType);
        Assert.Equal(settlement.Id, supplierLedger.SourceId);
        Assert.Equal(11, supplierLedger.ContractId);
        Assert.Equal("تسویه سه‌طرفه: کاهش بدهی تأمین‌کننده", supplierLedger.Description);

        Assert.Equal(customerLedger.Id, settlement.CustomerLedgerEntryId);
        Assert.Equal(supplierLedger.Id, settlement.SupplierLedgerEntryId);
        Assert.Equal(0, await db.PaymentTransactions.CountAsync());
        Assert.Equal(0, await db.SarrafSettlements.CountAsync());

        var cashAccount = await db.CashAccounts.SingleAsync();
        Assert.Equal("BANK", cashAccount.Code);
        Assert.Equal("Main bank", cashAccount.Name);

        var customerBalance = ledgers
            .Where(l => l.CustomerId == 1)
            .Sum(l => l.Side == LedgerSide.Credit ? l.AmountUsd : -l.AmountUsd);
        var supplierBalance = ledgers
            .Where(l => l.SupplierId == 2)
            .Sum(l => l.Side == LedgerSide.Credit ? l.AmountUsd : -l.AmountUsd);

        Assert.Equal(-1000m, customerBalance);
        Assert.Equal(-950m, supplierBalance);
    }

    [Fact]
    public async Task Confirm_DifferenceWithoutReason_IsRejectedAndCreatesNoRows()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        var controller = new ThreeWaySettlementController(db);

        var result = await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Supplier,
            SupplierId = 2,
            CustomerPaidAmount = 1000m,
            PayeeAcceptedAmount = 900m,
            Currency = "USD",
            FxRateToUsd = 1m
        });

        var model = AssertViewModel(result);
        Assert.True(model.ShowPreview);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState.Values.SelectMany(v => v.Errors), e => e.ErrorMessage.Contains("DifferenceReason"));
        await AssertNoFinancialRowsAsync(db);
    }

    [Fact]
    public async Task Confirm_ForOther_IsRejectedAsPreviewOnly()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        var controller = new ThreeWaySettlementController(db);

        var result = await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.OtherAccount,
            OtherPayeeName = "Other account",
            CustomerPaidAmount = 500m,
            PayeeAcceptedAmount = 500m,
            Currency = "USD",
            FxRateToUsd = 1m
        });

        _ = AssertViewModel(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState.Values.SelectMany(v => v.Errors), e => e.ErrorMessage.Contains("فعلاً فقط پیش‌نمایش است"));
        await AssertNoFinancialRowsAsync(db);
    }

    [Fact]
    public async Task Confirm_ForSarraf_CreatesSettlementAndExactlyTwoLedgerEntries()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        await SeedContractsAsync(db);
        db.CashAccounts.Add(new CashAccount { Id = 20, Code = "BANK", Name = "Main bank", Currency = "USD" });
        await db.SaveChangesAsync();

        var controller = new ThreeWaySettlementController(db);

        var result = await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            SettlementDate = new DateTime(2026, 6, 6),
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Sarraf,
            SarrafId = 3,
            SupplierId = 2,
            CustomerPaidAmount = 800m,
            PayeeAcceptedAmount = 800m,
            Currency = "USD",
            FxRateToUsd = 1m,
            CustomerSaleContractId = 10,
            SupplierPurchaseContractId = 11,
            ReferenceNumber = "HW-SARRAF-1",
            Description = "Sarraf conduit test"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ThreeWaySettlementController.Details), redirect.ActionName);

        var settlement = await db.ThreeWaySettlements.SingleAsync();
        Assert.Equal(ThreeWaySettlementStatus.Posted, settlement.Status);
        // Test #5: SarrafId is stored on ThreeWaySettlement.
        Assert.Equal(ThreeWayPayeeType.Sarraf, settlement.PayeeType);
        Assert.Equal(3, settlement.SarrafId);
        Assert.Equal(2, settlement.SupplierId);
        Assert.Equal(1, settlement.CustomerId);
        Assert.Equal(DateTimeKind.Utc, settlement.SettlementDate.Kind);

        // Test #1: exactly two ledger entries (customer + supplier), none for the Sarraf.
        var ledgers = await db.LedgerEntries.OrderBy(l => l.Id).ToListAsync();
        Assert.Equal(2, ledgers.Count);

        var customerLedger = ledgers.Single(l => l.CustomerId == 1);
        Assert.Equal(LedgerSide.Debit, customerLedger.Side);
        Assert.Equal(DateTimeKind.Utc, customerLedger.EntryDate.Kind);
        Assert.Equal(800m, customerLedger.AmountUsd);
        Assert.Equal(ThreeWaySettlementController.LedgerSourceType, customerLedger.SourceType);
        Assert.Equal(settlement.Id, customerLedger.SourceId);
        Assert.Equal(10, customerLedger.ContractId);

        var supplierLedger = ledgers.Single(l => l.SupplierId == 2);
        Assert.Equal(LedgerSide.Debit, supplierLedger.Side);
        Assert.Equal(DateTimeKind.Utc, supplierLedger.EntryDate.Kind);
        Assert.Equal(800m, supplierLedger.AmountUsd);
        Assert.Equal(ThreeWaySettlementController.LedgerSourceType, supplierLedger.SourceType);
        Assert.Equal(settlement.Id, supplierLedger.SourceId);
        Assert.Equal(11, supplierLedger.ContractId);

        // No LedgerEntry carries a sarraf reference (LedgerEntry has no SarrafId column at all).
        Assert.Equal(customerLedger.Id, settlement.CustomerLedgerEntryId);
        Assert.Equal(supplierLedger.Id, settlement.SupplierLedgerEntryId);

        // Tests #2, #3, #4: no PaymentTransaction, no CashAccount movement, no SarrafSettlement.
        Assert.Equal(0, await db.PaymentTransactions.CountAsync());
        Assert.Equal(0, await db.SarrafSettlements.CountAsync());
        var cashAccount = await db.CashAccounts.SingleAsync();
        Assert.Equal("BANK", cashAccount.Code);
        Assert.Equal("Main bank", cashAccount.Name);
    }

    [Fact]
    public async Task Confirm_ForSarraf_WithoutSupplier_IsRejected()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        var controller = new ThreeWaySettlementController(db);

        // Test #6: SupplierId is required for the Sarraf confirm (final payee).
        var result = await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Sarraf,
            SarrafId = 3,
            SupplierId = null,
            CustomerPaidAmount = 500m,
            PayeeAcceptedAmount = 500m,
            Currency = "USD",
            FxRateToUsd = 1m
        });

        _ = AssertViewModel(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState.Values.SelectMany(v => v.Errors), e => e.ErrorMessage.Contains("تأمین‌کننده گیرنده نهایی"));
        await AssertNoFinancialRowsAsync(db);
    }

    [Fact]
    public async Task Confirm_ForSarraf_InactiveSarraf_IsRejected()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        db.Sarrafs.Add(new Sarraf { Id = 5, Name = "Inactive Sarraf", IsActive = false });
        await db.SaveChangesAsync();
        var controller = new ThreeWaySettlementController(db);

        // Test #7: inactive Sarraf is rejected (id 5 inactive; id 99 missing).
        var inactive = await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Sarraf,
            SarrafId = 5,
            SupplierId = 2,
            CustomerPaidAmount = 500m,
            PayeeAcceptedAmount = 500m,
            Currency = "USD",
            FxRateToUsd = 1m
        });

        _ = AssertViewModel(inactive);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState.Values.SelectMany(v => v.Errors), e => e.ErrorMessage.Contains("صراف انتخاب‌شده فعال نیست"));
        await AssertNoFinancialRowsAsync(db);
    }

    [Fact]
    public async Task Confirm_ForSarraf_MissingSarraf_IsRejected()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        var controller = new ThreeWaySettlementController(db);

        var missing = await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Sarraf,
            SarrafId = 99,
            SupplierId = 2,
            CustomerPaidAmount = 500m,
            PayeeAcceptedAmount = 500m,
            Currency = "USD",
            FxRateToUsd = 1m
        });

        _ = AssertViewModel(missing);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState.Values.SelectMany(v => v.Errors), e => e.ErrorMessage.Contains("صراف انتخاب‌شده فعال نیست"));
        await AssertNoFinancialRowsAsync(db);
    }

    [Fact]
    public async Task Confirm_ForSarraf_DifferenceWithoutReason_IsRejected()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        var controller = new ThreeWaySettlementController(db);

        // Test #8: a difference between customer paid and supplier accepted needs a DifferenceReason.
        var result = await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Sarraf,
            SarrafId = 3,
            SupplierId = 2,
            CustomerPaidAmount = 1000m,
            PayeeAcceptedAmount = 950m,
            Currency = "USD",
            FxRateToUsd = 1m,
            DifferenceReason = null
        });

        _ = AssertViewModel(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState.Values.SelectMany(v => v.Errors), e => e.ErrorMessage.Contains("DifferenceReason"));
        await AssertNoFinancialRowsAsync(db);
    }

    [Fact]
    public async Task Cancel_SarrafSettlement_CreatesExactlyTwoReversalRows_NoPaymentOrCashChange()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        var settlement = await CreatePostedSarrafSettlementAsync(db);

        var controller = new ThreeWaySettlementController(db);
        await controller.Cancel(settlement.Id, "Wrong sarraf hawala");

        var reloaded = await db.ThreeWaySettlements.SingleAsync(s => s.Id == settlement.Id);
        Assert.Equal(ThreeWaySettlementStatus.Cancelled, reloaded.Status);

        // Test #10: exactly two reversal ledger rows, no PaymentTransaction.
        var reversalLedgers = await db.LedgerEntries
            .Where(l => l.SourceType == ThreeWaySettlementController.CancellationLedgerSourceType)
            .ToListAsync();
        Assert.Equal(2, reversalLedgers.Count);
        Assert.All(reversalLedgers, l => Assert.Equal(LedgerSide.Credit, l.Side));
        Assert.Contains(reversalLedgers, l => l.CustomerId == 1);
        Assert.Contains(reversalLedgers, l => l.SupplierId == 2);
        Assert.Equal(0, await db.PaymentTransactions.CountAsync());
        Assert.Equal(0, await db.SarrafSettlements.CountAsync());
    }

    [Fact]
    public async Task Reconciliation_DoesNotFlagValidSarrafThreeWaySettlementAsSuspense()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        await SeedContractsAsync(db);
        var controller = new ThreeWaySettlementController(db);

        // Test #11: a valid posted Sarraf settlement must not be flagged as suspense.
        await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            SettlementDate = new DateTime(2026, 6, 6),
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Sarraf,
            SarrafId = 3,
            SupplierId = 2,
            CustomerPaidAmount = 1000m,
            PayeeAcceptedAmount = 1000m,
            Currency = "USD",
            FxRateToUsd = 1m,
            CustomerSaleContractId = 10,
            SupplierPurchaseContractId = 11,
            ReferenceNumber = "HW-SARRAF-OK"
        });

        var reconciliation = new ReconciliationController(db);
        var suspenseResult = await reconciliation.SuspenseMoney();
        var suspense = Assert.IsType<SuspenseMoneyViewModel>(Assert.IsType<ViewResult>(suspenseResult).Model);
        Assert.Equal(0, suspense.TotalCount);
    }

    [Fact]
    public async Task Confirm_InvalidCustomerOrSupplierContractMismatch_IsRejected()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        db.Contracts.AddRange(
            new Contract
            {
                Id = 30,
                ContractNumber = "SALE-MISMATCH",
                ContractType = ContractType.Sale,
                CustomerId = 99,
                CompanyId = 1,
                ProductId = 1,
                ContractDate = new DateTime(2026, 1, 1),
                PricingMethod = PricingMethod.Fixed,
                QuantityMt = 1m
            },
            new Contract
            {
                Id = 31,
                ContractNumber = "PUR-MISMATCH",
                ContractType = ContractType.Purchase,
                SupplierId = 99,
                CompanyId = 1,
                ProductId = 1,
                ContractDate = new DateTime(2026, 1, 1),
                PricingMethod = PricingMethod.Fixed,
                QuantityMt = 1m
            });
        await db.SaveChangesAsync();

        var controller = new ThreeWaySettlementController(db);

        var result = await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Supplier,
            SupplierId = 2,
            CustomerPaidAmount = 1000m,
            PayeeAcceptedAmount = 1000m,
            Currency = "USD",
            FxRateToUsd = 1m,
            CustomerSaleContractId = 30,
            SupplierPurchaseContractId = 31
        });

        _ = AssertViewModel(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState.Values.SelectMany(v => v.Errors), e => e.ErrorMessage.Contains("مشتری انتخاب‌شده"));
        Assert.Contains(controller.ModelState.Values.SelectMany(v => v.Errors), e => e.ErrorMessage.Contains("تأمین‌کننده انتخاب‌شده"));
        await AssertNoFinancialRowsAsync(db);
    }

    [Fact]
    public async Task Reconciliation_DoesNotFlagValidPostedThreeWaySettlementAsSuspenseOrMissing()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        await SeedContractsAsync(db);
        var controller = new ThreeWaySettlementController(db);

        await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            SettlementDate = new DateTime(2026, 6, 6),
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Supplier,
            SupplierId = 2,
            CustomerPaidAmount = 1000m,
            PayeeAcceptedAmount = 1000m,
            Currency = "USD",
            FxRateToUsd = 1m,
            CustomerSaleContractId = 10,
            SupplierPurchaseContractId = 11,
            ReferenceNumber = "HW-OK"
        });

        var reconciliation = new ReconciliationController(db);

        var suspenseResult = await reconciliation.SuspenseMoney();
        var suspense = Assert.IsType<SuspenseMoneyViewModel>(Assert.IsType<ViewResult>(suspenseResult).Model);
        Assert.Equal(0, suspense.TotalCount);

        var missingResult = await reconciliation.MissingLedger();
        var missing = Assert.IsType<MissingLedgerViewModel>(Assert.IsType<ViewResult>(missingResult).Model);
        Assert.DoesNotContain(missing.SalesWithoutLedger, i => i.SourceType == ThreeWaySettlementController.LedgerSourceType);
        Assert.DoesNotContain(missing.ExpensesWithoutLedger, i => i.SourceType == ThreeWaySettlementController.LedgerSourceType);
        Assert.DoesNotContain(missing.PaymentsWithoutLedger, i => i.SourceType == ThreeWaySettlementController.LedgerSourceType);
        Assert.Equal(0, missing.SupplierPaymentIssueCount);
        Assert.Equal(0, missing.SarrafSettlementIssueCount);
    }

    [Fact]
    public async Task Cancel_PostedSettlement_CreatesReversalLedgerRows()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        var settlement = await CreatePostedSettlementAsync(db);
        var controller = new ThreeWaySettlementController(db);

        var result = await controller.Cancel(settlement.Id, "Wrong hawala");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ThreeWaySettlementController.Details), redirect.ActionName);

        var reloaded = await db.ThreeWaySettlements.SingleAsync(s => s.Id == settlement.Id);
        Assert.Equal(ThreeWaySettlementStatus.Cancelled, reloaded.Status);
        Assert.NotNull(reloaded.CancelledAtUtc);
        Assert.Equal("Wrong hawala", reloaded.CancellationReason);

        var reversalLedgers = await db.LedgerEntries
            .Where(l => l.SourceType == ThreeWaySettlementController.CancellationLedgerSourceType)
            .OrderBy(l => l.Id)
            .ToListAsync();

        Assert.Equal(2, reversalLedgers.Count);
        Assert.All(reversalLedgers, ledger =>
        {
            Assert.Equal(settlement.Id, ledger.SourceId);
            Assert.Equal(LedgerSide.Credit, ledger.Side);
            Assert.Contains("Wrong hawala", ledger.Description);
        });
        Assert.Contains(reversalLedgers, l => l.CustomerId == 1 && l.AmountUsd == 1000m && l.ContractId == 10);
        Assert.Contains(reversalLedgers, l => l.SupplierId == 2 && l.AmountUsd == 950m && l.ContractId == 11);
    }

    [Fact]
    public async Task Cancel_DoesNotEditOrDeleteOriginalLedgerRows()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        var settlement = await CreatePostedSettlementAsync(db);
        var originals = await db.LedgerEntries
            .AsNoTracking()
            .Where(l => l.SourceType == ThreeWaySettlementController.LedgerSourceType)
            .OrderBy(l => l.Id)
            .Select(l => new
            {
                l.Id,
                l.Side,
                l.AmountUsd,
                l.CustomerId,
                l.SupplierId,
                l.SourceType,
                l.SourceId,
                l.Description
            })
            .ToListAsync();

        var controller = new ThreeWaySettlementController(db);
        await controller.Cancel(settlement.Id, "Original was wrong");

        var afterCancelOriginals = await db.LedgerEntries
            .AsNoTracking()
            .Where(l => l.SourceType == ThreeWaySettlementController.LedgerSourceType)
            .OrderBy(l => l.Id)
            .Select(l => new
            {
                l.Id,
                l.Side,
                l.AmountUsd,
                l.CustomerId,
                l.SupplierId,
                l.SourceType,
                l.SourceId,
                l.Description
            })
            .ToListAsync();

        Assert.Equal(originals.Count, afterCancelOriginals.Count);
        Assert.Equal(originals, afterCancelOriginals);
        Assert.Equal(4, await db.LedgerEntries.CountAsync());
    }

    [Fact]
    public async Task Cancel_DoesNotCreatePaymentTransactionOrChangeCashAccount()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        var settlement = await CreatePostedSettlementAsync(db, includeCashAccount: true);
        var cashBefore = await db.CashAccounts.AsNoTracking().SingleAsync();

        var controller = new ThreeWaySettlementController(db);
        await controller.Cancel(settlement.Id, "Cancel test");

        Assert.Equal(0, await db.PaymentTransactions.CountAsync());
        Assert.Equal(1, await db.CashAccounts.CountAsync());
        var cashAfter = await db.CashAccounts.AsNoTracking().SingleAsync();
        Assert.Equal(cashBefore.Code, cashAfter.Code);
        Assert.Equal(cashBefore.Name, cashAfter.Name);
        Assert.Equal(cashBefore.Currency, cashAfter.Currency);
    }

    [Fact]
    public async Task Cancel_ReturnsCustomerAndSupplierBalancesToZero()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        var settlement = await CreatePostedSettlementAsync(db);

        var controller = new ThreeWaySettlementController(db);
        await controller.Cancel(settlement.Id, "Balance reversal");

        var ledgers = await db.LedgerEntries.AsNoTracking().ToListAsync();
        var customerBalance = ledgers
            .Where(l => l.CustomerId == 1)
            .Sum(l => l.Side == LedgerSide.Credit ? l.AmountUsd : -l.AmountUsd);
        var supplierBalance = ledgers
            .Where(l => l.SupplierId == 2)
            .Sum(l => l.Side == LedgerSide.Credit ? l.AmountUsd : -l.AmountUsd);

        Assert.Equal(0m, customerBalance);
        Assert.Equal(0m, supplierBalance);
    }

    [Fact]
    public async Task Cancel_DoubleCancel_IsRejected()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        var settlement = await CreatePostedSettlementAsync(db);

        var firstController = new ThreeWaySettlementController(db);
        await firstController.Cancel(settlement.Id, "First cancel");

        var secondController = new ThreeWaySettlementController(db);
        var result = await secondController.Cancel(settlement.Id, "Second cancel");

        Assert.IsType<ViewResult>(result);
        Assert.False(secondController.ModelState.IsValid);
        Assert.Equal(2, await db.LedgerEntries.CountAsync(l => l.SourceType == ThreeWaySettlementController.CancellationLedgerSourceType));
    }

    [Fact]
    public async Task Cancel_RequiresCancellationReason()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        var settlement = await CreatePostedSettlementAsync(db);

        var controller = new ThreeWaySettlementController(db);
        var result = await controller.Cancel(settlement.Id, " ");

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        var reloaded = await db.ThreeWaySettlements.SingleAsync(s => s.Id == settlement.Id);
        Assert.Equal(ThreeWaySettlementStatus.Posted, reloaded.Status);
        Assert.Null(reloaded.CancelledAtUtc);
        Assert.Equal(0, await db.LedgerEntries.CountAsync(l => l.SourceType == ThreeWaySettlementController.CancellationLedgerSourceType));
    }

    [Fact]
    public async Task Reconciliation_DoesNotFlagValidThreeWaySettlementCancellationAsSuspense()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        var settlement = await CreatePostedSettlementAsync(db);

        var controller = new ThreeWaySettlementController(db);
        await controller.Cancel(settlement.Id, "Valid reversal");

        var reconciliation = new ReconciliationController(db);
        var suspenseResult = await reconciliation.SuspenseMoney();
        var suspense = Assert.IsType<SuspenseMoneyViewModel>(Assert.IsType<ViewResult>(suspenseResult).Model);
        Assert.Equal(0, suspense.TotalCount);
    }

    [Fact]
    public async Task Confirm_StrongSarrafSettlementOverlap_IsBlocked_AndChangesNoFinancialState()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        await SeedContractsAsync(db);
        db.CashAccounts.Add(new CashAccount { Id = 20, Code = "BANK", Name = "Main bank", Currency = "USD" });
        // تسویه صراف company-funded که قبلاً بدهی همین تأمین‌کننده را با همین مرجع کم کرده است.
        db.SarrafSettlements.Add(new SarrafSettlement
        {
            Id = 70,
            SarrafId = 3,
            SupplierId = 2,
            SettlementDate = new DateTime(2026, 6, 6),
            ReferenceNumber = "HW-DUP",
            SupplierAcceptedAmountUsd = 1000m,
            RequestedCurrency = "USD",
            SarrafCurrency = "AFN",
            SupplierAcceptedCurrency = "USD",
            Status = SarrafSettlementStatus.Posted
        });
        await db.SaveChangesAsync();

        var controller = new ThreeWaySettlementController(db);
        var result = await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            SettlementDate = new DateTime(2026, 6, 6),
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Sarraf,
            SarrafId = 3,
            SupplierId = 2,
            CustomerPaidAmount = 1000m,
            PayeeAcceptedAmount = 1000m,
            Currency = "USD",
            FxRateToUsd = 1m,
            ReferenceNumber = "HW-DUP"
        });

        _ = AssertViewModel(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState.Values.SelectMany(v => v.Errors),
            e => e.ErrorMessage.Contains("دوباره‌کم‌شدن بدهی"));

        // block واقعی: هیچ سند/Ledger ساخته نشد و Payment/Cash/SarrafSettlement تغییر نکرد.
        Assert.Equal(0, await db.ThreeWaySettlements.CountAsync());
        Assert.Equal(0, await db.LedgerEntries.CountAsync());
        Assert.Equal(0, await db.PaymentTransactions.CountAsync());
        Assert.Equal(1, await db.SarrafSettlements.CountAsync());
        var cash = await db.CashAccounts.SingleAsync();
        Assert.Equal("BANK", cash.Code);
        Assert.Equal("Main bank", cash.Name);
    }

    [Fact]
    public async Task Preview_WeakSarrafSettlementOverlap_ShowsWarning_AndConfirmStillPosts()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        // همان تأمین‌کننده/صراف تسویه صراف دارد، اما مرجع/تاریخ/مبلغ متفاوت است → فقط هشدار ضعیف.
        db.SarrafSettlements.Add(new SarrafSettlement
        {
            Id = 71,
            SarrafId = 3,
            SupplierId = 2,
            SettlementDate = new DateTime(2026, 5, 1),
            ReferenceNumber = "OTHER-REF",
            SupplierAcceptedAmountUsd = 250m,
            RequestedCurrency = "USD",
            SarrafCurrency = "AFN",
            SupplierAcceptedCurrency = "USD",
            Status = SarrafSettlementStatus.Posted
        });
        await db.SaveChangesAsync();

        var model = new ThreeWaySettlementPreviewViewModel
        {
            SettlementDate = new DateTime(2026, 6, 6),
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Sarraf,
            SarrafId = 3,
            SupplierId = 2,
            CustomerPaidAmount = 1000m,
            PayeeAcceptedAmount = 1000m,
            Currency = "USD",
            FxRateToUsd = 1m,
            ReferenceNumber = "HW-NEW"
        };

        var previewController = new ThreeWaySettlementController(db);
        _ = AssertViewModel(await previewController.Preview(model));
        Assert.Equal(ThreeWaySettlementController.WeakSarrafOverlapMessage,
            previewController.ViewData["SarrafOverlapWarning"] as string);

        // هشدار ضعیف Confirm را block نمی‌کند.
        var confirmController = new ThreeWaySettlementController(db);
        var confirm = await confirmController.Confirm(model);
        Assert.IsType<RedirectToActionResult>(confirm);
        Assert.True(confirmController.ModelState.IsValid);
        Assert.Equal(1, await db.ThreeWaySettlements.CountAsync());
        Assert.Equal(2, await db.LedgerEntries.CountAsync());
        Assert.Equal(0, await db.PaymentTransactions.CountAsync());
        Assert.Equal(1, await db.SarrafSettlements.CountAsync());
    }

    [Fact]
    public async Task Preview_NoSarrafSettlementOverlap_ShowsNoWarning()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        var controller = new ThreeWaySettlementController(db);

        _ = AssertViewModel(await controller.Preview(new ThreeWaySettlementPreviewViewModel
        {
            SettlementDate = new DateTime(2026, 6, 6),
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Sarraf,
            SarrafId = 3,
            SupplierId = 2,
            CustomerPaidAmount = 1000m,
            PayeeAcceptedAmount = 1000m,
            Currency = "USD",
            FxRateToUsd = 1m,
            ReferenceNumber = "HW-CLEAN"
        }));

        Assert.Null(controller.ViewData["SarrafOverlapWarning"] as string);
    }

    [Fact]
    public async Task Reconciliation_FlagsThreeWayAndSarrafDuplicateRiskAsWarning()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        await SeedContractsAsync(db);

        // یک تسویه سه‌طرفه سالم ثبت می‌شود (هنوز تسویه صراف موازی وجود ندارد).
        var controller = new ThreeWaySettlementController(db);
        await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            SettlementDate = new DateTime(2026, 6, 6),
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Sarraf,
            SarrafId = 3,
            SupplierId = 2,
            CustomerPaidAmount = 1000m,
            PayeeAcceptedAmount = 1000m,
            Currency = "USD",
            FxRateToUsd = 1m,
            ReferenceNumber = "HW-RISK"
        });

        // بعداً یک تسویه صراف موازی با همان مرجع ثبت می‌شود (همان چیزی که guard هنگام Confirm نمی‌تواند جلویش را بگیرد).
        db.SarrafSettlements.Add(new SarrafSettlement
        {
            Id = 80,
            SarrafId = 3,
            SupplierId = 2,
            SettlementDate = new DateTime(2026, 6, 6),
            ReferenceNumber = "HW-RISK",
            SupplierAcceptedAmountUsd = 1000m,
            RequestedCurrency = "USD",
            SarrafCurrency = "AFN",
            SupplierAcceptedCurrency = "USD",
            Status = SarrafSettlementStatus.Posted
        });
        await db.SaveChangesAsync();

        var reconciliation = new ReconciliationController(db);
        var suspenseResult = await reconciliation.SuspenseMoney();
        var suspense = Assert.IsType<SuspenseMoneyViewModel>(Assert.IsType<ViewResult>(suspenseResult).Model);

        Assert.Contains(suspense.Items, i => i.IssueSource == "احتمال ثبت تکراری حواله"
            && i.Severity == SuspenseSeverity.Warning
            && i.DetailsController == "ThreeWaySettlement");
    }

    [Fact]
    public async Task Confirm_MultiCurrency_ComputesEachLegUsdSeparately_AndCreatesTwoLedgers()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        await SeedContractsAsync(db);
        db.CashAccounts.Add(new CashAccount { Id = 20, Code = "BANK", Name = "Main bank", Currency = "USD" });
        await db.SaveChangesAsync();

        var controller = new ThreeWaySettlementController(db);

        // مشتری 70000 AFN با نرخ 0.014 = 980 USD ؛ تأمین‌کننده 980 USD با نرخ 1 = 980 USD.
        var result = await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            SettlementDate = new DateTime(2026, 6, 6),
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Sarraf,
            SarrafId = 3,
            SupplierId = 2,
            CustomerPaidAmount = 70000m,
            CustomerPaidCurrency = "AFN",
            CustomerPaidFxRateToUsd = 0.014m,
            PayeeAcceptedAmount = 980m,
            SupplierAcceptedCurrency = "USD",
            SupplierAcceptedFxRateToUsd = 1m,
            ReferenceNumber = "HW-MC-1"
        });

        Assert.IsType<RedirectToActionResult>(result);

        var settlement = await db.ThreeWaySettlements.SingleAsync();
        Assert.Equal(980m, settlement.CustomerPaidUsd);
        Assert.Equal(980m, settlement.SupplierAcceptedUsd);
        Assert.Equal(0m, settlement.DifferenceUsd);
        Assert.Equal("AFN", settlement.CustomerPaidCurrency);
        Assert.Equal(0.014m, settlement.CustomerPaidFxRateToUsd);
        Assert.Equal("USD", settlement.SupplierAcceptedCurrency);
        Assert.Equal(1m, settlement.SupplierAcceptedFxRateToUsd);

        var ledgers = await db.LedgerEntries.OrderBy(l => l.Id).ToListAsync();
        Assert.Equal(2, ledgers.Count);

        var customerLedger = ledgers.Single(l => l.CustomerId == 1);
        Assert.Equal(LedgerSide.Debit, customerLedger.Side);
        Assert.Equal(980m, customerLedger.AmountUsd);
        Assert.Equal(70000m, customerLedger.SourceAmount);
        Assert.Equal("AFN", customerLedger.SourceCurrencyCode);
        Assert.Equal(0.014m, customerLedger.AppliedFxRateToUsd);

        var supplierLedger = ledgers.Single(l => l.SupplierId == 2);
        Assert.Equal(LedgerSide.Debit, supplierLedger.Side);
        Assert.Equal(980m, supplierLedger.AmountUsd);
        Assert.Equal(980m, supplierLedger.SourceAmount);
        Assert.Equal("USD", supplierLedger.SourceCurrencyCode);
        Assert.Equal(1m, supplierLedger.AppliedFxRateToUsd);

        // بدون Payment/Cash/SarrafSettlement.
        Assert.Equal(0, await db.PaymentTransactions.CountAsync());
        Assert.Equal(0, await db.SarrafSettlements.CountAsync());
        var cash = await db.CashAccounts.SingleAsync();
        Assert.Equal("BANK", cash.Code);
    }

    [Fact]
    public async Task Confirm_SingleCurrencyLegacy_FallsBackToBaseCurrencyOnBothLegs()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        var controller = new ThreeWaySettlementController(db);

        // رکورد تک‌ارز قدیمی: per-leg خالی، فقط Currency/FxRateToUsd پایه.
        await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            SettlementDate = new DateTime(2026, 6, 6),
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Supplier,
            SupplierId = 2,
            CustomerPaidAmount = 500m,
            PayeeAcceptedAmount = 500m,
            Currency = "USD",
            FxRateToUsd = 1m
        });

        var settlement = await db.ThreeWaySettlements.SingleAsync();
        Assert.Equal(500m, settlement.CustomerPaidUsd);
        Assert.Equal(500m, settlement.SupplierAcceptedUsd);
        Assert.Equal("USD", settlement.EffectiveCustomerPaidCurrency);
        Assert.Equal("USD", settlement.EffectiveSupplierAcceptedCurrency);
        Assert.Equal(1m, settlement.EffectiveCustomerPaidFxRateToUsd);
        Assert.Equal(1m, settlement.EffectiveSupplierAcceptedFxRateToUsd);

        var ledgers = await db.LedgerEntries.ToListAsync();
        Assert.Equal(2, ledgers.Count);
        Assert.All(ledgers, l => Assert.Equal("USD", l.SourceCurrencyCode));
    }

    [Fact]
    public async Task Cancel_MultiCurrencySettlement_ReversalsCarryPerLegCurrency()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        await SeedContractsAsync(db);

        var controller = new ThreeWaySettlementController(db);
        await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            SettlementDate = new DateTime(2026, 6, 6),
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Sarraf,
            SarrafId = 3,
            SupplierId = 2,
            CustomerPaidAmount = 70000m,
            CustomerPaidCurrency = "AFN",
            CustomerPaidFxRateToUsd = 0.014m,
            PayeeAcceptedAmount = 980m,
            SupplierAcceptedCurrency = "USD",
            SupplierAcceptedFxRateToUsd = 1m,
            ReferenceNumber = "HW-MC-CANCEL"
        });
        var settlement = await db.ThreeWaySettlements.SingleAsync();

        var cancelController = new ThreeWaySettlementController(db);
        await cancelController.Cancel(settlement.Id, "Wrong multi-currency hawala");

        var reversals = await db.LedgerEntries
            .Where(l => l.SourceType == ThreeWaySettlementController.CancellationLedgerSourceType)
            .ToListAsync();
        Assert.Equal(2, reversals.Count);
        Assert.All(reversals, l => Assert.Equal(LedgerSide.Credit, l.Side));

        var customerReversal = reversals.Single(l => l.CustomerId == 1);
        Assert.Equal("AFN", customerReversal.SourceCurrencyCode);
        Assert.Equal(0.014m, customerReversal.AppliedFxRateToUsd);

        var supplierReversal = reversals.Single(l => l.SupplierId == 2);
        Assert.Equal("USD", supplierReversal.SourceCurrencyCode);
        Assert.Equal(1m, supplierReversal.AppliedFxRateToUsd);

        Assert.Equal(0, await db.PaymentTransactions.CountAsync());
    }

    [Theory]
    [InlineData(DifferenceReason.FxDifference, "تفاوت نرخ", DifferencePolicyKind.FxReview, false)]
    [InlineData(DifferenceReason.Commission, "کمیشن", DifferencePolicyKind.PotentialExpense, true)]
    [InlineData(DifferenceReason.TransferFee, "کرایه حواله", DifferencePolicyKind.PotentialExpense, true)]
    [InlineData(DifferenceReason.BrokerMargin, "مارجین دلال", DifferencePolicyKind.NeedsReview, false)]
    [InlineData(DifferenceReason.Discount, "تخفیف", DifferencePolicyKind.AdjustmentOnly, false)]
    [InlineData(DifferenceReason.Adjustment, "اصلاح حساب", DifferencePolicyKind.AdjustmentOnly, false)]
    [InlineData(DifferenceReason.Other, "سایر", DifferencePolicyKind.NeedsReview, false)]
    public void DifferenceReasonPolicy_Label_Kind_AndPotentialExpense_AreCorrect(
        DifferenceReason reason, string expectedLabel, DifferencePolicyKind expectedKind, bool expectedPotentialExpense)
    {
        Assert.Equal(expectedLabel, DifferenceReasonPolicy.Label(reason));
        Assert.Equal(expectedKind, DifferenceReasonPolicy.Kind(reason));
        Assert.Equal(expectedPotentialExpense, DifferenceReasonPolicy.IsPotentialExpense(reason));
        Assert.False(string.IsNullOrWhiteSpace(DifferenceReasonPolicy.PolicyText(reason)));
    }

    [Fact]
    public void DifferenceReasonPolicy_NullReason_HasSafeDefaults()
    {
        Assert.Equal("دلیل مشخص نشده", DifferenceReasonPolicy.Label(null));
        Assert.Equal(DifferencePolicyKind.None, DifferenceReasonPolicy.Kind(null));
        Assert.False(DifferenceReasonPolicy.IsPotentialExpense(null));
    }

    [Fact]
    public async Task Confirm_CommissionDifference_CreatesNoExtraPosting_AndReconciliationWarnsOnly()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        await SeedContractsAsync(db);
        db.CashAccounts.Add(new CashAccount { Id = 20, Code = "BANK", Name = "Main bank", Currency = "USD" });
        await db.SaveChangesAsync();

        var controller = new ThreeWaySettlementController(db);
        await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            SettlementDate = new DateTime(2026, 6, 6),
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Supplier,
            SupplierId = 2,
            CustomerPaidAmount = 1000m,
            PayeeAcceptedAmount = 950m,
            Currency = "USD",
            FxRateToUsd = 1m,
            DifferenceReason = DifferenceReason.Commission,
            ReferenceNumber = "HW-COMM"
        });

        // فاز C1 trace-only: فقط دو ledger معمول؛ هیچ posting اضافه برای کمیشن.
        Assert.Equal(2, await db.LedgerEntries.CountAsync());
        Assert.Equal(0, await db.PaymentTransactions.CountAsync());
        Assert.Equal(0, await db.SarrafSettlements.CountAsync());

        var reconciliation = new ReconciliationController(db);
        var suspenseResult = await reconciliation.SuspenseMoney();
        var suspense = Assert.IsType<SuspenseMoneyViewModel>(Assert.IsType<ViewResult>(suspenseResult).Model);

        // کمیشن فقط Warning read-only می‌دهد، نه Critical.
        var commissionItem = Assert.Single(suspense.Items, i => i.IssueSource.Contains("کمیشن"));
        Assert.Equal(SuspenseSeverity.Warning, commissionItem.Severity);
        Assert.DoesNotContain(suspense.Items, i => i.Severity == SuspenseSeverity.Critical);
    }

    [Fact]
    public async Task Reconciliation_DiscountDifference_IsTraceOnly_NotFlagged()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedPartiesAsync(db);
        await SeedContractsAsync(db);

        var controller = new ThreeWaySettlementController(db);
        await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            SettlementDate = new DateTime(2026, 6, 6),
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Supplier,
            SupplierId = 2,
            CustomerPaidAmount = 1000m,
            PayeeAcceptedAmount = 950m,
            Currency = "USD",
            FxRateToUsd = 1m,
            DifferenceReason = DifferenceReason.Discount,
            ReferenceNumber = "HW-DISC"
        });

        var reconciliation = new ReconciliationController(db);
        var suspenseResult = await reconciliation.SuspenseMoney();
        var suspense = Assert.IsType<SuspenseMoneyViewModel>(Assert.IsType<ViewResult>(suspenseResult).Model);

        // تخفیف فقط trace است؛ نه Warning نه Critical.
        Assert.Equal(0, suspense.TotalCount);
    }

    private static async Task SeedPartiesAsync(ApplicationDbContext db)
    {
        db.Customers.Add(new Customer { Id = 1, Name = "Customer A" });
        db.Suppliers.Add(new Supplier { Id = 2, Name = "Supplier A" });
        db.Sarrafs.Add(new Sarraf { Id = 3, Name = "Sarraf A" });
        db.Currencies.Add(new Currency { Id = 1, Code = "USD", Name = "US Dollar" });
        db.Currencies.Add(new Currency { Id = 2, Code = "AFN", Name = "Afghani" });
        await db.SaveChangesAsync();
    }

    private static async Task<ThreeWaySettlement> CreatePostedSettlementAsync(
        ApplicationDbContext db,
        bool includeCashAccount = false)
    {
        await SeedPartiesAsync(db);
        await SeedContractsAsync(db);

        if (includeCashAccount)
        {
            db.CashAccounts.Add(new CashAccount { Id = 20, Code = "BANK", Name = "Main bank", Currency = "USD" });
            await db.SaveChangesAsync();
        }

        var controller = new ThreeWaySettlementController(db);
        await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            SettlementDate = new DateTime(2026, 6, 6),
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Supplier,
            SupplierId = 2,
            CustomerPaidAmount = 1000m,
            PayeeAcceptedAmount = 950m,
            Currency = "USD",
            FxRateToUsd = 1m,
            CustomerSaleContractId = 10,
            SupplierPurchaseContractId = 11,
            DifferenceReason = DifferenceReason.Commission,
            ReferenceNumber = "HW-100",
            Description = "D1 test"
        });

        return await db.ThreeWaySettlements.SingleAsync();
    }

    private static async Task<ThreeWaySettlement> CreatePostedSarrafSettlementAsync(ApplicationDbContext db)
    {
        await SeedPartiesAsync(db);
        await SeedContractsAsync(db);

        var controller = new ThreeWaySettlementController(db);
        await controller.Confirm(new ThreeWaySettlementPreviewViewModel
        {
            SettlementDate = new DateTime(2026, 6, 6),
            CustomerId = 1,
            PayeeType = ThreeWayPayeeType.Sarraf,
            SarrafId = 3,
            SupplierId = 2,
            CustomerPaidAmount = 1000m,
            PayeeAcceptedAmount = 1000m,
            Currency = "USD",
            FxRateToUsd = 1m,
            CustomerSaleContractId = 10,
            SupplierPurchaseContractId = 11,
            ReferenceNumber = "HW-SARRAF-CANCEL"
        });

        return await db.ThreeWaySettlements.SingleAsync();
    }

    private static async Task SeedContractsAsync(ApplicationDbContext db)
    {
        db.Contracts.AddRange(
            new Contract
            {
                Id = 10,
                ContractNumber = "SALE-1",
                ContractType = ContractType.Sale,
                CustomerId = 1,
                CompanyId = 1,
                ProductId = 1,
                ContractDate = new DateTime(2026, 1, 1),
                PricingMethod = PricingMethod.Fixed,
                QuantityMt = 1m
            },
            new Contract
            {
                Id = 11,
                ContractNumber = "PUR-1",
                ContractType = ContractType.Purchase,
                SupplierId = 2,
                CompanyId = 1,
                ProductId = 1,
                ContractDate = new DateTime(2026, 1, 1),
                PricingMethod = PricingMethod.Fixed,
                QuantityMt = 1m
            });
        await db.SaveChangesAsync();
    }

    private static async Task<ThreeWaySettlementPreviewViewModel> PreviewAsync(
        ThreeWaySettlementController controller,
        ThreeWaySettlementPreviewViewModel request)
    {
        var result = await controller.Preview(request);
        return AssertViewModel(result);
    }

    private static ThreeWaySettlementPreviewViewModel AssertViewModel(IActionResult result)
    {
        var view = Assert.IsType<ViewResult>(result);
        return Assert.IsType<ThreeWaySettlementPreviewViewModel>(view.Model);
    }

    private static async Task AssertNoFinancialRowsAsync(ApplicationDbContext db, int expectedPayments = 0)
    {
        Assert.Equal(0, await db.ThreeWaySettlements.CountAsync());
        Assert.Equal(0, await db.LedgerEntries.CountAsync());
        Assert.Equal(expectedPayments, await db.PaymentTransactions.CountAsync());
        Assert.Equal(0, await db.SarrafSettlements.CountAsync());
    }
}
