using PTGOilSystem.Web.Models;

namespace PTGOilSystem.Web.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> BuildDashboardAsync(CancellationToken ct = default);
}
