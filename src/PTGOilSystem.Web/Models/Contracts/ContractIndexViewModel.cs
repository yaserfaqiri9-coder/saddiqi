using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Models.Contracts;

public sealed class ContractIndexViewModel
{
    public string? Query { get; init; }
    public ContractType? Type { get; init; }
    public ContractStatus? Status { get; init; }
    public IReadOnlyList<Contract> Items { get; init; } = [];
    public int CurrentPage { get; init; } = 1;
    public int PageCount { get; init; } = 1;
    public int TotalCount { get; init; }
    public int ActiveCount { get; init; }
    public int PurchaseCount { get; init; }
    public int SaleCount { get; init; }
}
