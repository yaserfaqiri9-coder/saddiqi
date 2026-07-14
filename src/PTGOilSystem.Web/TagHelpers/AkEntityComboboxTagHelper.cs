using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PTGOilSystem.Web.TagHelpers;

/// <summary>
/// Marks entity-backed selects for the single shared searchable combobox.
/// The real select remains the binding and event source; the client-side
/// component is progressive enhancement only.
/// </summary>
[HtmlTargetElement("select", Attributes = "asp-for")]
[HtmlTargetElement("select", Attributes = "data-ak-entity-combobox")]
public sealed class AkEntityComboboxTagHelper : TagHelper
{
    private static readonly EntityDefinition[] Entities =
    [
        new("CashAccount", "CashAccounts", "حساب جدید"),
        new("ServiceProvider", "ServiceProviders", "شرکت خدماتی جدید"),
        new("OperationalAsset", "OperationalAssets", "دارایی جدید"),
        new("StorageTank", "StorageTanks", "مخزن جدید"),
        new("ExpenseType", "ExpenseTypes", "نوع مصرف جدید"),
        new("PurchaseContract", "Contracts", "قرارداد جدید"),
        new("SaleContract", "Contracts", "قرارداد جدید"),
        new("SourceContract", "Contracts", "قرارداد جدید"),
        new("Contract", "Contracts", "قرارداد جدید"),
        new("DestinationTerminal", "Terminals", "ترمینل جدید"),
        new("SourceTerminal", "Terminals", "ترمینل جدید"),
        new("Terminal", "Terminals", "ترمینل جدید"),
        new("DestinationLocation", "Locations", "موقعیت جدید"),
        new("SourceLocation", "Locations", "موقعیت جدید"),
        new("Location", "Locations", "موقعیت جدید"),
        new("SaleCustomer", "Customers", "مشتری جدید"),
        new("Customer", "Customers", "مشتری جدید"),
        new("Supplier", "Suppliers", "تأمین‌کننده جدید"),
        new("Company", "Companies", "شرکت جدید"),
        new("Partner", "Partners", "شریک جدید"),
        new("Product", "Products", "جنس جدید"),
        new("Unit", "Units", "واحد جدید"),
        new("Truck", "Trucks", "موتر جدید"),
        new("Driver", "Drivers", "راننده جدید"),
        new("Wagon", "Wagons", "واگون جدید"),
        new("Vessel", "Vessels", "کشتی جدید"),
        new("Shipment", "Shipments", "محموله جدید"),
        new("Employee", "Employees", "کارمند جدید"),
        new("Role", "Roles", "نقش جدید"),
        new("Sarraf", "Sarrafs", "صراف جدید"),
        new("Currency", "Currencies", "ارز جدید")
    ];

    [HtmlAttributeName("asp-for")]
    public ModelExpression? For { get; set; }

    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var explicitMode = context.AllAttributes["data-ak-entity-combobox"]?.Value?.ToString();
        if (string.Equals(explicitMode, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(explicitMode, "off", StringComparison.OrdinalIgnoreCase))
        {
            output.Attributes.RemoveAll("data-ak-entity-combobox");
            return;
        }

        var fieldName = For?.Name
            ?? output.Attributes["name"]?.Value?.ToString()
            ?? context.AllAttributes["name"]?.Value?.ToString();

        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return;
        }

        var normalized = Normalize(fieldName);
        var entity = Entities.FirstOrDefault(candidate => IsEntityField(normalized, candidate.Key));
        if (entity is null)
        {
            return;
        }

        output.Attributes.SetAttribute("data-ak-entity-combobox", "true");
        SetIfMissing(output, "data-ak-placeholder", "برای جستجو بنویسید...");
        SetIfMissing(output, "data-ak-empty", "موردی پیدا نشد");
        SetIfMissing(output, "data-ak-quick-create-label", entity.CreateLabel);
        SetIfMissing(output, "data-ak-quick-create-url", $"/{entity.Controller}/Create");
    }

    private static string Normalize(string fieldName)
    {
        var segment = fieldName.Split('.').Last();
        var bracket = segment.LastIndexOf(']');
        return bracket >= 0 && bracket + 1 < segment.Length
            ? segment[(bracket + 1)..]
            : segment;
    }

    private static bool IsEntityField(string fieldName, string key)
    {
        if (fieldName.Equals(key, StringComparison.OrdinalIgnoreCase)
            || fieldName.Equals($"{key}Id", StringComparison.OrdinalIgnoreCase)
            || fieldName.EndsWith($"{key}Id", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return key == "Currency"
            && (fieldName.EndsWith("Currency", StringComparison.OrdinalIgnoreCase)
                || fieldName.EndsWith("CurrencyCode", StringComparison.OrdinalIgnoreCase));
    }

    private static void SetIfMissing(TagHelperOutput output, string name, string value)
    {
        if (!output.Attributes.ContainsName(name))
        {
            output.Attributes.SetAttribute(name, value);
        }
    }

    private sealed record EntityDefinition(string Key, string Controller, string CreateLabel);
}
