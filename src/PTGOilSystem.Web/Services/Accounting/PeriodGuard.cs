using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;

namespace PTGOilSystem.Web.Services.Accounting;

public interface IPeriodGuard
{
    Task<FiscalCalendarSelection> EnsurePostingAllowedAsync(
        int companyId,
        DateTime accountingDate,
        CancellationToken cancellationToken = default);
}

public sealed class PeriodGuard(
    ApplicationDbContext db,
    IFiscalCalendarService fiscalCalendar) : IPeriodGuard
{
    public async Task<FiscalCalendarSelection> EnsurePostingAllowedAsync(
        int companyId,
        DateTime accountingDate,
        CancellationToken cancellationToken = default)
    {
        if (companyId <= 0 || !await db.Companies.AsNoTracking()
                .AnyAsync(x => x.Id == companyId && x.IsActive, cancellationToken))
        {
            throw new AccountingValidationException(
                "INVALID_COMPANY",
                "The accounting company is missing or inactive.");
        }

        var selection = await fiscalCalendar.FindOpenPeriodAsync(
            companyId,
            accountingDate,
            cancellationToken);

        return selection ?? throw new AccountingValidationException(
            "CLOSED_ACCOUNTING_DATE",
            "The accounting date is not inside an open fiscal year and open fiscal period.");
    }
}
