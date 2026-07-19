using PTGOilSystem.Web.Models.ShipmentPnl;
using Xunit;

namespace PTGOilSystem.Web.Tests;

/// <summary>
/// Regression tests for the P0 fix that moved shipment expense categorisation
/// out of the Razor view (two divergent keyword lists) into a single C# source.
/// </summary>
public class ShipmentExpenseCategorizerTests
{
    private static ShipmentExpenseDisplayRow Row(
        string typeName = "",
        string? description = null,
        decimal amountUsd = 0m,
        bool isCustoms = false,
        string? dbCategory = null)
        => new()
        {
            ExpenseTypeName = typeName,
            Description = description,
            AmountUsd = amountUsd,
            IsCustoms = isCustoms,
            ExpenseTypeCategory = dbCategory
        };

    [Fact]
    public void Customs_Flag_Wins_Over_Everything()
    {
        var row = Row(typeName: "کرایه حمل", dbCategory: "Transport", isCustoms: true);
        Assert.Equal(ShipmentExpenseCategory.Customs, ShipmentExpenseCategorizer.Categorize(row));
    }

    [Theory]
    [InlineData("Transport", ShipmentExpenseCategory.Freight)]
    [InlineData("transport", ShipmentExpenseCategory.Freight)]
    [InlineData("Storage", ShipmentExpenseCategory.Terminal)]
    [InlineData("Commission", ShipmentExpenseCategory.Other)]
    public void Db_Category_Is_Structured_Primary_Source(string dbCategory, ShipmentExpenseCategory expected)
    {
        var row = Row(typeName: "هر نامی", dbCategory: dbCategory);
        Assert.Equal(expected, ShipmentExpenseCategorizer.Categorize(row));
    }

    [Fact]
    public void Db_Category_Other_Falls_Back_To_Keyword_Table()
    {
        // «Other» مقدار پیش‌فرض ستون است و نباید دسته‌بندی واژه‌نامه را خنثی کند.
        var row = Row(typeName: "کرایه واگن", dbCategory: "Other");
        Assert.Equal(ShipmentExpenseCategory.Freight, ShipmentExpenseCategorizer.Categorize(row));
    }

    [Theory]
    [InlineData("کرایه حمل", ShipmentExpenseCategory.Freight)]
    [InlineData("Freight to border", ShipmentExpenseCategory.Freight)]
    [InlineData("مصرف گدام حیرتان", ShipmentExpenseCategory.Terminal)]
    [InlineData("Terminal handling", ShipmentExpenseCategory.Terminal)]
    [InlineData("ابزاردیه", ShipmentExpenseCategory.Documents)]
    [InlineData("مجوز عبور", ShipmentExpenseCategory.Documents)]
    [InlineData("متفرقه", ShipmentExpenseCategory.Other)]
    public void Keyword_Fallback_Uses_Single_Unified_Term_List(string typeName, ShipmentExpenseCategory expected)
    {
        var row = Row(typeName: typeName);
        Assert.Equal(expected, ShipmentExpenseCategorizer.Categorize(row));
    }

    [Fact]
    public void Legacy_Kpi_Gap_Terms_Now_Categorized_Consistently()
    {
        // پیش از اصلاح، «گدام» و «مجوز» فقط در فهرست دومِ View بودند و KPI با جدول نمی‌خواند.
        // (اولویت دسته‌ها مثل قبل است: کرایه → ترمینال → اسناد؛ پس عبارت بدون واژهٔ کرایه.)
        Assert.Equal(ShipmentExpenseCategory.Terminal, ShipmentExpenseCategorizer.Categorize(Row(typeName: "فیس گدام")));
        Assert.Equal(ShipmentExpenseCategory.Documents, ShipmentExpenseCategorizer.Categorize(Row(typeName: "مجوز")));
    }

    [Fact]
    public void Group_Totals_Partition_The_Row_Total_Exactly()
    {
        var rows = new[]
        {
            Row(typeName: "کرایه حمل", amountUsd: 100m),
            Row(typeName: "گمرک", amountUsd: 50m, isCustoms: true),
            Row(typeName: "مصرف انبار", amountUsd: 30m),
            Row(typeName: "اسناد", amountUsd: 20m),
            Row(typeName: "متفرقه", amountUsd: 7.5m)
        };

        var groups = ShipmentExpenseCategorizer.Group(rows);

        Assert.Equal(rows.Sum(r => r.AmountUsd), groups.Sum(g => g.TotalUsd));
        Assert.Equal(rows.Length, groups.Sum(g => g.Rows.Count));
        Assert.Equal(100m, ShipmentExpenseCategorizer.TotalFor(groups, ShipmentExpenseCategory.Freight));
        Assert.Equal(50m, ShipmentExpenseCategorizer.TotalFor(groups, ShipmentExpenseCategory.Customs));
        Assert.Equal(30m, ShipmentExpenseCategorizer.TotalFor(groups, ShipmentExpenseCategory.Terminal));
        Assert.Equal(20m, ShipmentExpenseCategorizer.TotalFor(groups, ShipmentExpenseCategory.Documents));
        Assert.Equal(7.5m, ShipmentExpenseCategorizer.TotalFor(groups, ShipmentExpenseCategory.Other));
    }

    [Fact]
    public void Group_Keeps_Fixed_Ui_Order_And_Drops_Empty_Categories()
    {
        var rows = new[]
        {
            Row(typeName: "متفرقه", amountUsd: 1m),
            Row(typeName: "کرایه حمل", amountUsd: 2m)
        };

        var groups = ShipmentExpenseCategorizer.Group(rows);

        Assert.Equal(2, groups.Count);
        Assert.Equal(ShipmentExpenseCategory.Freight, groups[0].Category);
        Assert.Equal(ShipmentExpenseCategory.Other, groups[1].Category);
    }
}
