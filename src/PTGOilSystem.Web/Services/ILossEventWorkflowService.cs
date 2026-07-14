using PTGOilSystem.Web.Models.LossEvents;

namespace PTGOilSystem.Web.Services;

public interface ILossEventWorkflowService
{
    LossEventComputation ComputeMetrics(decimal expectedQuantityMt, decimal actualQuantityMt, decimal toleranceQuantityMt);

    Task ValidateAsync(
        LossEventSubmission submission,
        Action<string, string> addError,
        CancellationToken ct = default);

    Task<LossEventWorkflowResult> CreateAsync(
        LossEventSubmission submission,
        CancellationToken ct = default);
}
