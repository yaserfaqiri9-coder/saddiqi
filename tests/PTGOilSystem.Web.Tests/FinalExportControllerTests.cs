using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.AccountStatements;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Models.Ledger;
using PTGOilSystem.Web.Services;
using PTGOilSystem.Web.Services.Audit;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class FinalExportControllerTests
{
    [Fact]
    public async Task Ledger_Csv_Uses_Filter_And_Utf8_Bom()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.LedgerEntries.AddRange(
            new LedgerEntry
            {
                Id = 1,
                EntryDate = new DateTime(2026, 4, 1),
                Side = LedgerSide.Credit,
                AmountUsd = 100m,
                SourceType = "Sale",
                SourceId = 10,
                Reference = "INV-1",
                Description = "فروش"
            },
            new LedgerEntry
            {
                Id = 2,
                EntryDate = new DateTime(2026, 4, 2),
                Side = LedgerSide.Debit,
                AmountUsd = 50m,
                SourceType = "Expense",
                SourceId = 20,
                Reference = "EXP-1",
                Description = "هزینه"
            });
        await db.SaveChangesAsync();

        var controller = new LedgerController(db, NullLogger<LedgerController>.Instance);

        var result = await controller.Csv(new LedgerIndexFilterViewModel
        {
            SourceType = "Sale",
            Reference = "INV"
        });

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, file.FileContents.Take(3).ToArray());
        var csv = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("INV-1", csv);
        Assert.DoesNotContain("EXP-1", csv);
    }

    [Fact]
    public async Task AccountStatements_Csv_Uses_Filter_And_Utf8_Bom()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.LedgerEntries.AddRange(
            new LedgerEntry
            {
                Id = 1,
                EntryDate = new DateTime(2026, 4, 1),
                Side = LedgerSide.Credit,
                AmountUsd = 100m,
                SourceAmount = 100m,
                SourceCurrencyCode = "USD",
                AppliedFxRateToUsd = 1m,
                SourceType = "OpeningBalance",
                SourceId = 1,
                Reference = "OPEN-USD",
                Description = "Opening"
            },
            new LedgerEntry
            {
                Id = 2,
                EntryDate = new DateTime(2026, 4, 2),
                Side = LedgerSide.Credit,
                AmountUsd = 200m,
                SourceAmount = 180m,
                SourceCurrencyCode = "EUR",
                AppliedFxRateToUsd = 1.111111m,
                SourceType = "ManualAdjustment",
                SourceId = 2,
                Reference = "ADJ-EUR",
                Description = "Adjustment"
            });
        await db.SaveChangesAsync();

        var controller = new AccountStatementsController(db, new PricingService(db), new AuditService(db));

        var result = await controller.Csv(new AccountStatementFilterViewModel
        {
            SourceCurrencyCode = "USD",
            Reference = "OPEN"
        });

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, file.FileContents.Take(3).ToArray());
        var csv = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("OPEN-USD", csv);
        Assert.DoesNotContain("ADJ-EUR", csv);
    }
}
