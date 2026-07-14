using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class PricingServiceTests
{
    [Fact]
    public async Task PricingService_Fixed_ReturnsUnitPrice()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        db.Contracts.Add(BuildFixedPurchaseContract(1, 450m));
        await db.SaveChangesAsync();

        var service = new PricingService(db);

        var result = await service.CalculateContractPriceAsync(1);

        Assert.Equal(450m, result.FinalUnitPrice);
        Assert.False(result.NeedsReview);
        Assert.False(result.FallbackApplied);
        Assert.Contains("قیمت ثابت", result.FormulaText);
    }

    [Fact]
    public async Task PricingService_PlattsDaily_CalculatesCorrectly()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        var contract = BuildDailyPlattsContract(1, new DateTime(2026, 4, 20), "ULSD", 5m);
        contract.ManualFinalPriceUsd = 505m;
        db.Contracts.Add(contract);
        db.DailyPlattsPrices.Add(new DailyPlattsPrice
        {
            Id = 1,
            ProductId = 1,
            BenchmarkCode = "ULSD",
            PriceDate = new DateTime(2026, 4, 20),
            PriceUsdPerMt = 500m,
            Source = "Platts"
        });
        await db.SaveChangesAsync();

        var service = new PricingService(db);

        var result = await service.CalculateContractPriceAsync(1);

        Assert.Null(result.BasePlattsPrice);
        Assert.Equal(5m, result.PremiumDiscountUsd);
        Assert.Equal(505m, result.FinalUnitPrice);
        Assert.False(result.NeedsReview);
        Assert.False(result.FallbackApplied);
    }

    [Fact]
    public async Task PricingService_PlattsDaily_WithManualFinalPrice_UsesUserFinalPrice_AndIgnoresReferenceRate()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        var contract = BuildDailyPlattsContract(1, new DateTime(2026, 4, 20), "ULSD", 25m);
        contract.ManualFinalPriceUsd = 615m;
        contract.MinimumPriceUsd = 700m;
        db.Contracts.Add(contract);
        db.DailyPlattsPrices.Add(new DailyPlattsPrice
        {
            Id = 1,
            ProductId = 1,
            BenchmarkCode = "ULSD",
            PriceDate = new DateTime(2026, 4, 20),
            PriceUsdPerMt = 500m,
            Source = "Platts"
        });
        await db.SaveChangesAsync();

        var service = new PricingService(db);

        var result = await service.CalculateContractPriceAsync(1);

        Assert.Equal(615m, result.FinalUnitPrice);
        Assert.Null(result.BasePlattsPrice);
        Assert.False(result.NeedsReview);
        Assert.False(result.FallbackApplied);
    }

    [Fact]
    public async Task PricingService_PlattsDaily_WithoutManualFinalPrice_DoesNotProduceFinancialPriceFromReferenceRate()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        db.Contracts.Add(BuildDailyPlattsContract(1, new DateTime(2026, 4, 20), "ULSD", 25m));
        db.DailyPlattsPrices.Add(new DailyPlattsPrice
        {
            Id = 1,
            ProductId = 1,
            BenchmarkCode = "ULSD",
            PriceDate = new DateTime(2026, 4, 20),
            PriceUsdPerMt = 500m,
            Source = "Platts"
        });
        await db.SaveChangesAsync();

        var service = new PricingService(db);

        var result = await service.CalculateContractPriceAsync(1);

        Assert.Null(result.FinalUnitPrice);
        Assert.Null(result.BasePlattsPrice);
        Assert.True(result.NeedsReview);
    }

    [Fact]
    public async Task PricingService_PlattsDaily_FallbackWhenNoPriceOnDate()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        var contract = BuildDailyPlattsContract(1, new DateTime(2026, 4, 21), "ULSD", 5m);
        contract.ManualFinalPriceUsd = 505m;
        db.Contracts.Add(contract);
        db.DailyPlattsPrices.Add(new DailyPlattsPrice
        {
            Id = 1,
            ProductId = 1,
            BenchmarkCode = "ULSD",
            PriceDate = new DateTime(2026, 4, 20),
            PriceUsdPerMt = 500m,
            Source = "Platts"
        });
        await db.SaveChangesAsync();

        var service = new PricingService(db);

        var result = await service.CalculateContractPriceAsync(1);

        Assert.Equal(505m, result.FinalUnitPrice);
        Assert.False(result.FallbackApplied);
        Assert.Equal(string.Empty, result.Reason);
        Assert.False(result.NeedsReview);
    }

    [Fact]
    public async Task PricingService_PlattsDaily_NeedsReviewWhenNoBenchmark()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        db.Contracts.Add(BuildDailyPlattsContract(1, new DateTime(2026, 4, 20), benchmarkCode: null, premiumDiscountUsd: 5m));
        await db.SaveChangesAsync();

        var service = new PricingService(db);

        var result = await service.CalculateContractPriceAsync(1);

        Assert.True(result.NeedsReview);
        Assert.Null(result.FinalUnitPrice);
        Assert.Contains("قیمت نهایی", result.Reason);
    }

    [Fact]
    public async Task PricingService_PlattsMonthly_AveragesCorrectly()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        var contract = BuildMonthlyPlattsContract(1, new DateTime(2026, 4, 1), "GO", 2m);
        contract.ManualFinalPriceUsd = 302m;
        db.Contracts.Add(contract);
        SeedMonthlyDailyRates(db, "GO", 100m, 200m, 300m, 400m, 500m);
        await db.SaveChangesAsync();

        var service = new PricingService(db);

        var result = await service.CalculateContractPriceAsync(1);

        Assert.Null(result.BasePlattsPrice);
        Assert.Equal(302m, result.FinalUnitPrice);
        Assert.False(result.NeedsReview);
        Assert.False(result.FallbackApplied);
    }

    [Fact]
    public async Task PricingService_PlattsMonthly_NeedsReviewWhenInsufficientData()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        db.Contracts.Add(BuildMonthlyPlattsContract(1, new DateTime(2026, 4, 1), "GO", 2m));
        SeedMonthlyDailyRates(db, "GO", 100m, 200m, 300m);
        await db.SaveChangesAsync();

        var service = new PricingService(db);

        var result = await service.CalculateContractPriceAsync(1);

        Assert.Null(result.FinalUnitPrice);
        Assert.True(result.NeedsReview);
        Assert.False(result.FallbackApplied);
        Assert.Contains("قیمت نهایی", result.Reason);
    }

    [Fact]
    public async Task PricingService_Manual_ReturnsManuaPrice()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        db.Contracts.Add(new Contract
        {
            Id = 1,
            ContractNumber = "PUR-001",
            ContractType = ContractType.Purchase,
            Status = ContractStatus.Active,
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 1,
            ContractDate = new DateTime(2026, 4, 20),
            QuantityMt = 100m,
            Currency = "USD",
            PricingMethod = PricingMethod.FormulaPlatts,
            BenchmarkCode = "JET",
            PlattsPeriodType = PlattsPeriodType.Manual,
            PlattsManualPriceUsd = 600m,
            PremiumDiscountUsd = -10m,
            ManualFinalPriceUsd = 590m
        });
        await db.SaveChangesAsync();

        var service = new PricingService(db);

        var result = await service.CalculateContractPriceAsync(1);

        Assert.Null(result.BasePlattsPrice);
        Assert.Equal(590m, result.FinalUnitPrice);
        Assert.False(result.NeedsReview);
        Assert.False(result.FallbackApplied);
    }

    [Fact]
    public async Task SuggestedPrice_ReturnsOkFalse_WhenNoContract()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        await db.SaveChangesAsync();

        var controller = new SalesController(
            db,
            new StockService(db),
            new AuditService(db),
            NullLogger<SalesController>.Instance,
            new PricingService(db));

        var result = await controller.SuggestedPrice(null);

        var json = Assert.IsType<JsonResult>(result);
        var payload = json.Value;
        Assert.NotNull(payload);
        Assert.False(GetPayloadValue<bool>(payload, "ok"));
        Assert.Equal("قرارداد انتخاب نشده", GetPayloadValue<string>(payload, "reason"));
    }

    [Fact]
    public async Task SuggestedPrice_ReturnsPrice_WhenFixedContract()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        db.Contracts.Add(BuildFixedPurchaseContract(1, 450m));
        await db.SaveChangesAsync();

        var controller = new SalesController(
            db,
            new StockService(db),
            new AuditService(db),
            NullLogger<SalesController>.Instance,
            new PricingService(db));

        var result = await controller.SuggestedPrice(1);

        var json = Assert.IsType<JsonResult>(result);
        var payload = json.Value;
        Assert.NotNull(payload);
        Assert.True(GetPayloadValue<bool>(payload, "ok"));
        Assert.Equal(450m, GetPayloadValue<decimal?>(payload, "finalUnitPrice"));
        Assert.Contains("قیمت ثابت", GetPayloadValue<string>(payload, "formulaText"));
    }

    private static DbContextOptions<ApplicationDbContext> NewDbOptions()
        => new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static void SeedBaseData(ApplicationDbContext db)
    {
        db.Companies.Add(new Company { Id = 1, Code = "PTG", Name = "PTG" });
        db.Products.Add(new Product { Id = 1, Code = "GO", Name = "Gas Oil" });
        db.Suppliers.Add(new Supplier { Id = 1, Name = "Supplier A" });
    }

    private static Contract BuildFixedPurchaseContract(int id, decimal unitPriceUsd)
        => new()
        {
            Id = id,
            ContractNumber = $"PUR-{id:000}",
            ContractType = ContractType.Purchase,
            Status = ContractStatus.Active,
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 1,
            ContractDate = new DateTime(2026, 4, 20),
            QuantityMt = 100m,
            Currency = "USD",
            PricingMethod = PricingMethod.Fixed,
            UnitPriceUsd = unitPriceUsd
        };

    private static Contract BuildDailyPlattsContract(
        int id,
        DateTime basisDate,
        string? benchmarkCode,
        decimal premiumDiscountUsd)
        => new()
        {
            Id = id,
            ContractNumber = $"PUR-{id:000}",
            ContractType = ContractType.Purchase,
            Status = ContractStatus.Active,
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 1,
            ContractDate = new DateTime(2026, 4, 20),
            QuantityMt = 100m,
            Currency = "USD",
            PricingMethod = PricingMethod.FormulaPlatts,
            BenchmarkCode = benchmarkCode,
            PlattsPeriodType = PlattsPeriodType.Daily,
            PlattsBasisDate = basisDate,
            PremiumDiscountUsd = premiumDiscountUsd
        };

    private static Contract BuildMonthlyPlattsContract(
        int id,
        DateTime basisMonth,
        string benchmarkCode,
        decimal premiumDiscountUsd)
        => new()
        {
            Id = id,
            ContractNumber = $"PUR-{id:000}",
            ContractType = ContractType.Purchase,
            Status = ContractStatus.Active,
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 1,
            ContractDate = new DateTime(2026, 4, 20),
            QuantityMt = 100m,
            Currency = "USD",
            PricingMethod = PricingMethod.FormulaPlatts,
            BenchmarkCode = benchmarkCode,
            PlattsPeriodType = PlattsPeriodType.Monthly,
            PlattsBasisMonth = basisMonth,
            PremiumDiscountUsd = premiumDiscountUsd
        };

    [Fact]
    public async Task PricingService_ManualFinalPrice_ReturnsProvidedPrice()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        db.Contracts.Add(new Contract
        {
            Id = 1,
            ContractNumber = "PUR-001",
            ContractType = ContractType.Purchase,
            Status = ContractStatus.Active,
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 1,
            ContractDate = new DateTime(2026, 5, 1),
            QuantityMt = 100m,
            Currency = "USD",
            PricingMethod = PricingMethod.ManualFinalPrice,
            ManualFinalPriceUsd = 620m
        });
        await db.SaveChangesAsync();

        var service = new PricingService(db);
        var result = await service.CalculateContractPriceAsync(1);

        Assert.Equal(620m, result.FinalUnitPrice);
        Assert.False(result.NeedsReview);
        Assert.False(result.FallbackApplied);
        Assert.Contains("620", result.FormulaText);
    }

    [Fact]
    public async Task PricingService_ManualFinalPrice_NeedsReviewWhenPriceNotSet()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        db.Contracts.Add(new Contract
        {
            Id = 1,
            ContractNumber = "PUR-001",
            ContractType = ContractType.Purchase,
            Status = ContractStatus.Active,
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 1,
            ContractDate = new DateTime(2026, 5, 1),
            QuantityMt = 100m,
            Currency = "USD",
            PricingMethod = PricingMethod.ManualFinalPrice,
            ManualFinalPriceUsd = null
        });
        await db.SaveChangesAsync();

        var service = new PricingService(db);
        var result = await service.CalculateContractPriceAsync(1);

        Assert.True(result.NeedsReview);
        Assert.Null(result.FinalUnitPrice);
    }

    [Fact]
    public async Task PricingService_ManualFinalPrice_IncludesFormulaNote()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        db.Contracts.Add(new Contract
        {
            Id = 1,
            ContractNumber = "PUR-001",
            ContractType = ContractType.Purchase,
            Status = ContractStatus.Active,
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 1,
            ContractDate = new DateTime(2026, 5, 1),
            QuantityMt = 100m,
            Currency = "USD",
            PricingMethod = PricingMethod.ManualFinalPrice,
            ManualFinalPriceUsd = 630m,
            PricingFormulaNote = "توافق شخصی"
        });
        await db.SaveChangesAsync();

        var service = new PricingService(db);
        var result = await service.CalculateContractPriceAsync(1);

        Assert.Equal(630m, result.FinalUnitPrice);
        Assert.False(result.NeedsReview);
        Assert.Contains("توافق شخصی", result.FormulaText);
    }

    [Fact]
    public async Task PricingService_ManualPlatts_IncludesFormulaNote()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        db.Contracts.Add(new Contract
        {
            Id = 1,
            ContractNumber = "PUR-001",
            ContractType = ContractType.Purchase,
            Status = ContractStatus.Active,
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 1,
            ContractDate = new DateTime(2026, 5, 1),
            QuantityMt = 100m,
            Currency = "USD",
            PricingMethod = PricingMethod.FormulaPlatts,
            BenchmarkCode = "ULSD",
            PlattsPeriodType = PlattsPeriodType.Manual,
            PlattsManualPriceUsd = 500m,
            PremiumDiscountUsd = 10m,
            ManualFinalPriceUsd = 510m,
            PricingFormulaNote = "قیمت مرجع توافقی"
        });
        await db.SaveChangesAsync();

        var service = new PricingService(db);
        var result = await service.CalculateContractPriceAsync(1);

        Assert.Equal(510m, result.FinalUnitPrice);
        Assert.False(result.NeedsReview);
        Assert.Contains("قیمت مرجع توافقی", result.FormulaText);
    }

    [Fact]
    public async Task PricingService_ManualPlatts_CalculatesBasePlusPremium()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        db.Contracts.Add(new Contract
        {
            Id = 1,
            ContractNumber = "PUR-001",
            ContractType = ContractType.Purchase,
            Status = ContractStatus.Active,
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 1,
            ContractDate = new DateTime(2026, 5, 1),
            QuantityMt = 100m,
            Currency = "USD",
            PricingMethod = PricingMethod.FormulaPlatts,
            BenchmarkCode = "ULSD",
            PlattsPeriodType = PlattsPeriodType.Manual,
            PlattsManualPriceUsd = 570m,
            PremiumDiscountUsd = 10m,
            ManualFinalPriceUsd = 580m
        });
        await db.SaveChangesAsync();

        var service = new PricingService(db);
        var result = await service.CalculateContractPriceAsync(1);

        Assert.Null(result.BasePlattsPrice);
        Assert.Equal(10m, result.PremiumDiscountUsd);
        Assert.Equal(580m, result.FinalUnitPrice);
        Assert.False(result.NeedsReview);
        Assert.False(result.FallbackApplied);
    }

    [Fact]
    public async Task SuggestedPrice_ReturnsPrice_WhenManualFinalPriceContract()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        db.Contracts.Add(new Contract
        {
            Id = 1,
            ContractNumber = "PUR-001",
            ContractType = ContractType.Purchase,
            Status = ContractStatus.Active,
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 1,
            ContractDate = new DateTime(2026, 5, 1),
            QuantityMt = 100m,
            Currency = "USD",
            PricingMethod = PricingMethod.ManualFinalPrice,
            ManualFinalPriceUsd = 585m,
            PricingFormulaNote = "توافق شخصی"
        });
        await db.SaveChangesAsync();

        var controller = new SalesController(
            db,
            new StockService(db),
            new AuditService(db),
            NullLogger<SalesController>.Instance,
            new PricingService(db));

        var result = await controller.SuggestedPrice(1);

        var json = Assert.IsType<JsonResult>(result);
        var payload = json.Value;
        Assert.NotNull(payload);
        Assert.True(GetPayloadValue<bool>(payload, "ok"));
        Assert.Equal(585m, GetPayloadValue<decimal?>(payload, "finalUnitPrice"));
        Assert.False(GetPayloadValue<bool>(payload, "needsReview"));
        Assert.Contains("585", GetPayloadValue<string>(payload, "formulaText"));
    }

    [Fact]
    public async Task PricingService_ManualPlatts_NeedsReview_WhenNoPriceSet()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedBaseData(db);
        db.Contracts.Add(new Contract
        {
            Id = 1,
            ContractNumber = "PUR-001",
            ContractType = ContractType.Purchase,
            Status = ContractStatus.Active,
            CompanyId = 1,
            ProductId = 1,
            SupplierId = 1,
            ContractDate = new DateTime(2026, 5, 1),
            QuantityMt = 100m,
            Currency = "USD",
            PricingMethod = PricingMethod.FormulaPlatts,
            BenchmarkCode = "ULSD",
            PlattsPeriodType = PlattsPeriodType.Manual,
            PlattsManualPriceUsd = null
        });
        await db.SaveChangesAsync();

        var service = new PricingService(db);
        var result = await service.CalculateContractPriceAsync(1);

        Assert.True(result.NeedsReview);
        Assert.Null(result.FinalUnitPrice);
        Assert.Contains("قیمت نهایی", result.Reason);
    }

    private static void SeedMonthlyDailyRates(ApplicationDbContext db, string benchmarkCode, params decimal[] prices)
    {
        for (var i = 0; i < prices.Length; i++)
        {
            db.DailyPlattsPrices.Add(new DailyPlattsPrice
            {
                Id = i + 1,
                ProductId = 1,
                BenchmarkCode = benchmarkCode,
                PriceDate = new DateTime(2026, 4, i + 1),
                PriceUsdPerMt = prices[i],
                Source = "Platts"
            });
        }
    }

    private static T GetPayloadValue<T>(object payload, string propertyName)
        => (T)payload.GetType().GetProperty(propertyName)!.GetValue(payload)!;
}
