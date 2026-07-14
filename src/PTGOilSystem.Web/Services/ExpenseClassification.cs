using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services;

public static class ExpenseClassification
{
    private static readonly string[] WagonRentTerms =
    [
        "wagon rent",
        "rent of wagon",
        "rent of wagons",
        "wagon-rent",
        "wagon_rent",
        "wagons rent",
        "کرایه واگون",
        "کرایه واگن",
        "اجاره واگون",
        "اجاره واگن"
    ];

    public static bool IsWagonRentExpense(ExpenseTransaction expense)
        => IsWagonRent(
            expense.ExpenseType?.Code,
            expense.ExpenseType?.Name,
            expense.ExpenseType?.NamePersian,
            expense.Description);

    public static bool IsWagonRent(string? code, string? name, string? namePersian, string? description)
    {
        var text = string.Join(' ', code, name, namePersian, description);
        return WagonRentTerms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
