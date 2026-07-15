using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public sealed record FiscalCalendarSelection(FiscalYear FiscalYear, FiscalPeriod FiscalPeriod);

public interface IFiscalCalendarService
{
    Task<FiscalCalendarSelection?> FindOpenPeriodAsync(
        int companyId,
        DateTime accountingDate,
        CancellationToken cancellationToken = default);
}

public sealed class FiscalCalendarService(ApplicationDbContext db) : IFiscalCalendarService
{
    public async Task<FiscalCalendarSelection?> FindOpenPeriodAsync(
        int companyId,
        DateTime accountingDate,
        CancellationToken cancellationToken = default)
    {
        var date = accountingDate.Date;
        var fiscalYear = await db.FiscalYears
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.CompanyId == companyId
                    && x.Status == FiscalYearStatus.Open
                    && x.StartDate <= date
                    && x.EndDate >= date,
                cancellationToken);

        if (fiscalYear is null)
            return null;

        var period = await db.FiscalPeriods
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.CompanyId == companyId
                    && x.FiscalYearId == fiscalYear.Id
                    && x.Status == FiscalPeriodStatus.Open
                    && x.StartDate <= date
                    && x.EndDate >= date,
                cancellationToken);

        return period is null ? null : new FiscalCalendarSelection(fiscalYear, period);
    }
}
