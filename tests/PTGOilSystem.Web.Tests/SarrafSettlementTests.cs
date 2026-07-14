using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Models.Reconciliation;
using PTGOilSystem.Web.Models.Reports;
using PTGOilSystem.Web.Services;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class SarrafSettlementTests
{
    [Fact]
    public async Task CreatePosted_AcceptedAmountOnly_ReducesSupplierByAcceptedAmount_AndTracksShortfall()
    {
        var options = NewDbOptions();
        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        await db.SaveChangesAsync();

        var service = new SarrafSettlementService(db);

        var settlement = await service.CreatePostedAsync(BuildCommand(SarrafSettlementDifferenceTreatment.AcceptedAmountOnly));

        Assert.Equal(SarrafSettlementStatus.Posted, settlement.Status);
        Assert.Equal(10_000m, settlement.RequestedAmountUsd);
        Assert.Equal(9_929.08m, settlement.SupplierAcceptedAmountUsd);
        Assert.Equal(70.92m, settlement.DifferenceAmountUsd);
        Assert.Equal(SarrafSettlementDifferenceType.SupplierShortfall, settlement.DifferenceType);
        Assert.NotNull(settlement.LedgerEntryId);
        Assert.Null(settlement.ExchangeDifferenceLedgerEntryId);

        var ledger = Assert.Single(await db.LedgerEntries.ToListAsync());
        Assert.Equal(SarrafSettlementService.SupplierLedgerSourceType, ledger.SourceType);
        Assert.Equal(settlement.Id, ledger.SourceId);
        Assert.Equal(LedgerSide.Debit, ledger.Side);
        Assert.Equal(9_929.08m, ledger.AmountUsd);
        Assert.Equal(1, ledger.SupplierId);
        Assert.Equal(1, ledger.ContractId);
    }

    [Fact]
    public async Task CreatePosted_RecognizeExchangeGainLoss_ReducesSupplierByRequestedAmount_AndPostsLossLedger()
    {
        var options = NewDbOptions();
        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        await db.SaveChangesAsync();

        var service = new SarrafSettlementService(db);

        var settlement = await service.CreatePostedAsync(BuildCommand(SarrafSettlementDifferenceTreatment.RecognizeExchangeGainLoss));

        Assert.Equal(SarrafSettlementDifferenceType.Loss, settlement.DifferenceType);
        Assert.NotNull(settlement.LedgerEntryId);
        Assert.NotNull(settlement.ExchangeDifferenceLedgerEntryId);

        var supplierLedger = await db.LedgerEntries.SingleAsync(l => l.Id == settlement.LedgerEntryId);
        Assert.Equal(10_000m, supplierLedger.AmountUsd);
        Assert.Equal(LedgerSide.Debit, supplierLedger.Side);

        var differenceLedger = await db.LedgerEntries.SingleAsync(l => l.Id == settlement.ExchangeDifferenceLedgerEntryId);
        Assert.Equal(SarrafSettlementService.ExchangeDifferenceSourceType, differenceLedger.SourceType);
        Assert.Equal(70.92m, differenceLedger.AmountUsd);
        Assert.Equal(LedgerSide.Debit, differenceLedger.Side);
        Assert.Equal(1, differenceLedger.ContractId);
        Assert.Null(differenceLedger.SupplierId);
    }

    [Fact]
    public async Task CreatePosted_ForSupplierPaymentViaSarraf_DoesNotCreatePaymentTransactionOrThreeWaySettlement()
    {
        var options = NewDbOptions();
        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        db.CashAccounts.Add(new CashAccount
        {
            Id = 1,
            Code = "BNK",
            Name = "BNK USD",
            AccountType = CashAccountType.Bank,
            Currency = "USD",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = new SarrafSettlementService(db);

        var settlement = await service.CreatePostedAsync(BuildCommand(SarrafSettlementDifferenceTreatment.AcceptedAmountOnly));

        Assert.Equal(SarrafSettlementStatus.Posted, settlement.Status);
        Assert.Equal(0, await db.PaymentTransactions.CountAsync());
        Assert.Equal(0, await db.ThreeWaySettlements.CountAsync());
        var cashAccount = await db.CashAccounts.SingleAsync();
        Assert.Equal("BNK USD", cashAccount.Name);
        Assert.Equal("USD", cashAccount.Currency);
    }

    [Fact]
    public async Task Cancel_CreatesReversalLedgerEntries_ForSupplierAndDifferenceLedgers()
    {
        var options = NewDbOptions();
        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        await db.SaveChangesAsync();

        var service = new SarrafSettlementService(db);
        var settlement = await service.CreatePostedAsync(BuildCommand(SarrafSettlementDifferenceTreatment.RecognizeExchangeGainLoss));

        await service.CancelAsync(settlement.Id, "test cancel");

        var reloaded = await db.SarrafSettlements.SingleAsync(s => s.Id == settlement.Id);
        Assert.Equal(SarrafSettlementStatus.Cancelled, reloaded.Status);
        Assert.Equal("test cancel", reloaded.CancelReason);

        var reversals = await db.LedgerEntries
            .Where(l => l.SourceType == SarrafSettlementService.CancelSourceType && l.SourceId == settlement.Id)
            .OrderBy(l => l.AmountUsd)
            .ToListAsync();

        Assert.Equal(2, reversals.Count);
        Assert.All(reversals, l => Assert.Equal(LedgerSide.Credit, l.Side));
        Assert.Contains(reversals, l => l.AmountUsd == 10_000m);
        Assert.Contains(reversals, l => l.AmountUsd == 70.92m);
    }

    [Fact]
    public async Task ContractPnl_IncludesPostedSarrafShortfallAndExchangeDifference()
    {
        var options = NewDbOptions();
        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        db.SarrafSettlements.AddRange(
            BuildSettlement(10, SarrafSettlementDifferenceTreatment.AcceptedAmountOnly, SarrafSettlementDifferenceType.SupplierShortfall, 70m),
            BuildSettlement(11, SarrafSettlementDifferenceTreatment.RecognizeExchangeGainLoss, SarrafSettlementDifferenceType.Loss, 10m),
            BuildSettlement(12, SarrafSettlementDifferenceTreatment.RecognizeExchangeGainLoss, SarrafSettlementDifferenceType.Gain, -5m),
            BuildSettlement(13, SarrafSettlementDifferenceTreatment.AcceptedAmountOnly, SarrafSettlementDifferenceType.SupplierShortfall, 999m, SarrafSettlementStatus.Cancelled));
        await db.SaveChangesAsync();

        var controller = new ReportsController(db);

        var result = await controller.ContractPnl(new ManagementReportFilterViewModel { ContractId = 1 });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ContractPnlReportViewModel>(view.Model);
        var row = Assert.Single(model.PurchaseRows);
        Assert.Equal(70m, row.SarrafSupplierShortfallUsd);
        Assert.Equal(5m, row.ExchangeGainUsd);
        Assert.Equal(10m, row.ExchangeLossUsd);
        Assert.Equal(75m, row.TotalCostUsd);
        Assert.Equal(70m, model.TotalSarrafSupplierShortfallUsd);
        Assert.Equal(5m, model.TotalExchangeGainUsd);
        Assert.Equal(10m, model.TotalExchangeLossUsd);
    }

    [Fact]
    public async Task MissingLedger_FlagsSarrafSettlementLedgerGapsAndMismatches()
    {
        var options = NewDbOptions();
        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        db.SarrafSettlements.AddRange(
            BuildSettlement(20, SarrafSettlementDifferenceTreatment.AcceptedAmountOnly, SarrafSettlementDifferenceType.SupplierShortfall, 80m),
            BuildSettlement(
                21,
                SarrafSettlementDifferenceTreatment.RecognizeExchangeGainLoss,
                SarrafSettlementDifferenceType.Loss,
                50m,
                ledgerEntryId: 210,
                exchangeDifferenceLedgerEntryId: 211));
        db.LedgerEntries.AddRange(
            new LedgerEntry
            {
                Id = 210,
                EntryDate = new DateTime(2026, 5, 1),
                Side = LedgerSide.Debit,
                AmountUsd = 9_000m,
                Currency = "USD",
                SourceType = SarrafSettlementService.SupplierLedgerSourceType,
                SourceId = 21,
                SupplierId = 1,
                ContractId = 1,
                Description = "Wrong supplier amount"
            },
            new LedgerEntry
            {
                Id = 211,
                EntryDate = new DateTime(2026, 5, 1),
                Side = LedgerSide.Debit,
                AmountUsd = 40m,
                Currency = "USD",
                SourceType = SarrafSettlementService.ExchangeDifferenceSourceType,
                SourceId = 21,
                ContractId = 1,
                Description = "Wrong difference amount"
            });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(db);

        var result = await controller.MissingLedger();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MissingLedgerViewModel>(view.Model);
        Assert.Contains(model.SarrafSettlementsWithoutSupplierLedger, row => row.SettlementId == 20);
        Assert.Contains(model.SarrafSettlementSupplierLedgerMismatches, row => row.SettlementId == 21);
        Assert.Contains(model.SarrafSettlementDifferenceLedgerMismatches, row => row.SettlementId == 21);
        Assert.Equal(3, model.SarrafSettlementIssueCount);
    }

    [Fact]
    public async Task CreatePosted_ViaSarrafRubPayment_StoresExactRubSourceAmount_NotReconverted()
    {
        // سناریوی روزنامچه «از طریق صراف»: 100,000 روبل، نرخ تأمین‌کننده 77، نرخ صراف 80.
        // حساب تأمین‌کننده باید دقیقاً 100,000 روبل (SourceAmount) ثبت شود؛
        // USD فقط برای دفترِ دالریِ داخلی است و نباید مبنای روبلِ تأمین‌کننده باشد.
        var options = NewDbOptions();
        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        await db.SaveChangesAsync();

        var service = new SarrafSettlementService(db);

        var settlement = await service.CreatePostedAsync(BuildRubViaSarrafCommand());

        var ledger = await db.LedgerEntries.SingleAsync(l => l.Id == settlement.LedgerEntryId);
        Assert.Equal(SarrafSettlementService.SupplierLedgerSourceType, ledger.SourceType);
        Assert.Equal(LedgerSide.Debit, ledger.Side);
        Assert.Equal("RUB", ledger.SourceCurrencyCode);
        Assert.Equal(100_000m, ledger.SourceAmount);
        Assert.Equal(1, ledger.SupplierId);
        // USD داخلی = 100000 / 77 ≈ 1298.7013 (فقط برای حسابداری دالری)
        Assert.Equal(1_298.7013m, ledger.AmountUsd);
        Assert.Equal(1_298.7013m, settlement.SupplierAcceptedAmountUsd);
        // در حالت AcceptedAmountOnly ردیف تفاوت نرخی ساخته نمی‌شود.
        Assert.Null(settlement.ExchangeDifferenceLedgerEntryId);
    }

    [Fact]
    public async Task CreatePosted_CustomerReceiptViaSarraf_PostsCustomerDebitLedger_NoPaymentTransaction()
    {
        var options = NewDbOptions();
        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        await db.SaveChangesAsync();

        var service = new SarrafSettlementService(db);

        var settlement = await service.CreatePostedAsync(BuildCustomerInCommand());

        Assert.Equal(SarrafSettlementStatus.Posted, settlement.Status);
        Assert.Equal(SarrafSettlementDirection.In, settlement.Direction);
        Assert.Equal(SarrafSettlementCounterpartyType.Customer, settlement.CounterpartyType);

        var ledger = await db.LedgerEntries.SingleAsync(l => l.Id == settlement.LedgerEntryId);
        // در این کدبیس Debit طلب مشتری را کم می‌کند (مثل دریافت نقدی مشتری).
        Assert.Equal(LedgerSide.Debit, ledger.Side);
        Assert.Equal(1, ledger.CustomerId);
        Assert.Null(ledger.SupplierId);
        Assert.Null(ledger.ServiceProviderId);
        Assert.Null(ledger.ContractId);
        Assert.Equal("RUB", ledger.SourceCurrencyCode);
        Assert.Equal(50_000m, ledger.SourceAmount);

        Assert.Equal(0, await db.PaymentTransactions.CountAsync());
        Assert.Equal(0, await db.ThreeWaySettlements.CountAsync());
    }

    [Fact]
    public async Task CreatePosted_ServiceProviderPaymentViaSarraf_PostsServiceProviderDebitLedger_NoPaymentTransaction()
    {
        var options = NewDbOptions();
        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        await db.SaveChangesAsync();

        var service = new SarrafSettlementService(db);

        var settlement = await service.CreatePostedAsync(BuildServiceProviderOutCommand());

        Assert.Equal(SarrafSettlementDirection.Out, settlement.Direction);
        Assert.Equal(SarrafSettlementCounterpartyType.ServiceProvider, settlement.CounterpartyType);

        var ledger = await db.LedgerEntries.SingleAsync(l => l.Id == settlement.LedgerEntryId);
        Assert.Equal(LedgerSide.Debit, ledger.Side);
        Assert.Equal(1, ledger.ServiceProviderId);
        Assert.Null(ledger.SupplierId);
        Assert.Null(ledger.CustomerId);
        Assert.Null(ledger.ContractId);

        Assert.Equal(0, await db.PaymentTransactions.CountAsync());
    }

    [Fact]
    public async Task CreatePosted_CustomerType_WithoutCustomerId_Throws()
    {
        var options = NewDbOptions();
        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        await db.SaveChangesAsync();

        var service = new SarrafSettlementService(db);

        var command = BuildCustomerInCommand() with { CustomerId = null };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePostedAsync(command));
    }

    [Fact]
    public async Task CreatePosted_CustomerType_WithExtraSupplierId_Throws()
    {
        var options = NewDbOptions();
        await using var db = new ApplicationDbContext(options);
        SeedReferenceData(db);
        await db.SaveChangesAsync();

        var service = new SarrafSettlementService(db);

        // مشتری انتخاب شده اما SupplierId هم پر است → باید رد شود (فقط یک طرف‌حساب).
        var command = BuildCustomerInCommand() with { SupplierId = 1 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePostedAsync(command));
    }

    private static SarrafSettlementCommand BuildCustomerInCommand()
    {
        const decimal amount = 50_000m;
        const decimal customerRate = 77m;
        const decimal companyRate = 80m;
        var customerFx = 1m / customerRate;
        var companyFx = 1m / companyRate;

        return new SarrafSettlementCommand(
            new DateTime(2026, 5, 1),
            SarrafId: 1,
            SupplierId: null,
            ContractId: null,
            PaymentTransactionId: null,
            CashAccountId: null,
            ReferenceNumber: "SAR-CUST-1",
            Description: "Customer receipt via sarraf",
            RequestedAmount: amount,
            RequestedCurrency: "RUB",
            RequestedFxRateToUsd: companyFx,
            SarrafCurrency: "RUB",
            SarrafRate: companyRate,
            SarrafChargedAmount: amount,
            SarrafFxRateToUsd: companyFx,
            SupplierAcceptedAmount: amount,
            SupplierAcceptedCurrency: "RUB",
            SupplierAcceptedFxRateToUsd: customerFx,
            SupplierRate: customerRate,
            DifferenceTreatment: SarrafSettlementDifferenceTreatment.AcceptedAmountOnly,
            Direction: SarrafSettlementDirection.In,
            CounterpartyType: SarrafSettlementCounterpartyType.Customer,
            CustomerId: 1,
            ServiceProviderId: null);
    }

    private static SarrafSettlementCommand BuildServiceProviderOutCommand()
    {
        const decimal amount = 30_000m;
        const decimal spRate = 77m;
        const decimal companyRate = 80m;
        var spFx = 1m / spRate;
        var companyFx = 1m / companyRate;

        return new SarrafSettlementCommand(
            new DateTime(2026, 5, 1),
            SarrafId: 1,
            SupplierId: null,
            ContractId: null,
            PaymentTransactionId: null,
            CashAccountId: null,
            ReferenceNumber: "SAR-SP-1",
            Description: "Service provider payment via sarraf",
            RequestedAmount: amount,
            RequestedCurrency: "RUB",
            RequestedFxRateToUsd: companyFx,
            SarrafCurrency: "RUB",
            SarrafRate: companyRate,
            SarrafChargedAmount: amount,
            SarrafFxRateToUsd: companyFx,
            SupplierAcceptedAmount: amount,
            SupplierAcceptedCurrency: "RUB",
            SupplierAcceptedFxRateToUsd: spFx,
            SupplierRate: spRate,
            DifferenceTreatment: SarrafSettlementDifferenceTreatment.AcceptedAmountOnly,
            Direction: SarrafSettlementDirection.Out,
            CounterpartyType: SarrafSettlementCounterpartyType.ServiceProvider,
            CustomerId: null,
            ServiceProviderId: 1);
    }

    private static SarrafSettlementCommand BuildRubViaSarrafCommand()
    {
        const decimal amount = 100_000m;
        const decimal supplierRate = 77m;
        const decimal companyRate = 80m;
        var supplierFx = 1m / supplierRate;
        var companyFx = 1m / companyRate;

        return new SarrafSettlementCommand(
            new DateTime(2026, 5, 1),
            SarrafId: 1,
            SupplierId: 1,
            ContractId: 1,
            PaymentTransactionId: null,
            CashAccountId: null,
            ReferenceNumber: "SAR-RUB-1",
            Description: "Via-sarraf RUB payment",
            RequestedAmount: amount,
            RequestedCurrency: "RUB",
            RequestedFxRateToUsd: companyFx,
            SarrafCurrency: "RUB",
            SarrafRate: companyRate,
            SarrafChargedAmount: amount,
            SarrafFxRateToUsd: companyFx,
            SupplierAcceptedAmount: amount,
            SupplierAcceptedCurrency: "RUB",
            SupplierAcceptedFxRateToUsd: supplierFx,
            SupplierRate: supplierRate,
            DifferenceTreatment: SarrafSettlementDifferenceTreatment.AcceptedAmountOnly);
    }

    private static DbContextOptions<ApplicationDbContext> NewDbOptions()
        => new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static void SeedReferenceData(ApplicationDbContext db)
    {
        db.Companies.Add(new Company { Id = 1, Code = "PTG", Name = "PTG" });
        db.Products.Add(new Product { Id = 1, Code = "GO", Name = "Gas Oil" });
        db.Suppliers.Add(new Supplier { Id = 1, Name = "Supplier A", IsActive = true });
        db.Customers.Add(new Customer { Id = 1, Name = "Customer A", IsActive = true });
        db.ServiceProviders.Add(new ServiceProvider { Id = 1, Name = "Service Provider A", IsActive = true });
        db.Sarrafs.Add(new Sarraf { Id = 1, Name = "Sarraf A", IsActive = true });
        db.Contracts.Add(new Contract
        {
            Id = 1,
            ContractNumber = "PUR-SAR-1",
            ContractType = ContractType.Purchase,
            Status = ContractStatus.Active,
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 1,
            ContractDate = new DateTime(2026, 5, 1),
            QuantityMt = 100m,
            Currency = "USD",
            PricingMethod = PricingMethod.Fixed,
            UnitPriceUsd = null
        });
    }

    private static SarrafSettlementCommand BuildCommand(SarrafSettlementDifferenceTreatment treatment)
        => new(
            new DateTime(2026, 5, 1),
            SarrafId: 1,
            SupplierId: 1,
            ContractId: 1,
            PaymentTransactionId: null,
            CashAccountId: null,
            ReferenceNumber: "SAR-001",
            Description: "Test settlement",
            RequestedAmount: 10_000m,
            RequestedCurrency: "USD",
            RequestedFxRateToUsd: 1m,
            SarrafCurrency: "AFN",
            SarrafRate: 70m,
            SarrafChargedAmount: 700_000m,
            SarrafFxRateToUsd: 0.0142857m,
            SupplierAcceptedAmount: 9_929.08m,
            SupplierAcceptedCurrency: "USD",
            SupplierAcceptedFxRateToUsd: 1m,
            SupplierRate: null,
            DifferenceTreatment: treatment);

    private static SarrafSettlement BuildSettlement(
        int id,
        SarrafSettlementDifferenceTreatment treatment,
        SarrafSettlementDifferenceType differenceType,
        decimal differenceAmountUsd,
        SarrafSettlementStatus status = SarrafSettlementStatus.Posted,
        int? ledgerEntryId = null,
        int? exchangeDifferenceLedgerEntryId = null)
    {
        var requestedUsd = 10_000m;
        var acceptedUsd = differenceAmountUsd >= 0m
            ? requestedUsd - Math.Abs(differenceAmountUsd)
            : requestedUsd + Math.Abs(differenceAmountUsd);

        return new SarrafSettlement
        {
            Id = id,
            SettlementDate = new DateTime(2026, 5, 1),
            SarrafId = 1,
            SupplierId = 1,
            ContractId = 1,
            ReferenceNumber = "SAR-" + id,
            RequestedAmount = requestedUsd,
            RequestedCurrency = "USD",
            RequestedFxRateToUsd = 1m,
            RequestedAmountUsd = requestedUsd,
            SarrafCurrency = "AFN",
            SarrafRate = 70m,
            SarrafChargedAmount = 700_000m,
            SarrafFxRateToUsd = 0.0142857m,
            SarrafChargedAmountUsd = 9_999.99m,
            SupplierAcceptedAmount = acceptedUsd,
            SupplierAcceptedCurrency = "USD",
            SupplierAcceptedFxRateToUsd = 1m,
            SupplierAcceptedAmountUsd = acceptedUsd,
            DifferenceAmountUsd = differenceAmountUsd,
            DifferenceType = differenceType,
            DifferenceTreatment = treatment,
            Status = status,
            LedgerEntryId = ledgerEntryId,
            ExchangeDifferenceLedgerEntryId = exchangeDifferenceLedgerEntryId,
            PostedAtUtc = DateTime.UtcNow
        };
    }
}
