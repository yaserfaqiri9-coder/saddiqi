using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

/// <summary>
/// خطای پیکربندیِ شرکتِ مالک: نبودِ مالک یا وجودِ بیش از یک مالک. عمداً از
/// <see cref="AccountingValidationException"/> جداست تا این «خطای راه‌اندازی/پیکربندی» با
/// «خطای اعتبارسنجیِ یک سند» اشتباه نشود.
/// </summary>
public sealed class SystemCompanyConfigurationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// منبعِ مرکزیِ «شرکتِ مالکِ سیستم». مالک فقط از <see cref="Company.IsSystemOwner"/> شناسایی می‌شود؛
/// هیچ شناسه‌ای هاردکد نمی‌شود و <see cref="Company.IsActive"/> معیارِ مالک‌بودن نیست. سیستم فعلاً
/// Single-Company است، ولی چون فیلدِ CompanyId در مدل می‌ماند، فردا می‌تواند چندشرکتی شود.
/// </summary>
public interface ISystemCompanyProvider
{
    /// <summary>شناسهٔ شرکتِ مالک؛ اگر مالک صفر یا بیش از یک باشد خطای پیکربندی می‌دهد.</summary>
    Task<int> GetOwnerCompanyIdAsync(CancellationToken cancellationToken = default);

    /// <summary>خودِ شرکتِ مالک؛ اگر مالک صفر یا بیش از یک باشد خطای پیکربندی می‌دهد.</summary>
    Task<Company> GetOwnerCompanyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// نسخهٔ نرم برای گاردها: اگر دقیقاً یک مالک باشد شناسه‌اش، اگر هیچ مالکی نباشد null. وجودِ
    /// بیش از یک مالک همچنان خطای پیکربندی است، چون همیشه اشتباهِ راه‌اندازی است.
    /// </summary>
    Task<int?> FindOwnerCompanyIdAsync(CancellationToken cancellationToken = default);
}

public sealed class SystemCompanyProvider(ApplicationDbContext db) : ISystemCompanyProvider
{
    public async Task<Company> GetOwnerCompanyAsync(CancellationToken cancellationToken = default)
    {
        var owners = await db.Companies.AsNoTracking()
            .Where(c => c.IsSystemOwner)
            .OrderBy(c => c.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        return owners.Count switch
        {
            1 => owners[0],
            0 => throw new SystemCompanyConfigurationException(
                "NO_SYSTEM_OWNER",
                "هیچ شرکتِ مالکی تعیین نشده است. باید دقیقاً یک شرکت با IsSystemOwner = true وجود داشته باشد."),
            _ => throw MultipleOwners()
        };
    }

    public async Task<int> GetOwnerCompanyIdAsync(CancellationToken cancellationToken = default)
        => (await GetOwnerCompanyAsync(cancellationToken)).Id;

    public async Task<int?> FindOwnerCompanyIdAsync(CancellationToken cancellationToken = default)
    {
        var owners = await db.Companies.AsNoTracking()
            .Where(c => c.IsSystemOwner)
            .OrderBy(c => c.Id)
            .Select(c => c.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        return owners.Count switch
        {
            1 => owners[0],
            0 => null,
            _ => throw MultipleOwners()
        };
    }

    private static SystemCompanyConfigurationException MultipleOwners()
        => new(
            "MULTIPLE_SYSTEM_OWNERS",
            "بیش از یک شرکتِ مالک تعیین شده است. فقط یک شرکت می‌تواند مالکِ سیستم باشد.");
}
