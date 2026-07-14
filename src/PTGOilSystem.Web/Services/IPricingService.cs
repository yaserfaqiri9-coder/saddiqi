using System;
using System.Threading;
using System.Threading.Tasks;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services;

/// <summary>
/// Pricing lookup service.
///
/// System rules enforced here:
///   #5 — Pricing by transaction date: Platt's and FX must be looked up
///        using the transaction date, never the data-entry date.
///   #6 — Fallback policy: if no rate exists on the transaction date, the
///        latest valid rate strictly before that date is returned. If no
///        prior rate exists, a <see cref="Exceptions.BusinessRuleException"/>
///        is thrown so the caller cannot silently zero-out the price.
///
/// All returned rates are <see cref="decimal"/> (system rule #1).
/// </summary>
public interface IPricingService
{
    /// <summary>
    /// Calculates the effective contract unit price based on the contract's
    /// configured pricing method and related Platts data.
    /// </summary>
    Task<ContractPriceResult> CalculateContractPriceAsync(
        int contractId,
        CancellationToken ct = default);

    /// <summary>
    /// Calculates the effective contract unit price for an already-loaded contract
    /// to avoid an extra database round-trip.
    /// </summary>
    Task<ContractPriceResult> CalculateContractPriceAsync(
        Contract contract,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the Platt's USD/MT price for the given product+benchmark on
    /// <paramref name="transactionDate"/> with fallback to the last valid prior date.
    /// </summary>
    Task<PriceLookupResult> GetPlattsPriceAsync(
        int productId,
        string benchmarkCode,
        DateTime transactionDate,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the FX rate (base→quote) on <paramref name="transactionDate"/>
    /// with fallback to the last valid prior date.
    /// </summary>
    Task<PriceLookupResult> GetFxRateAsync(
        string baseCurrency,
        string quoteCurrency,
        DateTime transactionDate,
        CancellationToken ct = default);
}

/// <summary>
/// Result of a date-aware price/rate lookup.
/// <see cref="EffectiveDate"/> may differ from the transaction date when
/// fallback was applied.
/// </summary>
public sealed record PriceLookupResult(
    decimal Value,
    DateTime EffectiveDate,
    bool FallbackApplied);

public sealed record ContractPriceResult(
    decimal? FinalUnitPrice,
    decimal? BasePlattsPrice,
    decimal? PremiumDiscountUsd,
    string FormulaText,
    bool NeedsReview,
    string Reason,
    bool FallbackApplied);
