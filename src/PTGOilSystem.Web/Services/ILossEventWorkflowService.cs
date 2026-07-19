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

    /// <summary>
    /// ثبت گروهی با تعداد ثابت SaveChanges. ترتیب نتایج با ترتیب ورودی یکسان است.
    /// تراکنش را فراخوان مدیریت می‌کند.
    /// </summary>
    Task<IReadOnlyList<LossEventWorkflowResult>> CreateBatchAsync(
        IReadOnlyList<LossEventSubmission> submissions,
        CancellationToken ct = default);
}
