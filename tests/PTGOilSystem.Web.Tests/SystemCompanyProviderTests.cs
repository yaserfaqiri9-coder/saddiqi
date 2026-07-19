using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services.Accounting;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class SystemCompanyProviderTests
{
    [Fact]
    public async Task GetOwnerCompanyId_ExactlyOneOwner_ReturnsThatCompany()
    {
        await using var db = NewDb();
        db.Companies.AddRange(
            Company(1, "A", isOwner: false),
            Company(2, "B", isOwner: true),
            Company(3, "C", isOwner: false));
        await db.SaveChangesAsync();

        var ownerId = await new SystemCompanyProvider(db).GetOwnerCompanyIdAsync();

        Assert.Equal(2, ownerId);
    }

    [Fact]
    public async Task GetOwnerCompanyId_NoOwner_ThrowsConfigurationError()
    {
        await using var db = NewDb();
        db.Companies.Add(Company(1, "A", isOwner: false));
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SystemCompanyConfigurationException>(
            () => new SystemCompanyProvider(db).GetOwnerCompanyIdAsync());

        Assert.Equal("NO_SYSTEM_OWNER", ex.Code);
    }

    [Fact]
    public async Task GetOwnerCompanyId_MultipleOwners_ThrowsConfigurationError()
    {
        await using var db = NewDb();
        db.Companies.AddRange(
            Company(1, "A", isOwner: true),
            Company(2, "B", isOwner: true));
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SystemCompanyConfigurationException>(
            () => new SystemCompanyProvider(db).GetOwnerCompanyIdAsync());

        Assert.Equal("MULTIPLE_SYSTEM_OWNERS", ex.Code);
    }

    [Fact]
    public async Task FindOwnerCompanyId_NoOwner_ReturnsNull()
    {
        await using var db = NewDb();
        db.Companies.Add(Company(1, "A", isOwner: false));
        await db.SaveChangesAsync();

        var ownerId = await new SystemCompanyProvider(db).FindOwnerCompanyIdAsync();

        Assert.Null(ownerId);
    }

    [Fact]
    public async Task FindOwnerCompanyId_MultipleOwners_StillThrows()
    {
        await using var db = NewDb();
        db.Companies.AddRange(
            Company(1, "A", isOwner: true),
            Company(2, "B", isOwner: true));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<SystemCompanyConfigurationException>(
            () => new SystemCompanyProvider(db).FindOwnerCompanyIdAsync());
    }

    [Fact]
    public async Task Parties_AreNeverConsideredOwnerCompanies()
    {
        await using var db = NewDb();
        db.Companies.Add(Company(5, "OWNER", isOwner: true));
        // طرف‌حساب‌ها جداول جدا هستند و هرگز شرکتِ مالک محسوب نمی‌شوند.
        db.Suppliers.Add(new Supplier { Id = 1, Name = "Supplier" });
        db.Customers.Add(new Customer { Id = 1, Name = "Customer" });
        db.Sarrafs.Add(new Sarraf { Id = 1, Name = "Sarraf" });
        await db.SaveChangesAsync();

        var owner = await new SystemCompanyProvider(db).GetOwnerCompanyAsync();

        Assert.Equal(5, owner.Id);
        Assert.Equal("OWNER", owner.Code);
    }

    private static ApplicationDbContext NewDb()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Company Company(int id, string code, bool isOwner)
        => new() { Id = id, Code = code, Name = code, Country = "AF", IsActive = true, IsSystemOwner = isOwner };
}
