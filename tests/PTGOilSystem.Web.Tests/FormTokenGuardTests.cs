using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Contracts;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services;
using Xunit;

namespace PTGOilSystem.Web.Tests;

/// <summary>
/// Idempotency / double-submit guard. Uses SQLite (relational) rather than the
/// EF in-memory provider because the guarantee depends on the unique index being
/// enforced, which the in-memory provider ignores.
/// </summary>
public sealed class FormTokenGuardTests
{
    [Fact]
    public void Duplicate_Token_Violates_Unique_Index_And_IsDuplicate_Detects_It()
    {
        using var connection = OpenSqlite();
        using var db = NewContext(connection);
        var guard = new FormTokenGuard(db);

        guard.Stamp("TOKEN-ABC", "Test.Create");
        db.SaveChanges();

        // Second submit carries the same token.
        guard.Stamp("TOKEN-ABC", "Test.Create");
        var ex = Assert.Throws<DbUpdateException>(() => db.SaveChanges());

        Assert.True(guard.IsDuplicate(ex));

        // Only the first row survived.
        db.ChangeTracker.Clear();
        Assert.Equal(1, db.ProcessedFormTokens.Count(t => t.Token == "TOKEN-ABC"));
    }

    [Fact]
    public void Empty_Token_Is_Fail_Open_No_Row_And_No_Block()
    {
        using var connection = OpenSqlite();
        using var db = NewContext(connection);
        var guard = new FormTokenGuard(db);

        guard.Stamp(null, "Test.Create");
        guard.Stamp("   ", "Test.Create");
        db.SaveChanges(); // must not throw

        Assert.Equal(0, db.ProcessedFormTokens.Count());
    }

    [Fact]
    public void IsDuplicate_Returns_False_For_Unrelated_Exception()
    {
        using var connection = OpenSqlite();
        using var db = NewContext(connection);
        var guard = new FormTokenGuard(db);

        Assert.False(guard.IsDuplicate(new InvalidOperationException("something else")));
    }

    [Fact]
    public async Task Contract_Create_Twice_With_Same_Token_Creates_Only_One_Record()
    {
        using var connection = OpenSqlite();
        using (var seed = NewContext(connection))
        {
            SeedContractMasterData(seed);
        }

        const string token = "CONTRACT-TOKEN-1";

        RedirectToActionResult secondResult;
        using (var db = NewContext(connection))
        {
            var controller = BuildContractsController(db);

            var first = await controller.Create(NewContractModel(), token);
            Assert.IsType<RedirectToActionResult>(first);

            // Impatient second click: same rendered form => same token.
            var second = await controller.Create(NewContractModel(), token);
            secondResult = Assert.IsType<RedirectToActionResult>(second);
        }

        // Duplicate submit was redirected to the list, not the created record.
        Assert.Equal("Index", secondResult.ActionName);

        using var verify = NewContext(connection);
        Assert.Equal(1, await verify.Contracts.CountAsync(c => c.ContractType == ContractType.Purchase && c.SupplierId == 1));
        Assert.Equal(1, await verify.ProcessedFormTokens.CountAsync(t => t.Token == token));
    }

    private static SqliteConnection OpenSqlite()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        return connection;
    }

    private static ApplicationDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static ContractsController BuildContractsController(ApplicationDbContext db)
        => new(db, new AuditService(db))
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new NullTempDataProvider())
        };

    private static void SeedContractMasterData(ApplicationDbContext db)
    {
        db.Units.Add(new Unit { Id = 1, Code = "MT", Name = "Metric Ton", Symbol = "MT", IsActive = true });
        db.Companies.Add(new Company { Id = 1, Code = "PTG", Name = "PTG", IsActive = true });
        db.Products.Add(new Product { Id = 1, Code = "GO", Name = "Gas Oil", UnitId = 1, UnitOfMeasure = "MT", IsActive = true });
        db.Suppliers.Add(new Supplier { Id = 1, Name = "Supplier A", IsActive = true });
        db.Currencies.Add(new Currency { Id = 1, Code = "USD", Name = "US Dollar", IsActive = true });
        db.SaveChanges();
    }

    private static ContractFormViewModel NewContractModel() => new()
    {
        ContractType = ContractType.Purchase,
        Status = ContractStatus.Draft,
        CompanyId = 1,
        ProductId = 1,
        UnitId = 1,
        SupplierId = 1,
        OwnershipType = ContractOwnershipType.Personal,
        ContractDate = new DateTime(2026, 4, 23),
        PricingMethod = PricingMethod.Fixed,
        QuantityMt = 100m,
        Currency = "USD",
        UnitPriceInCurrency = 450m,
        RubRatePolicy = RubSettlementRatePolicy.NotApplicable
    };

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
