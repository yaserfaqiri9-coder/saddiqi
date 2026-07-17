using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Accounting;

namespace PTGOilSystem.Web.Services.Accounting;

public interface IChartOfAccountsReadService
{
    Task<ChartOfAccountsIndexViewModel> BuildAsync(
        int? companyId,
        string? search,
        int page,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// فهرست فقط‌خواندنی سرفصل حساب‌ها. منبع Query فقط DbSet حساب‌های هسته جدید است.
/// </summary>
public sealed class ChartOfAccountsReadService(ApplicationDbContext db) : IChartOfAccountsReadService
{
    private const int PageSize = 20;

    public async Task<ChartOfAccountsIndexViewModel> BuildAsync(
        int? companyId,
        string? search,
        int page,
        CancellationToken cancellationToken = default)
    {
        var companyIds = await db.Accounts.AsNoTracking()
            .Select(account => account.CompanyId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);

        var selectedCompanyId = companyId is int requested && companyIds.Contains(requested)
            ? requested
            : companyIds.Select(id => (int?)id).FirstOrDefault();

        var companies = companyIds
            .Select(id => new ChartOfAccountsCompanyOption(id, id == selectedCompanyId))
            .ToList();

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (selectedCompanyId is null)
        {
            return new ChartOfAccountsIndexViewModel(
                companies,
                null,
                normalizedSearch,
                [],
                CurrentPage: 1,
                PageCount: 1,
                TotalCount: 0,
                PageSize);
        }

        var query = db.Accounts.AsNoTracking()
            .Where(account => account.CompanyId == selectedCompanyId.Value);

        if (normalizedSearch is not null)
        {
            query = query.Where(account =>
                account.Code.Contains(normalizedSearch)
                || account.Name.Contains(normalizedSearch)
                || (account.ParentAccount != null
                    && (account.ParentAccount.Code.Contains(normalizedSearch)
                        || account.ParentAccount.Name.Contains(normalizedSearch))));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageCount = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        var currentPage = Math.Clamp(page, 1, pageCount);

        var items = await query
            .OrderBy(account => account.ParentAccountId.HasValue
                ? account.ParentAccount!.Code
                : account.Code)
            .ThenBy(account => account.ParentAccountId.HasValue)
            .ThenBy(account => account.Code)
            .Skip((currentPage - 1) * PageSize)
            .Take(PageSize)
            .Select(account => new ChartOfAccountsRowViewModel(
                account.Id,
                account.CompanyId,
                account.Code,
                account.Name,
                account.AccountType,
                account.NormalBalance,
                account.IsActive,
                account.ParentAccountId,
                account.ParentAccount != null ? account.ParentAccount.Code : null,
                account.ParentAccount != null ? account.ParentAccount.Name : null))
            .ToListAsync(cancellationToken);

        return new ChartOfAccountsIndexViewModel(
            companies,
            selectedCompanyId,
            normalizedSearch,
            items,
            currentPage,
            pageCount,
            totalCount,
            PageSize);
    }
}
