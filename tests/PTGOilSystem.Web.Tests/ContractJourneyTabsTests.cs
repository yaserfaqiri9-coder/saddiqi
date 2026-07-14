using PTGOilSystem.Web.Models.ContractJourney;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class ContractJourneyTabsTests
{
    [Theory]
    [InlineData("inventoryTransport")]
    [InlineData("inventorytransport")]
    public void Details_Normalize_Keeps_InventoryTransport_Tab_Open(string tab)
    {
        var normalized = ContractJourneyTabs.Details.Normalize(tab);

        Assert.Equal(ContractJourneyTabs.Details.InventoryTransport, normalized);
    }
}
