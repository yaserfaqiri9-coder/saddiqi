using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using PTGOilSystem.Web.Models.Entities;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class UnitConversionSupportStructureTests
{
    [Fact]
    public void Unit_Entity_Exposes_Safe_Conversion_Metadata()
    {
        AssertStringProperty<Unit>("UnitType", 50);
        AssertStringProperty<Unit>("BaseUnitCode", 50);
        AssertStringProperty<Unit>("Notes", 1000);

        var factor = typeof(Unit).GetProperty("ConversionFactorToBase", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(factor);
        Assert.Equal(typeof(decimal?), factor!.PropertyType);

        var isBase = typeof(Unit).GetProperty("IsBaseUnit", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(isBase);
        Assert.Equal(typeof(bool), isBase!.PropertyType);
        Assert.False((bool)(isBase.GetValue(new Unit()) ?? true));
    }

    [Fact]
    public void Product_Entity_Exposes_Optional_Secondary_Unit_Metadata()
    {
        var secondaryUnitId = typeof(Product).GetProperty("SecondaryUnitId", BindingFlags.Instance | BindingFlags.Public);
        var secondaryUnit = typeof(Product).GetProperty("SecondaryUnit", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(secondaryUnitId);
        Assert.Equal(typeof(int?), secondaryUnitId!.PropertyType);
        Assert.NotNull(secondaryUnit);
        Assert.Equal(typeof(Unit), Nullable.GetUnderlyingType(secondaryUnit!.PropertyType) ?? secondaryUnit.PropertyType);
        AssertStringProperty<Product>("SecondaryUnitConversionNote", 1000);
    }

    [Fact]
    public void Unit_Views_Expose_Conversion_Fields_Without_Removing_Core_Fields()
    {
        var createForm = ReadRepoFile("src/PTGOilSystem.Web/Views/Units/_CreateForm.cshtml");
        var edit = ReadRepoFile("src/PTGOilSystem.Web/Views/Units/Edit.cshtml");
        var details = ReadRepoFile("src/PTGOilSystem.Web/Views/Units/Details.cshtml");
        var index = ReadRepoFile("src/PTGOilSystem.Web/Views/Units/Index.cshtml");
        var combinedForms = createForm + edit;

        foreach (var field in new[] { "Code", "Name", "NamePersian", "Symbol", "UnitType", "IsBaseUnit", "BaseUnitCode", "ConversionFactorToBase", "Notes" })
        {
            Assert.Contains($"asp-for=\"{field}\"", combinedForms);
        }

        Assert.Contains("تبدیل واحد", combinedForms);
        Assert.Contains("نوع واحد", details + index);
        Assert.Contains("واحد پایه", details + index);
        Assert.DoesNotContain("asp-for=\"Notes\"", index);
    }

    [Fact]
    public void Product_Views_Expose_Secondary_Unit_As_Optional_Display_Metadata()
    {
        var createForm = ReadRepoFile("src/PTGOilSystem.Web/Views/Products/_CreateForm.cshtml");
        var edit = ReadRepoFile("src/PTGOilSystem.Web/Views/Products/Edit.cshtml");
        var details = ReadRepoFile("src/PTGOilSystem.Web/Views/Products/Details.cshtml");
        var combinedForms = createForm + edit;

        Assert.Contains("asp-for=\"UnitId\"", combinedForms);
        Assert.Contains("asp-for=\"SecondaryUnitId\"", combinedForms);
        Assert.DoesNotContain("asp-for=\"SecondaryUnitConversionNote\"", combinedForms);
        Assert.Contains("محاسبات موجودی هنوز با واحد عملیاتی فعلی سیستم انجام می‌شود", combinedForms);
        Assert.Contains("SecondaryUnit", details);
        Assert.Contains("SecondaryUnitConversionNote", details);
    }

    [Fact]
    public void DbContext_Configures_Unit_Conversion_Precision_And_NonCascade_Secondary_Unit()
    {
        var dbContext = ReadRepoFile("src/PTGOilSystem.Web/Data/ApplicationDbContext.cs");

        Assert.Contains("ConversionFactorToBase", dbContext);
        Assert.Contains("numeric(18,10)", dbContext);
        Assert.Contains("HasDefaultValue(false)", dbContext);
        Assert.Contains("SecondaryUnit", dbContext);
        Assert.DoesNotContain("HasForeignKey(p => p.SecondaryUnitId)\r\n            .OnDelete(DeleteBehavior.Cascade)", dbContext);
        Assert.DoesNotContain("HasForeignKey(p => p.SecondaryUnitId)\n            .OnDelete(DeleteBehavior.Cascade)", dbContext);
    }

    private static void AssertStringProperty<T>(string name, int maxLength)
    {
        var property = typeof(T).GetProperty(name, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        Assert.Equal(typeof(string), property!.PropertyType);
        Assert.Equal(maxLength, property.GetCustomAttribute<MaxLengthAttribute>()?.Length);
    }

    private static string ReadRepoFile(string relativePath)
        => File.ReadAllText(GetRepoPath(relativePath));

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
