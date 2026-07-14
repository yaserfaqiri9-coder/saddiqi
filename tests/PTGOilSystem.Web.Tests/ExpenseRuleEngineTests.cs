using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class ExpenseRuleEngineTests
{
    [Fact]
    public void CalculateAmount_For_Percent_Rule_Uses_Base_Amount()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new ApplicationDbContext(options);
        var engine = new ExpenseRuleEngine(db, new AuditService(db));

        var amount = engine.CalculateAmount(
            new ExpenseRule
            {
                Name = "Commission",
                ExpenseTypeId = 1,
                CalculationKind = "Percent",
                Amount = 2.5m,
                Currency = "USD"
            },
            quantityMt: null,
            baseAmountUsd: 2000m);

        Assert.Equal(50m, amount);
    }
}
