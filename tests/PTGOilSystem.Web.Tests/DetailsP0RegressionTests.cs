using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace PTGOilSystem.Web.Tests;

/// <summary>
/// View-structure regression guards for the four P0 fixes on Details pages:
/// 1) Loading lists must not be truncated with Take(5) (server pagers instead),
/// 2) ShipmentPnl expense categorisation must not live in Razor,
/// 3) profile pages must link contracts to ContractJourney (not the legacy
///    Contracts/Details redirect shim),
/// 4) StorageTanks must render the fill-percent KPI with the shared stat card.
/// </summary>
public class DetailsP0RegressionTests
{
    [Fact]
    public void Loading_Details_Does_Not_Truncate_Lists_With_Take5()
    {
        var view = ReadRepoFile("src/PTGOilSystem.Web/Views/Loading/Details.cshtml");

        Assert.DoesNotContain(".Take(5)", view);
        Assert.Contains("receiptsPage", view);
        Assert.Contains("customsPage", view);
        Assert.Contains("lossesPage", view);
        Assert.Contains("pagedReceiptItems", view);
        Assert.Contains("pagedCustomsItems", view);
        Assert.Contains("pagedLossItems", view);
    }

    [Fact]
    public void ShipmentPnl_Details_Uses_Single_Categorisation_Source()
    {
        var view = ReadRepoFile("src/PTGOilSystem.Web/Views/ShipmentPnl/Details.cshtml");

        // The old in-view keyword classifier (duplicated twice with divergent
        // term lists) must stay out of Razor.
        Assert.DoesNotContain("ExpenseMatches(", view);
        Assert.DoesNotContain("catFreight", view);
        Assert.Contains("Model.ExpenseCategoryGroups", view);
        Assert.Contains("ShipmentExpenseCategorizer.TotalFor", view);
    }

    [Theory]
    [InlineData("src/PTGOilSystem.Web/Views/Suppliers/Details.cshtml")]
    [InlineData("src/PTGOilSystem.Web/Views/Customers/Details.cshtml")]
    [InlineData("src/PTGOilSystem.Web/Views/Payments/Details.cshtml")]
    public void Profile_Pages_Link_Contracts_Directly_To_ContractJourney(string relativePath)
    {
        var view = ReadRepoFile(relativePath);

        Assert.DoesNotContain("asp-controller=\"Contracts\" asp-action=\"Details\"", view);
    }

    [Fact]
    public void StorageTanks_Details_Renders_Fill_Percent_Kpi_With_Shared_StatCard()
    {
        var view = ReadRepoFile("src/PTGOilSystem.Web/Views/StorageTanks/Details.cshtml");

        Assert.Contains("<vc:stat-card", view);
        Assert.Contains("fillPercentValue", view);
        // The computed value must actually be rendered, not just assigned.
        Assert.Contains("value=\"@FormatQuantity(fillPercentValue)\"", view);
    }

    private static string ReadRepoFile(string relativePath)
        => File.ReadAllText(GetRepoPath(relativePath));

    private static string GetRepoPath(string relativePath, [CallerFilePath] string sourceFilePath = "")
    {
        var normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        foreach (var start in new[]
                 {
                     Environment.CurrentDirectory,
                     AppContext.BaseDirectory,
                     Path.GetDirectoryName(sourceFilePath) ?? string.Empty
                 })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, normalizedPath);
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException($"Repo file not found: {relativePath}");
    }
}
