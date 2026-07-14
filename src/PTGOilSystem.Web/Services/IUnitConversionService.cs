namespace PTGOilSystem.Web.Services;

public interface IUnitConversionService
{
    Task<bool> CanConvertAsync(int fromUnitId, int toUnitId, CancellationToken ct = default);

    Task<decimal> ConvertAsync(decimal value, int fromUnitId, int toUnitId, CancellationToken ct = default);
}
