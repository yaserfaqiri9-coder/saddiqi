using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services.Accounting;
using Xunit;

namespace PTGOilSystem.Web.Tests;

/// <summary>
/// گاردِ مرکزیِ مالک در <see cref="AccountingPostingService"/>: هیچ سندی نباید به دفترِ شرکتی جز
/// شرکتِ مالک بنشیند. وقتی هنوز مالکی تعیین نشده، گارد ساکت می‌ماند تا رفتارِ قبلی حفظ شود.
/// </summary>
public class AccountingPostingOwnerGuardTests
{
    [Fact]
    public async Task Post_ToNonOwnerCompany_IsRejectedWithControlledError()
    {
        await using var db = NewDb();
        db.Companies.AddRange(
            Company(1, "OWNER", isOwner: true),
            Company(2, "OTHER", isOwner: false));
        await db.SaveChangesAsync();

        var service = NewService(db, new SystemCompanyProvider(db));

        var ex = await Assert.ThrowsAsync<AccountingValidationException>(
            () => service.PostAsync(RequestForCompany(2)));

        Assert.Equal("COMPANY_NOT_OWNER", ex.Code);
    }

    [Fact]
    public async Task Post_WhenNoOwnerConfigured_IsRejectedFailClosed()
    {
        await using var db = NewDb();
        db.Companies.Add(Company(2, "OTHER", isOwner: false));
        await db.SaveChangesAsync();

        var service = NewService(db, new SystemCompanyProvider(db));

        // Fail-Closed: بدون مالک هیچ عملیاتی نباید بگذرد؛ خطای پیکربندیِ واضح.
        var ex = await Assert.ThrowsAsync<SystemCompanyConfigurationException>(
            () => service.PostAsync(RequestForCompany(2)));

        Assert.Equal("NO_SYSTEM_OWNER", ex.Code);
    }

    [Fact]
    public async Task Post_WhenMultipleOwnersConfigured_IsRejectedFailClosed()
    {
        await using var db = NewDb();
        db.Companies.AddRange(
            Company(1, "OWNER-A", isOwner: true),
            Company(2, "OWNER-B", isOwner: true));
        await db.SaveChangesAsync();

        var service = NewService(db, new SystemCompanyProvider(db));

        var ex = await Assert.ThrowsAsync<SystemCompanyConfigurationException>(
            () => service.PostAsync(RequestForCompany(1)));

        Assert.Equal("MULTIPLE_SYSTEM_OWNERS", ex.Code);
    }

    private static AccountingPostingService NewService(ApplicationDbContext db, ISystemCompanyProvider provider)
        => new(
            db,
            new PeriodGuard(db, new FiscalCalendarService(db)),
            Options.Create(new AccountingOptions { Enabled = true }),
            provider);

    private static AccountingPostRequest RequestForCompany(int companyId)
        => new(
            companyId,
            "JV-1",
            new DateTime(2026, 1, 15),
            new DateTime(2026, 1, 15),
            new DateTime(2026, 1, 15),
            "GuardTest",
            new[]
            {
                new AccountingPostLine(1, Debit: 100m, Credit: 0m, "USD", 100m, 1m),
                new AccountingPostLine(2, Debit: 0m, Credit: 100m, "USD", 100m, 1m)
            });

    private static ApplicationDbContext NewDb()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Company Company(int id, string code, bool isOwner)
        => new() { Id = id, Code = code, Name = code, Country = "AF", IsActive = true, IsSystemOwner = isOwner };
}
