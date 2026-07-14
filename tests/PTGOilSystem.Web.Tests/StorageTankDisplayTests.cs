using PTGOilSystem.Web.Helpers;
using PTGOilSystem.Web.Models.Entities;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class StorageTankDisplayTests
{
    [Fact]
    public void Build_Prefers_DisplayName_And_Trims_It()
    {
        var tank = new StorageTank
        {
            Id = 7,
            TankCode = "TK-007",
            DisplayName = "  مخزن ترکمنستان شماره ۱  "
        };

        Assert.Equal("مخزن ترکمنستان شماره ۱", StorageTankDisplay.Build(tank));
    }

    [Theory]
    [InlineData(null, "TK-001", "TK-001")]
    [InlineData("   ", "TK-002", "TK-002")]
    [InlineData(null, "   ", "مخزن #5")]
    public void Build_Uses_Safe_Fallbacks(string? displayName, string? tankCode, string expected)
        => Assert.Equal(expected, StorageTankDisplay.Build(5, displayName, tankCode));
}
