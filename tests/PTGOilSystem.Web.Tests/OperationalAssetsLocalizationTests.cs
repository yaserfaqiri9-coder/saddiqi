using System.IO;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class OperationalAssetsLocalizationTests
{
    [Fact]
    public void OperationalAsset_Views_Use_UiText_For_Visible_Copy_And_Avoid_Mojibake()
    {
        var viewPaths = new[]
        {
            "src/PTGOilSystem.Web/Views/OperationalAssets/Index.cshtml",
            "src/PTGOilSystem.Web/Views/OperationalAssets/_Form.cshtml",
            "src/PTGOilSystem.Web/Views/OperationalAssets/Create.cshtml",
            "src/PTGOilSystem.Web/Views/OperationalAssets/Edit.cshtml",
            "src/PTGOilSystem.Web/Views/OperationalAssets/CreateRent.cshtml",
            "src/PTGOilSystem.Web/Views/OperationalAssets/Details.cshtml",
            "src/PTGOilSystem.Web/Views/OperationalAssets/Profitability.cshtml"
        };

        foreach (var viewPath in viewPaths)
        {
            var view = ReadRepoFile(viewPath);

            Assert.Contains("UiText.T(Context", view);
            AssertNoCommonMojibake(view);
            Assert.DoesNotContain("<h1 class=\"page-title\">Operational", view);
            Assert.DoesNotContain(">Record Use/Rent<", view);
            Assert.DoesNotContain(">Save Asset<", view);
            Assert.DoesNotContain(">Apply Filter<", view);
        }
    }

    [Fact]
    public void OperationalAsset_Enum_Options_Are_Localized_From_Request_Language()
    {
        var controller = ReadRepoFile("src/PTGOilSystem.Web/Controllers/OperationalAssetsController.cs");
        var viewModel = ReadRepoFile("src/PTGOilSystem.Web/Models/OperationalAssets/OperationalAssetViewModels.cs");

        Assert.Contains("OperationalAssetLabels.AssetType(assetType, HttpContext)", controller);
        Assert.Contains("OperationalAssetLabels.OwnershipMode(ownershipMode, HttpContext)", controller);
        Assert.Contains("OperationalAssetLabels.UsageType(usageType, HttpContext)", controller);
        Assert.Contains("OperationalAssetLabels.ChargedToType(chargedToType, HttpContext)", controller);
        Assert.Contains("public static string AssetType(OperationalAssetType type, HttpContext? context)", viewModel);
        Assert.Contains("InternalCompanyUse => \"استفاده داخلی شرکت\"", viewModel);
        AssertNoCommonMojibake(viewModel);
    }

    private static string ReadRepoFile(string relativePath)
        => File.ReadAllText(GetRepoPath(relativePath));

    private static void AssertNoCommonMojibake(string content)
    {
        Assert.DoesNotContain("Ã˜", content);
        Assert.DoesNotContain("Ã™", content);
        Assert.DoesNotContain("Ãš", content);
        Assert.DoesNotContain("Ã›", content);
        Assert.DoesNotContain("Ãƒ", content);
        Assert.DoesNotContain("Ã¢", content);
        Assert.DoesNotContain("Ø¯", content);
    }

    private static string GetRepoPath(string relativePath)
        => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
