using System.Runtime.CompilerServices;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class ShellViewStructureTests
{
    private static readonly string[] CanonicalPtgStylesheets =
    [
        "~/css/ptg/01-tokens.css",
        "~/css/ptg/02-base.css",
        "~/css/ptg/03-layout.css",
        "~/css/ptg/04-sidebar.css",
        "~/css/ptg/05-components.css",
        "~/css/ptg/06-forms.css",
        "~/css/ptg/07-tables.css",
        "~/css/ptg/08-modals.css",
        "~/css/ptg/09-pages.css",
        "~/css/ptg/10-responsive.css",
        "~/css/ptg/11-details.css",
        "~/css/ptg/12-dashboard.css",
        "~/css/ptg/13-compat.css",
    ];

    [Theory]
    [InlineData("~/css/ptg/01-tokens.css")]
    [InlineData("~/css/ptg/02-base.css")]
    [InlineData("~/css/ptg/03-layout.css")]
    [InlineData("~/css/ptg/04-sidebar.css")]
    [InlineData("~/css/ptg/05-components.css")]
    [InlineData("~/css/ptg/06-forms.css")]
    [InlineData("~/css/ptg/07-tables.css")]
    [InlineData("~/css/ptg/08-modals.css")]
    [InlineData("~/css/ptg/09-pages.css")]
    [InlineData("~/css/ptg/10-responsive.css")]
    [InlineData("~/css/ptg/11-details.css")]
    [InlineData("~/css/ptg/12-dashboard.css")]
    [InlineData("~/css/ptg/13-compat.css")]
    public void Layout_Loads_Each_Canonical_Ptg_Stylesheet_In_Order(string expectedStylesheet)
    {
        var layout = ReadRepoFile("src/PTGOilSystem.Web/Views/Shared/_Layout.cshtml");
        var modalLayout = ReadRepoFile("src/PTGOilSystem.Web/Views/Shared/_ModalLayout.cshtml");

        Assert.Contains(expectedStylesheet, layout);
        Assert.Contains(expectedStylesheet, modalLayout);

        var previousIndex = -1;
        foreach (var stylesheet in CanonicalPtgStylesheets)
        {
            var currentIndex = layout.IndexOf(stylesheet, StringComparison.Ordinal);
            Assert.True(currentIndex > previousIndex, $"{stylesheet} must load once and after the preceding PTG layer.");
            previousIndex = currentIndex;
        }
    }

    [Fact]
    public void Canonical_Tabs_Skin_Loads_Last_And_Covers_Every_Tab_Family()
    {
        var layout = ReadRepoFile("src/PTGOilSystem.Web/Views/Shared/_Layout.cshtml");
        var modalLayout = ReadRepoFile("src/PTGOilSystem.Web/Views/Shared/_ModalLayout.cshtml");
        var tabsCss = ReadRepoFile("src/PTGOilSystem.Web/wwwroot/css/ptg/16-system-tabs.css");
        var listsCss = ReadRepoFile("src/PTGOilSystem.Web/wwwroot/css/ptg/15-system-lists.css");
        var tabsJs = ReadRepoFile("src/PTGOilSystem.Web/wwwroot/js/ptg-tabs.js");
        var sectionTabs = ReadRepoFile("src/PTGOilSystem.Web/Views/Shared/_SectionTabs.cshtml");
        var shipmentTabs = ReadRepoFile("src/PTGOilSystem.Web/Views/ShipmentPnl/Details.cshtml");
        var journeyTabs = ReadRepoFile("src/PTGOilSystem.Web/Views/ContractJourney/_ContractJourneyDetailTabsRail.cshtml");

        Assert.True(
            layout.LastIndexOf("~/css/ptg/16-system-tabs.css", StringComparison.Ordinal)
            > layout.IndexOf("RenderSectionAsync(\"Styles\"", StringComparison.Ordinal),
            "The canonical tabs skin must load after page-specific styles.");
        Assert.True(
            modalLayout.LastIndexOf("~/css/ptg/16-system-tabs.css", StringComparison.Ordinal)
            > modalLayout.IndexOf("RenderSectionAsync(\"Styles\"", StringComparison.Ordinal),
            "Modal tabs must use the same final skin.");

        // Akaunting tab tokens. These are the contract every rail renders against.
        Assert.Contains("--ptg-tabs-font-size: 14px", tabsCss);
        Assert.Contains("--ptg-tabs-text-color: #424242", tabsCss);
        Assert.Contains("--ptg-tabs-active-color: #55588B", tabsCss);
        Assert.Contains("--ptg-tabs-border-color: #E5E7EB", tabsCss);
        Assert.Contains("--ptg-tabs-horizontal-padding: 16px", tabsCss);
        Assert.Contains("--ptg-tabs-bottom-padding: 8px", tabsCss);
        Assert.Contains("--ptg-tabs-indicator-height: 2px", tabsCss);
        Assert.Contains("--ptg-tabs-transition-duration: 180ms", tabsCss);
        Assert.Contains("--ptg-tabs-focus-color: rgba(85, 88, 139, .25)", tabsCss);
        Assert.Contains("border-bottom: 1px solid var(--ptg-tabs-border-color)", tabsCss);

        // Flat rail: spacing comes from tab padding, never from a gap, and the
        // rail must not wrap to a second line on narrow screens.
        Assert.Contains("gap: 0;", tabsCss);
        Assert.Contains("flex-wrap: nowrap", tabsCss);
        Assert.Contains("scrollbar-width: thin", tabsCss);
        Assert.DoesNotContain("font-weight: 750 !important", listsCss);
        Assert.Contains(".ptg-tabs-rail", tabsCss);
        Assert.Contains(".ptg-tab-item", tabsCss);
        Assert.DoesNotContain(".section-tabs", tabsCss);
        Assert.DoesNotContain(".oa-reference-tabs", tabsCss);
        Assert.DoesNotContain(".transport-profile-tabs", tabsCss);

        // 45-akaunting.css loads after the canonical skin, so it must not restyle
        // tabs — its old block beat every rail with !important. It may only refer
        // to tabs to exclude them from the global link colour, which otherwise
        // outweighs .ptg-tab-item and paints idle link-tabs in the active purple.
        var akauntingCss = ReadRepoFile("src/PTGOilSystem.Web/wwwroot/css/ptg/45-akaunting.css");
        Assert.Contains(":not(.ptg-tab-item)", akauntingCss);
        Assert.DoesNotContain("font-size: 14px !important", akauntingCss);
        Assert.DoesNotContain("font-weight: 500 !important", akauntingCss);
        Assert.DoesNotContain(".ptg-tab-item {", akauntingCss);

        Assert.Contains("class=\"ptg-tabs-rail\"", sectionTabs);
        Assert.Contains("class=\"ptg-tab-item ", sectionTabs);
        Assert.Contains("data-section-tabs-group", sectionTabs);
        Assert.Contains("class=\"ptg-tabs-rail ak-detail-tabs no-print\" data-shipment-file-tabs", shipmentTabs);
        Assert.Contains("class=\"ptg-tab-item active\"", shipmentTabs);
        Assert.Contains("class=\"ptg-tabs-rail\"", journeyTabs);
        Assert.DoesNotContain("cj-tabs-card", journeyTabs);
        Assert.DoesNotContain("cj-tab-pill", journeyTabs);
        Assert.DoesNotContain("cj-step-sep", journeyTabs);

        // Tabs are text-only, like the Akaunting reference: no icon markup inside
        // a rail, and no Bootstrap tab toggle anywhere in the system. (The page
        // action button next to the rail keeps its own icon — that is not a tab.)
        Assert.DoesNotContain("<i class=\"bi @tab.Icon\"", sectionTabs);
        Assert.DoesNotContain("<use href=\"#@tab.Icon\"", sectionTabs);
        Assert.DoesNotContain("<i class=\"bi @tab.Icon\"", journeyTabs);
        Assert.DoesNotContain("data-bs-toggle=\"tab\"", shipmentTabs);
        Assert.DoesNotContain("data-bs-toggle=\"tab\"", ReadRepoFile("src/PTGOilSystem.Web/Views/InventoryTransportLegs/Journey.cshtml"));
        Assert.DoesNotContain("data-bs-toggle=\"tab\"", ReadRepoFile("src/PTGOilSystem.Web/Views/OperationalAssets/Details.cshtml"));

        // One engine drives every rail: keyboard, ARIA and overflow scrolling.
        Assert.Contains("wireHorizontalScroll", tabsJs);
        Assert.Contains("event.preventDefault();", tabsJs);
        Assert.Contains("ArrowLeft", tabsJs);
        Assert.Contains("ArrowRight", tabsJs);
        Assert.Contains("role\", \"tablist\"", tabsJs);
        Assert.Contains("role\", \"tabpanel\"", tabsJs);
        Assert.Contains("aria-controls", tabsJs);
    }

    [Theory]
    [InlineData(".ptg-app")]
    [InlineData(".ptg-sidebar")]
    [InlineData(".ak-page-header")]
    [InlineData(".ak-form-page")]
    [InlineData(".ak-form-section")]
    [InlineData(".ak-detail-toolbar")]
    [InlineData(".ptg-tabs-rail")]
    [InlineData(".ak-status")]
    [InlineData(".ak-row-menu")]
    [InlineData(".ak-form-grid")]
    [InlineData(".ak-table-wrap")]
    [InlineData(".ak-table")]
    [InlineData(".ak-list")]
    [InlineData(".ak-empty")]
    [InlineData(".ak-pager")]
    [InlineData(".ak-modal-content")]
    public void Ptg_Css_Defines_Each_Canonical_Selector_Or_Intentional_Bridge(string selector)
    {
        var css = ReadPtgCss();

        Assert.Contains(selector, css);
    }

    [Theory]
    [InlineData("src/PTGOilSystem.Web/Views/Home/Index.cshtml", "ak-form-page")]
    [InlineData("src/PTGOilSystem.Web/Views/Products/Index.cshtml", "ak-list-page")]
    [InlineData("src/PTGOilSystem.Web/Views/StorageTanks/Index.cshtml", "ak-list-page")]
    [InlineData("src/PTGOilSystem.Web/Views/Customers/Index.cshtml", "ak-list-page")]
    [InlineData("src/PTGOilSystem.Web/Views/Suppliers/Index.cshtml", "ak-list-page")]
    [InlineData("src/PTGOilSystem.Web/Views/Employees/Index.cshtml", "ak-list-page")]
    [InlineData("src/PTGOilSystem.Web/Views/Contracts/Index.cshtml", "ak-table")]
    [InlineData("src/PTGOilSystem.Web/Views/Contracts/Create.cshtml", "ak-form")]
    [InlineData("src/PTGOilSystem.Web/Views/Loading/Details.cshtml", "ak-form-page")]
    [InlineData("src/PTGOilSystem.Web/Views/LoadingReceipts/_ReceiptCreateForm.cshtml", "ak-form")]
    [InlineData("src/PTGOilSystem.Web/Views/Dispatch/Create.cshtml", "_AkPageHeader")]
    [InlineData("src/PTGOilSystem.Web/Views/Sales/Create.cshtml", "ak-form")]
    [InlineData("src/PTGOilSystem.Web/Views/Expenses/Create.cshtml", "ak-form")]
    [InlineData("src/PTGOilSystem.Web/Views/Payments/Create.cshtml", "ak-form")]
    [InlineData("src/PTGOilSystem.Web/Views/CustomsDeclarations/Create.cshtml", "ak-form")]
    [InlineData("src/PTGOilSystem.Web/Views/CustomsPermitTurnover/Index.cshtml", "ak-list-page")]
    [InlineData("src/PTGOilSystem.Web/Views/AccountStatements/Details.cshtml", "ak-form-page")]
    [InlineData("src/PTGOilSystem.Web/Views/ShipmentPnl/Details.cshtml", "ak-form-page")]
    [InlineData("src/PTGOilSystem.Web/Views/Shared/_CreateModalShell.cshtml", "ak-modal")]
    [InlineData("src/PTGOilSystem.Web/Views/Shared/_EmptyState.cshtml", "ak-empty")]
    [InlineData("src/PTGOilSystem.Web/Views/Shared/_Pagination.cshtml", "ak-pager")]
    public void Razor_View_Uses_The_Current_Ptg_Marker_Or_Intentional_Compat_Bridge(
        string relativePath,
        string expectedMarker)
    {
        var view = ReadRepoFile(relativePath);

        Assert.Contains(expectedMarker, view);
    }

    [Theory]
    [InlineData("~/css/ptg/37-masterdata-cards.css", "src/PTGOilSystem.Web/wwwroot/css/ptg/37-masterdata-cards.css")]
    [InlineData("~/css/design-system/core.css", "src/PTGOilSystem.Web/wwwroot/css/design-system/core.css")]
    [InlineData("~/css/design-system/layout.css", "src/PTGOilSystem.Web/wwwroot/css/design-system/layout.css")]
    [InlineData("~/css/design-system/components.css", "src/PTGOilSystem.Web/wwwroot/css/design-system/components.css")]
    [InlineData("~/css/design-system/pages.css", "src/PTGOilSystem.Web/wwwroot/css/design-system/pages.css")]
    [InlineData("~/css/view-structure.css", "src/PTGOilSystem.Web/wwwroot/css/view-structure.css")]
    public void Legacy_Stylesheet_Is_Not_An_Active_Layout_Dependency(string layoutReference, string relativePath)
    {
        var layout = ReadRepoFile("src/PTGOilSystem.Web/Views/Shared/_Layout.cshtml");
        var modalLayout = ReadRepoFile("src/PTGOilSystem.Web/Views/Shared/_ModalLayout.cshtml");

        Assert.DoesNotContain(layoutReference, layout);
        Assert.DoesNotContain(layoutReference, modalLayout);
        Assert.False(File.Exists(GetRepoPath(relativePath)), $"Legacy stylesheet must not be restored: {relativePath}");
    }

    private static string ReadPtgCss()
    {
        var ptgRoot = GetRepoPath("src/PTGOilSystem.Web/wwwroot/css/ptg");

        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(ptgRoot, "*.css")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(File.ReadAllText));
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
                     Path.GetDirectoryName(sourceFilePath) ?? string.Empty,
                 })
        {
            var directory = new DirectoryInfo(start);

            while (directory is not null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "src", "PTGOilSystem.Web"))
                    && Directory.Exists(Path.Combine(directory.FullName, "tests")))
                {
                    return Path.GetFullPath(Path.Combine(directory.FullName, normalizedPath));
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root for ShellViewStructureTests.");
    }
}
