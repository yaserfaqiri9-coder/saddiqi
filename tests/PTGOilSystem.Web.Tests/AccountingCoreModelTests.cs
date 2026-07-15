using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public sealed class AccountingCoreModelTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=ptg_accounting_model;Username=ptg;Password=ptg")
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void Feature_Flag_Defaults_To_Off()
        => Assert.False(new AccountingOptions().Enabled);

    [Fact]
    public void Accounting_RowVersions_Use_PostgreSql_Xmin()
    {
        using var db = CreateDb();
        var rowVersionEntities = new[]
        {
            typeof(Account), typeof(AccountingSettings), typeof(JournalEntry),
            typeof(FiscalYear), typeof(FiscalPeriod), typeof(FiscalYearCloseRun)
        };

        foreach (var clrType in rowVersionEntities)
        {
            var entity = db.Model.FindEntityType(clrType)!;
            var property = entity.FindProperty("RowVersion")!;
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            Assert.True(property.IsConcurrencyToken);
            Assert.Equal("xmin", property.GetColumnName(table));
        }
    }

    [Fact]
    public void AccountingSettings_Foreign_Keys_Are_Restrict()
    {
        using var db = CreateDb();
        var entity = db.Model.FindEntityType(typeof(AccountingSettings))!;
        Assert.NotEmpty(entity.GetForeignKeys());
        Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }

    [Fact]
    public void Journal_Line_Uses_Debit_Credit_As_Functional_Amounts()
    {
        Assert.Null(typeof(JournalEntryLine).GetProperty("FunctionalAmount"));
        Assert.NotNull(typeof(JournalEntryLine).GetProperty(nameof(JournalEntryLine.Debit)));
        Assert.NotNull(typeof(JournalEntryLine).GetProperty(nameof(JournalEntryLine.Credit)));
    }
}
