using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public sealed record CreateNextFiscalYearResult(bool Succeeded, string? ErrorCode, int? FiscalYearId);

public sealed record CreateInitialFiscalYearResult(bool Succeeded, string? ErrorCode, int? FiscalYearId);

/// <summary>
/// ورودیِ ساختِ اولین سال مالیِ یک شرکت. دوره‌ها به‌صورت ماهانه از <paramref name="PeriodCount"/>
/// ماهِ پیوسته تولید می‌شوند و باید دقیقاً بازهٔ [<paramref name="StartDate"/>, <paramref name="EndDate"/>]
/// را بدون فاصله یا هم‌پوشانی بپوشانند.
/// </summary>
public sealed record CreateInitialFiscalYearInput(
    int CompanyId,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    int PeriodCount);

public interface IFiscalYearProvisioningService
{
    Task<CreateNextFiscalYearResult> CreateNextYearAsync(
        int companyId,
        int? actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// اولین سال مالیِ یک شرکت را می‌سازد — تنها زمانی که شرکت هیچ سال مالیِ موجودی نداشته باشد.
    /// سالِ اول با وضعیت <see cref="FiscalYearStatus.Open"/> و <see cref="FiscalYear.IsCurrent"/>
    /// ساخته می‌شود؛ برخلافِ <see cref="CreateNextYearAsync"/> که سالِ Draft از روی سالِ منبع آینه می‌کند.
    /// </summary>
    Task<CreateInitialFiscalYearResult> CreateInitialFiscalYearAsync(
        CreateInitialFiscalYearInput input,
        int? actorUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// مرحله ۱۰ — تنها مسیرِ نوشتنِ صفحهٔ سال مالی: ساختِ سال بعد.
///
/// سالِ جدید **آینهٔ دقیقِ آخرین سال** است (همان تعداد دوره، هر تاریخ یک سال جلوتر) و با وضعیت
/// <see cref="FiscalYearStatus.Draft"/> ساخته می‌شود؛ بازکردنش تصمیم جداگانه‌ای است و اینجا
/// خودکار انجام نمی‌شود. <see cref="FiscalYear.IsCurrent"/> هم دست‌نخورده می‌ماند — جابه‌جایی
/// سالِ جاری کارِ همین دکمه نیست.
/// </summary>
public sealed class FiscalYearProvisioningService(
    ApplicationDbContext db,
    IFiscalYearOverviewService overview,
    IAuditService audit) : IFiscalYearProvisioningService
{
    public async Task<CreateNextFiscalYearResult> CreateNextYearAsync(
        int companyId,
        int? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var proposal = await overview.BuildNextYearProposalAsync(companyId, cancellationToken);
        if (!proposal.IsAllowed
            || proposal.ProposedStartDate is not DateTime start
            || proposal.ProposedEndDate is not DateTime end)
        {
            return new CreateNextFiscalYearResult(false, proposal.BlockedReason ?? "NEXT_YEAR_NOT_ALLOWED", null);
        }

        var source = await overview.FindSourceYearAsync(companyId, cancellationToken);
        if (source is null)
            return new CreateNextFiscalYearResult(false, "SOURCE_FISCAL_YEAR_NOT_FOUND", null);

        var sourcePeriods = await db.FiscalPeriods.AsNoTracking()
            .Where(p => p.FiscalYearId == source.Id)
            .OrderBy(p => p.PeriodNumber)
            .ToListAsync(cancellationToken);

        if (sourcePeriods.Count == 0)
            return new CreateNextFiscalYearResult(false, "SOURCE_FISCAL_YEAR_HAS_NO_PERIODS", null);

        var owned = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var year = new FiscalYear
            {
                CompanyId = companyId,
                Name = proposal.ProposedName ?? $"FY-{start.Year}",
                StartDate = start,
                EndDate = end,
                Status = FiscalYearStatus.Draft,
                PreviousFiscalYearId = source.Id,
                IsCurrent = false,
                CreatedByUserId = actorUserId
            };
            db.FiscalYears.Add(year);
            await db.SaveChangesAsync(cancellationToken);

            foreach (var period in sourcePeriods)
            {
                db.FiscalPeriods.Add(new FiscalPeriod
                {
                    CompanyId = companyId,
                    FiscalYearId = year.Id,
                    PeriodNumber = period.PeriodNumber,
                    Name = period.Name,
                    StartDate = period.StartDate.AddYears(1).Date,
                    EndDate = period.EndDate.AddYears(1).Date,
                    Status = FiscalPeriodStatus.Open,
                    CreatedByUserId = actorUserId
                });
            }

            await audit.LogAsync(
                nameof(FiscalYear),
                year.Id,
                AuditAction.Insert,
                actorUserId,
                JsonSerializer.Serialize(new
                {
                    Action = "CreateNextFiscalYear",
                    CompanyId = companyId,
                    SourceFiscalYearId = source.Id,
                    year.Name,
                    StartDate = start,
                    EndDate = end,
                    PeriodCount = sourcePeriods.Count,
                    Status = nameof(FiscalYearStatus.Draft)
                }),
                cancellationToken);

            await db.SaveChangesAsync(cancellationToken);

            if (owned is not null)
                await owned.CommitAsync(cancellationToken);

            return new CreateNextFiscalYearResult(true, null, year.Id);
        }
        catch
        {
            if (owned is not null)
                await owned.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (owned is not null)
                await owned.DisposeAsync();
        }
    }

    public async Task<CreateInitialFiscalYearResult> CreateInitialFiscalYearAsync(
        CreateInitialFiscalYearInput input,
        int? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var name = input.Name?.Trim() ?? "";
        var start = input.StartDate.Date;
        var end = input.EndDate.Date;

        // اعتبارسنجی ورودی پیش از هر نوشتنی. هیچ‌کدام از این‌ها نباید حدس زده یا اصلاح شوند.
        if (string.IsNullOrWhiteSpace(name))
            return new CreateInitialFiscalYearResult(false, "FISCAL_YEAR_NAME_REQUIRED", null);
        if (end <= start)
            return new CreateInitialFiscalYearResult(false, "END_DATE_NOT_AFTER_START_DATE", null);
        if (input.PeriodCount < 1)
            return new CreateInitialFiscalYearResult(false, "PERIOD_COUNT_INVALID", null);

        // فقط شرکتی که هیچ سال مالی‌ای ندارد. هر سالِ موجود یعنی مسیرِ درست «ساختِ سالِ بعد» است،
        // نه این متد — و ساختِ سالِ Open دوم می‌تواند یکتاییِ سالِ جاری/باز را بشکند.
        if (await db.FiscalYears.AnyAsync(y => y.CompanyId == input.CompanyId, cancellationToken))
            return new CreateInitialFiscalYearResult(false, "FISCAL_YEAR_ALREADY_EXISTS", null);

        var layout = BuildMonthlyPeriodLayout(start, end, input.PeriodCount);
        if (layout is null)
            return new CreateInitialFiscalYearResult(false, "PERIOD_LAYOUT_DOES_NOT_COVER_YEAR", null);

        var owned = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var year = new FiscalYear
            {
                CompanyId = input.CompanyId,
                Name = name,
                StartDate = start,
                EndDate = end,
                Status = FiscalYearStatus.Open,
                PreviousFiscalYearId = null,
                IsCurrent = true,
                OpenedAt = DateTime.UtcNow,
                OpenedByUserId = actorUserId,
                CreatedByUserId = actorUserId
            };
            db.FiscalYears.Add(year);
            await db.SaveChangesAsync(cancellationToken);

            foreach (var period in layout)
            {
                db.FiscalPeriods.Add(new FiscalPeriod
                {
                    CompanyId = input.CompanyId,
                    FiscalYearId = year.Id,
                    PeriodNumber = period.Number,
                    Name = $"P{period.Number}",
                    StartDate = period.Start,
                    EndDate = period.End,
                    Status = FiscalPeriodStatus.Open,
                    CreatedByUserId = actorUserId
                });
            }

            await audit.LogAsync(
                nameof(FiscalYear),
                year.Id,
                AuditAction.Insert,
                actorUserId,
                JsonSerializer.Serialize(new
                {
                    Action = "CreateInitialFiscalYear",
                    input.CompanyId,
                    year.Name,
                    StartDate = start,
                    EndDate = end,
                    PeriodCount = layout.Count,
                    Status = nameof(FiscalYearStatus.Open),
                    IsCurrent = true
                }),
                cancellationToken);

            await db.SaveChangesAsync(cancellationToken);

            if (owned is not null)
                await owned.CommitAsync(cancellationToken);

            return new CreateInitialFiscalYearResult(true, null, year.Id);
        }
        catch
        {
            if (owned is not null)
                await owned.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (owned is not null)
                await owned.DisposeAsync();
        }
    }

    private sealed record PeriodBounds(int Number, DateTime Start, DateTime End);

    /// <summary>
    /// <paramref name="periodCount"/> دورهٔ ماهانهٔ پیوسته تولید می‌کند که دقیقاً بازهٔ سال را
    /// می‌پوشاند: دورهٔ اول از <paramref name="start"/>، هر دوره یک ماه جلوتر، و دورهٔ آخر تا
    /// <paramref name="end"/>. اگر نتیجه فاصله/هم‌پوشانی داشته باشد یا دوره‌ای تهی شود (مثلاً وقتی
    /// تعداد دوره با طولِ بازه نمی‌خواند)، <c>null</c> برمی‌گرداند تا صدازننده آن را رد کند.
    /// </summary>
    private static IReadOnlyList<PeriodBounds>? BuildMonthlyPeriodLayout(
        DateTime start,
        DateTime end,
        int periodCount)
    {
        var periods = new List<PeriodBounds>(periodCount);
        for (var i = 0; i < periodCount; i++)
        {
            var periodStart = i == 0 ? start : start.AddMonths(i);
            var periodEnd = i == periodCount - 1 ? end : start.AddMonths(i + 1).AddDays(-1);
            periods.Add(new PeriodBounds(i + 1, periodStart.Date, periodEnd.Date));
        }

        // پوششِ دقیق: اولین دوره از شروعِ سال، آخرین دوره تا پایانِ سال، هر دوره ناتهی، و هر دوره
        // دقیقاً یک روز پس از دورهٔ قبلی شروع شود — بدون فاصله و بدون هم‌پوشانی.
        if (periods[0].Start != start || periods[^1].End != end)
            return null;

        for (var i = 0; i < periods.Count; i++)
        {
            if (periods[i].End < periods[i].Start)
                return null;
            if (i > 0 && periods[i].Start != periods[i - 1].End.AddDays(1))
                return null;
        }

        return periods;
    }
}
