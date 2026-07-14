using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services;
using PTGOilSystem.Web.Services.Exceptions;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class UnitConversionServiceTests
{
    [Fact]
    public async Task ConvertAsync_SameUnit_ReturnsSameValue()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        db.Units.Add(new Unit { Id = 1, Code = "MT", Name = "Metric Ton", UnitType = "Weight", BaseUnitCode = "MT", ConversionFactorToBase = 1m });
        await db.SaveChangesAsync();

        var service = new UnitConversionService(db);

        var result = await service.ConvertAsync(12.5m, 1, 1);

        Assert.Equal(12.5m, result);
    }

    [Fact]
    public async Task ConvertAsync_MtToKg_UsesConfiguredFactors()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedWeightUnits(db);
        await db.SaveChangesAsync();

        var service = new UnitConversionService(db);

        var result = await service.ConvertAsync(2m, 1, 2);

        Assert.Equal(2000m, result);
    }

    [Fact]
    public async Task ConvertAsync_KgToMt_UsesConfiguredFactors()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedWeightUnits(db);
        await db.SaveChangesAsync();

        var service = new UnitConversionService(db);

        var result = await service.ConvertAsync(1500m, 2, 1);

        Assert.Equal(1.5m, result);
    }

    [Fact]
    public async Task CanConvertAsync_ReturnsFalse_ForUnitTypeMismatch()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        SeedWeightUnits(db);
        db.Units.Add(new Unit { Id = 3, Code = "LTR", Name = "Liter", UnitType = "Volume", BaseUnitCode = "LTR", ConversionFactorToBase = 1m });
        await db.SaveChangesAsync();

        var service = new UnitConversionService(db);

        var canConvert = await service.CanConvertAsync(1, 3);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConvertAsync(1m, 1, 3));

        Assert.False(canConvert);
        Assert.Equal("UNIT_CONVERSION_TYPE_MISMATCH", ex.Code);
    }

    [Fact]
    public async Task ConvertAsync_Fails_WhenFactorMissingOrNonPositive()
    {
        await using var db = new ApplicationDbContext(NewDbOptions());
        db.Units.AddRange(
            new Unit { Id = 1, Code = "MT", Name = "Metric Ton", UnitType = "Weight", BaseUnitCode = "MT", ConversionFactorToBase = 1m },
            new Unit { Id = 2, Code = "KG", Name = "Kilogram", UnitType = "Weight", BaseUnitCode = "MT" },
            new Unit { Id = 3, Code = "GM", Name = "Gram", UnitType = "Weight", BaseUnitCode = "MT", ConversionFactorToBase = 0m });
        await db.SaveChangesAsync();

        var service = new UnitConversionService(db);

        Assert.False(await service.CanConvertAsync(1, 2));
        var missing = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConvertAsync(1m, 1, 2));
        var nonPositive = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConvertAsync(1m, 1, 3));

        Assert.Equal("UNIT_CONVERSION_FACTOR_MISSING", missing.Code);
        Assert.Equal("UNIT_CONVERSION_FACTOR_MISSING", nonPositive.Code);
    }

    private static void SeedWeightUnits(ApplicationDbContext db)
    {
        db.Units.AddRange(
            new Unit { Id = 1, Code = "MT", Name = "Metric Ton", UnitType = "Weight", BaseUnitCode = "MT", IsBaseUnit = true, ConversionFactorToBase = 1m },
            new Unit { Id = 2, Code = "KG", Name = "Kilogram", UnitType = "Weight", BaseUnitCode = "MT", ConversionFactorToBase = 0.001m });
    }

    private static DbContextOptions<ApplicationDbContext> NewDbOptions()
        => new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
}
