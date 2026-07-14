using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services;

public sealed class ExpenseRuleGenerationRequest
{
    public DateTime ExpenseDate { get; init; }
    public int? ContractId { get; init; }
    public int? ShipmentId { get; init; }
    public int? TruckDispatchId { get; init; }
    public decimal? QuantityMt { get; init; }
    public decimal? BaseAmountUsd { get; init; }
    public decimal? AppliedFxRateToUsd { get; init; }
    public string? Description { get; init; }
}

public interface IExpenseRuleEngine
{
    decimal CalculateAmount(
        ExpenseRule rule,
        decimal? quantityMt = null,
        decimal? baseAmountUsd = null);

    Task<int> GenerateExpenseAsync(
        ExpenseRule rule,
        ExpenseRuleGenerationRequest request,
        CancellationToken ct = default);
}
