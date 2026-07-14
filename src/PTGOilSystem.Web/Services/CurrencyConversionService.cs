using PTGOilSystem.Web.Services.Exceptions;

namespace PTGOilSystem.Web.Services;

public static class SystemCurrency
{
    public const string BaseCurrencyCode = "USD";

    public static bool IsBaseCurrency(string? currencyCode)
        => string.Equals((currencyCode ?? string.Empty).Trim(), BaseCurrencyCode, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? currencyCode)
        => string.IsNullOrWhiteSpace(currencyCode)
            ? BaseCurrencyCode
            : currencyCode.Trim().ToUpperInvariant();
}

public sealed record CurrencyConversionResult(
    string SourceCurrencyCode,
    string BaseCurrencyCode,
    decimal AppliedRateToBase,
    DateTime EffectiveDate,
    bool FallbackApplied,
    bool ManualOverride,
    string SourceDescription)
{
    public decimal ConvertToBase(decimal sourceAmount)
        => decimal.Round(sourceAmount * AppliedRateToBase, 4, MidpointRounding.AwayFromZero);
}

public interface ICurrencyConversionService
{
    Task<CurrencyConversionResult> ResolveToBaseAsync(
        string sourceCurrencyCode,
        DateTime transactionDate,
        decimal? manualRateToBase = null,
        CancellationToken ct = default);
}

public sealed class CurrencyConversionService : ICurrencyConversionService
{
    private readonly IPricingService _pricing;

    public CurrencyConversionService(IPricingService pricing)
        => _pricing = pricing;

    public async Task<CurrencyConversionResult> ResolveToBaseAsync(
        string sourceCurrencyCode,
        DateTime transactionDate,
        decimal? manualRateToBase = null,
        CancellationToken ct = default)
    {
        var normalizedCurrency = SystemCurrency.Normalize(sourceCurrencyCode);
        var normalizedDate = DateTime.SpecifyKind(transactionDate.Date, DateTimeKind.Utc);

        if (SystemCurrency.IsBaseCurrency(normalizedCurrency))
        {
            return new CurrencyConversionResult(
                SourceCurrencyCode: SystemCurrency.BaseCurrencyCode,
                BaseCurrencyCode: SystemCurrency.BaseCurrencyCode,
                AppliedRateToBase: 1m,
                EffectiveDate: normalizedDate,
                FallbackApplied: false,
                ManualOverride: false,
                SourceDescription: "Identity USD/USD");
        }

        if (manualRateToBase.HasValue)
        {
            if (manualRateToBase <= 0m)
            {
                throw new BusinessRuleException(
                    "FX_RATE_INVALID",
                    "نرخ تبدیل باید بزرگ‌تر از صفر باشد.");
            }

            return new CurrencyConversionResult(
                SourceCurrencyCode: normalizedCurrency,
                BaseCurrencyCode: SystemCurrency.BaseCurrencyCode,
                AppliedRateToBase: manualRateToBase.Value,
                EffectiveDate: normalizedDate,
                FallbackApplied: false,
                ManualOverride: true,
                SourceDescription: $"Manual FX {normalizedCurrency}/{SystemCurrency.BaseCurrencyCode}");
        }

        var fxRate = await _pricing.GetFxRateAsync(
            normalizedCurrency,
            SystemCurrency.BaseCurrencyCode,
            normalizedDate,
            ct);

        return new CurrencyConversionResult(
            SourceCurrencyCode: normalizedCurrency,
            BaseCurrencyCode: SystemCurrency.BaseCurrencyCode,
            AppliedRateToBase: fxRate.Value,
            EffectiveDate: fxRate.EffectiveDate.Date,
            FallbackApplied: fxRate.FallbackApplied,
            ManualOverride: false,
            SourceDescription: fxRate.FallbackApplied
                ? $"DailyFxRate {normalizedCurrency}/{SystemCurrency.BaseCurrencyCode} fallback from {fxRate.EffectiveDate:yyyy-MM-dd}"
                : $"DailyFxRate {normalizedCurrency}/{SystemCurrency.BaseCurrencyCode} on {fxRate.EffectiveDate:yyyy-MM-dd}");
    }
}
