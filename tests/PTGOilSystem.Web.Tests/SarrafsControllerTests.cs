using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Models.Sarrafs;
using PTGOilSystem.Web.Models.ThreeWaySettlement;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class SarrafsControllerTests
{
    private static DbContextOptions<ApplicationDbContext> NewDbOptions()
        => new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public void Details_View_Uses_Two_Clear_Sarraf_Flow_Actions_And_Tabs()
    {
        var view = File.ReadAllText(GetProjectFilePath("src", "PTGOilSystem.Web", "Views", "Sarrafs", "Details.cshtml"));

        Assert.Contains("صراف برای تأمین‌کننده پرداخت کرد", view);
        Assert.Contains("ما با صراف حساب کردیم", view);
        Assert.Contains("asp-controller=\"SarrafSettlements\"", view);
        Assert.Contains("asp-controller=\"Payments\"", view);
        Assert.Contains("asp-route-sarrafId=\"@Model.Id\"", view);
        Assert.Contains("پرداخت‌های صراف برای تأمین‌کنندگان", view);
        Assert.Contains("پرداخت‌های ما به صراف", view);
        Assert.DoesNotContain("Ledger", view);
        Assert.DoesNotContain("Debit", view);
        Assert.DoesNotContain("Credit", view);
        Assert.DoesNotContain("SourceType", view);
        Assert.DoesNotContain("ThreeWaySettlement", view);
    }

    [Fact]
    public async Task Details_SarrafSettlement_And_SarrafPayment_Show_User_Friendly_Document_Rows()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedAsync(db);
        db.CashAccounts.Add(new CashAccount
        {
            Id = 4,
            Code = "BNK",
            Name = "BNK USD",
            AccountType = CashAccountType.Bank,
            Currency = "USD",
            IsActive = true
        });
        db.SarrafSettlements.Add(new SarrafSettlement
        {
            Id = 10,
            SettlementDate = new DateTime(2026, 6, 7),
            SarrafId = 3,
            SupplierId = 2,
            ReferenceNumber = "NOORZAD-BNK-1",
            RequestedAmount = 1000m,
            RequestedCurrency = "USD",
            RequestedFxRateToUsd = 1m,
            RequestedAmountUsd = 1000m,
            SarrafCurrency = "USD",
            SarrafRate = 1m,
            SarrafChargedAmount = 1010m,
            SarrafFxRateToUsd = 1m,
            SarrafChargedAmountUsd = 1010m,
            SupplierAcceptedAmount = 1000m,
            SupplierAcceptedCurrency = "USD",
            SupplierAcceptedFxRateToUsd = 1m,
            SupplierAcceptedAmountUsd = 1000m,
            DifferenceAmountUsd = 10m,
            DifferenceType = SarrafSettlementDifferenceType.SupplierShortfall,
            DifferenceTreatment = SarrafSettlementDifferenceTreatment.AcceptedAmountOnly,
            DifferenceReason = DifferenceReason.Commission,
            Status = SarrafSettlementStatus.Posted
        });
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 20,
            PaymentDate = new DateTime(2026, 6, 8),
            Direction = PaymentDirection.Out,
            PaymentKind = PaymentKind.SarrafSettlement,
            CashAccountId = 4,
            SarrafId = 3,
            Amount = 1010m,
            Currency = "USD",
            AppliedFxRateToUsd = 1m,
            AmountUsd = 1010m,
            Reference = "NOORZAD-BNK-1"
        });
        await db.SaveChangesAsync();

        var controller = new SarrafsController(db);

        var result = await controller.Details(3);

        var model = Assert.IsType<SarrafDetailsViewModel>(Assert.IsType<ViewResult>(result).Model);
        var settlement = Assert.Single(model.Settlements);
        Assert.Equal("Supplier A", settlement.SupplierName);
        Assert.Equal("NOORZAD-BNK-1", settlement.ReferenceNumber);
        Assert.Equal("دارای تفاوت", settlement.UserStatusName);
        Assert.Equal(1m, settlement.SupplierAcceptedFxRateToUsd);

        var payment = Assert.Single(model.Payments);
        Assert.Equal("BNK USD", payment.CashAccountName);
        Assert.Equal("NOORZAD-BNK-1", payment.Reference);
        Assert.Equal("تسویه‌شده", payment.UserStatusName);
        Assert.Equal(1m, payment.AppliedFxRateToUsd);
    }

    [Fact]
    public async Task Details_SarrafConduitHawala_ShowsTrace_WithoutAffectingSarrafBalance()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedAsync(db);

        // یک حواله سه‌طرفه که صراف #3 فقط واسطه آن است.
        var threeWay = new ThreeWaySettlementController(db);
        await threeWay.Confirm(new ThreeWaySettlementPreviewViewModel
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
            ReferenceNumber = "HW-TRACE-1"
        });

        var controller = new SarrafsController(db);
        var result = await controller.Details(3);
        var model = Assert.IsType<SarrafDetailsViewModel>(Assert.IsType<ViewResult>(result).Model);

        // Test #12: trace نمایش داده می‌شود ...
        var trace = Assert.Single(model.CustomerHawalas);
        Assert.Equal("Customer A", trace.CustomerName);
        Assert.Equal("Supplier A", trace.SupplierName);
        Assert.Equal(1000m, trace.CustomerPaidUsd);
        Assert.Equal(ThreeWaySettlementStatus.Posted, trace.Status);

        // ... اما هیچ اثری روی مانده صراف ندارد (هیچ SarrafSettlement یا پرداختی وجود ندارد).
        Assert.Equal(0m, model.ChargedUsd);
        Assert.Equal(0m, model.PaidUsd);
        Assert.Equal(0m, model.PayableUsd);
        Assert.Empty(model.Settlements);
        Assert.Empty(model.Payments);
    }

    [Fact]
    public async Task Details_Statement_Builds_Running_Balance_From_Posted_Settlements_And_Payments()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedAsync(db);
        db.CashAccounts.Add(new CashAccount
        {
            Id = 4,
            Code = "BNK",
            Name = "BNK USD",
            AccountType = CashAccountType.Bank,
            Currency = "USD",
            IsActive = true
        });

        // تسویه Posted: بدهی ما به صراف +1000 (با اختلاف نرخ 100 که نباید دوباره به مانده اضافه شود).
        db.SarrafSettlements.Add(new SarrafSettlement
        {
            Id = 10,
            SettlementDate = new DateTime(2026, 6, 1),
            SarrafId = 3,
            SupplierId = 2,
            ReferenceNumber = "POSTED-1",
            SarrafChargedAmount = 1000m,
            SarrafCurrency = "USD",
            SarrafRate = 1m,
            SarrafFxRateToUsd = 1m,
            SarrafChargedAmountUsd = 1000m,
            SupplierAcceptedAmountUsd = 900m,
            DifferenceAmountUsd = 100m,
            DifferenceType = SarrafSettlementDifferenceType.SupplierShortfall,
            Status = SarrafSettlementStatus.Posted
        });
        // تسویه Cancelled: نباید در مانده حساب شود.
        db.SarrafSettlements.Add(new SarrafSettlement
        {
            Id = 11,
            SettlementDate = new DateTime(2026, 6, 1),
            SarrafId = 3,
            SupplierId = 2,
            ReferenceNumber = "CANCELLED-1",
            SarrafChargedAmount = 500m,
            SarrafCurrency = "USD",
            SarrafFxRateToUsd = 1m,
            SarrafChargedAmountUsd = 500m,
            Status = SarrafSettlementStatus.Cancelled
        });
        // پرداخت In: بدهی ما به صراف +50.
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 20,
            PaymentDate = new DateTime(2026, 6, 2),
            Direction = PaymentDirection.In,
            PaymentKind = PaymentKind.SarrafSettlement,
            CashAccountId = 4,
            SarrafId = 3,
            Amount = 50m,
            Currency = "USD",
            AppliedFxRateToUsd = 1m,
            AmountUsd = 50m,
            Reference = "IN-1"
        });
        // پرداخت Out: بدهی ما به صراف -300.
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 21,
            PaymentDate = new DateTime(2026, 6, 3),
            Direction = PaymentDirection.Out,
            PaymentKind = PaymentKind.SarrafSettlement,
            CashAccountId = 4,
            SarrafId = 3,
            Amount = 300m,
            Currency = "USD",
            AppliedFxRateToUsd = 1m,
            AmountUsd = 300m,
            Reference = "OUT-1"
        });
        await db.SaveChangesAsync();

        var controller = new SarrafsController(db);
        var model = Assert.IsType<SarrafDetailsViewModel>(Assert.IsType<ViewResult>(await controller.Details(3)).Model);

        // مانده فقط از Posted (Charged) و Payments (Out-In) ساخته می‌شود؛ Cancelled و اختلاف نرخ بی‌اثرند.
        Assert.Equal(1000m, model.ChargedUsd);
        Assert.Equal(250m, model.PaidUsd);
        Assert.Equal(750m, model.PayableUsd);

        // قاعدهٔ یک‌دست «داده/گرفته»: پرداخت ما = داده‌شده (is-increase)، پرداخت صراف از طرف ما = گرفته‌شده (is-decrease).
        // مانده = Σ(داده − گرفته) ⇒ تسویه/برگشت مانده را منفی می‌کند، پرداخت ما آن را مثبت.
        // Cancelled نباید ردیف بسازد ⇒ ۳ ردیف.
        Assert.Equal(3, model.StatementRows.Count);
        Assert.Collection(
            model.StatementRows,
            row => { Assert.Equal(-1000m, row.RunningBalanceUsd); Assert.Equal("is-decrease", row.EffectClass); }, // تسویه Posted (گرفته)
            row => { Assert.Equal(-1050m, row.RunningBalanceUsd); Assert.Equal("is-decrease", row.EffectClass); }, // پرداخت In/برگشت (گرفته)
            row => { Assert.Equal(-750m, row.RunningBalanceUsd); Assert.Equal("is-increase", row.EffectClass); });  // پرداخت Out (داده)

        // مانده نهایی = Paid - Charged = -750 (قابل پرداخت به صراف؛ اختلاف نرخ 100 دوباره اضافه نشده است).
        Assert.Equal(-750m, model.StatementRows[^1].RunningBalanceUsd);
        Assert.DoesNotContain(model.StatementRows, r => r.RunningBalanceUsd == -850m);
    }

    [Fact]
    public async Task Details_Statement_Excludes_Cancelled_Only_Sarraf()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        await SeedAsync(db);
        db.SarrafSettlements.Add(new SarrafSettlement
        {
            Id = 30,
            SettlementDate = new DateTime(2026, 6, 1),
            SarrafId = 3,
            SupplierId = 2,
            ReferenceNumber = "CANCELLED-ONLY",
            SarrafChargedAmount = 700m,
            SarrafCurrency = "USD",
            SarrafFxRateToUsd = 1m,
            SarrafChargedAmountUsd = 700m,
            Status = SarrafSettlementStatus.Cancelled
        });
        await db.SaveChangesAsync();

        var controller = new SarrafsController(db);
        var model = Assert.IsType<SarrafDetailsViewModel>(Assert.IsType<ViewResult>(await controller.Details(3)).Model);

        Assert.Empty(model.StatementRows);
        Assert.Equal(0m, model.ChargedUsd);
        Assert.Equal(0m, model.PayableUsd);
    }

    private static async Task SeedAsync(ApplicationDbContext db)
    {
        db.Customers.Add(new Customer { Id = 1, Name = "Customer A" });
        db.Suppliers.Add(new Supplier { Id = 2, Name = "Supplier A" });
        db.Sarrafs.Add(new Sarraf { Id = 3, Name = "Sarraf A" });
        db.Currencies.Add(new Currency { Id = 1, Code = "USD", Name = "US Dollar" });
        await db.SaveChangesAsync();
    }

    private static string GetProjectFilePath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ptg-oil-system.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Could not locate repository root.");
        }

        return Path.Combine(new[] { dir.FullName }.Concat(segments).ToArray());
    }
}
