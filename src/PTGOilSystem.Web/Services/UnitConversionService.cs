using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services.Exceptions;

namespace PTGOilSystem.Web.Services;

public class UnitConversionService : IUnitConversionService
{
    private readonly ApplicationDbContext _db;

    public UnitConversionService(ApplicationDbContext db)
        => _db = db;

    public async Task<bool> CanConvertAsync(int fromUnitId, int toUnitId, CancellationToken ct = default)
    {
        if (fromUnitId == toUnitId)
            return true;

        var units = await LoadUnitsAsync(fromUnitId, toUnitId, ct);
        if (units.From is null || units.To is null)
            return false;

        return IsConvertible(units.From, units.To);
    }

    public async Task<decimal> ConvertAsync(decimal value, int fromUnitId, int toUnitId, CancellationToken ct = default)
    {
        if (fromUnitId == toUnitId)
            return value;

        var units = await LoadUnitsAsync(fromUnitId, toUnitId, ct);
        if (units.From is null || units.To is null)
        {
            throw new BusinessRuleException(
                "UNIT_NOT_FOUND",
                "واحد مبدأ یا مقصد برای تبدیل پیدا نشد.");
        }

        ValidateConvertible(units.From, units.To);

        var valueInBase = value * units.From.ConversionFactorToBase!.Value;
        return valueInBase / units.To.ConversionFactorToBase!.Value;
    }

    private async Task<(Unit? From, Unit? To)> LoadUnitsAsync(int fromUnitId, int toUnitId, CancellationToken ct)
    {
        var units = await _db.Units
            .AsNoTracking()
            .Where(u => u.Id == fromUnitId || u.Id == toUnitId)
            .ToListAsync(ct);

        return (
            units.FirstOrDefault(u => u.Id == fromUnitId),
            units.FirstOrDefault(u => u.Id == toUnitId));
    }

    private static bool IsConvertible(Unit from, Unit to)
    {
        if (string.IsNullOrWhiteSpace(from.UnitType) || string.IsNullOrWhiteSpace(to.UnitType))
            return false;

        if (!string.Equals(from.UnitType.Trim(), to.UnitType.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(from.BaseUnitCode) || string.IsNullOrWhiteSpace(to.BaseUnitCode))
            return false;

        if (!string.Equals(from.BaseUnitCode.Trim(), to.BaseUnitCode.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        return HasUsableFactor(from) && HasUsableFactor(to);
    }

    private static void ValidateConvertible(Unit from, Unit to)
    {
        if (string.IsNullOrWhiteSpace(from.UnitType) || string.IsNullOrWhiteSpace(to.UnitType))
        {
            throw new BusinessRuleException(
                "UNIT_CONVERSION_TYPE_MISSING",
                "نوع واحد برای تبدیل مشخص نشده است.");
        }

        if (!string.Equals(from.UnitType.Trim(), to.UnitType.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "UNIT_CONVERSION_TYPE_MISMATCH",
                "تبدیل فقط بین واحدهای هم‌نوع ممکن است.");
        }

        if (string.IsNullOrWhiteSpace(from.BaseUnitCode) || string.IsNullOrWhiteSpace(to.BaseUnitCode)
            || !string.Equals(from.BaseUnitCode.Trim(), to.BaseUnitCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "UNIT_CONVERSION_BASE_MISMATCH",
                "واحدهای انتخاب‌شده پایه تبدیل مشترک ندارند.");
        }

        if (!HasUsableFactor(from) || !HasUsableFactor(to))
        {
            throw new BusinessRuleException(
                "UNIT_CONVERSION_FACTOR_MISSING",
                "ضریب تبدیل واحدها باید مشخص و بزرگ‌تر از صفر باشد.");
        }
    }

    private static bool HasUsableFactor(Unit unit)
        => unit.ConversionFactorToBase.HasValue && unit.ConversionFactorToBase.Value > 0m;
}
