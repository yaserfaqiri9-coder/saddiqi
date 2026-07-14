using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Helpers;

public sealed record ContractLookupOption(int Id, string Display);

public static class ContractUiText
{
    public static string ResolveUnitText(Unit? unit)
        => ResolveUnitText(unit?.Symbol, unit?.Code, unit?.NamePersian, unit?.Name);

    public static string ResolveUnitText(
        string? symbol,
        string? code,
        string? namePersian,
        string? name)
    {
        foreach (var candidate in new[] { symbol, code, namePersian, name })
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate.Trim();
            }
        }

        return "—";
    }

    public static string FormatLookup(Contract contract)
        => FormatLookup(
            contract.ContractNumber,
            contract.ContractType,
            contract.Product?.Name,
            ResolveUnitText(contract.Unit));

    public static string FormatLookup(
        string contractNumber,
        ContractType contractType,
        string? productName,
        string? unitText)
    {
        var parts = new List<string>
        {
            contractNumber.Trim(),
            contractType == ContractType.Purchase ? "خرید" : "فروش"
        };

        if (!string.IsNullOrWhiteSpace(productName))
        {
            parts.Add(productName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(unitText) && unitText.Trim() != "—")
        {
            parts.Add($"واحد {unitText.Trim()}");
        }

        return string.Join(" | ", parts);
    }

    public static IReadOnlyList<ContractLookupOption> ToLookupOptions(IEnumerable<Contract> contracts)
        => contracts
            .Select(contract => new ContractLookupOption(contract.Id, FormatLookup(contract)))
            .ToList();
}