using System;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services;

namespace PTGOilSystem.Web.Helpers;

/// <summary>
/// سطر دفتر قدیمی (Legacy) بابت بدهی تأمین‌کننده از یک بارگیریِ روبلیِ قفل‌شده.
/// هر بارگیری دقیقاً یک سطر دارد: کلید یکتا (SourceType، SourceId).
/// بعد از اصلاح قیمت یا بازقفل نرخ، همان سطر با مبلغ جدید هماهنگ می‌شود و سطر دوم ساخته نمی‌شود.
/// دفتر کل جدید از این مسیر عبور نمی‌کند و همچنان با Reversal + Revision کار می‌کند؛
/// این کلاس فقط سطر Legacy را از snapshot خود بارگیری بازتولید می‌کند.
/// </summary>
public static class SupplierLoadingLedger
{
    public const string SourceType = "Loading";

    /// <summary>
    /// مسیر قدیمی فقط برای بارگیری روبلیِ قفل‌شدهٔ یک قرارداد خرید سطر می‌سازد.
    /// همان شرطی که پیش از این داخل LoadingController بود.
    /// </summary>
    public static bool IsPostable(LoadingRegister loading, Contract? contract)
    {
        ArgumentNullException.ThrowIfNull(loading);

        return contract is not null
            && contract.ContractType == ContractType.Purchase
            && contract.SupplierId.HasValue
            && LoadingRubSettlement.IsRubSettlement(loading.SettlementCurrencyCode)
            && loading.RubRateStatus == RubSettlementRateStatus.Locked
            && loading.AmountUsdAtRubLock is > 0m
            && loading.AmountRubAtRubLock is > 0m
            && loading.RubPerUsdRate is > 0m;
    }

    public static string BuildReference(LoadingRegister loading)
    {
        ArgumentNullException.ThrowIfNull(loading);

        var reference = string.IsNullOrWhiteSpace(loading.BillOfLadingNumber)
            ? $"LOAD-{loading.Id}"
            : loading.BillOfLadingNumber.Trim();
        return reference.Length > 200 ? reference[..200] : reference;
    }

    public static LedgerEntry Create(LoadingRegister loading, Contract contract)
    {
        ArgumentNullException.ThrowIfNull(loading);
        ArgumentNullException.ThrowIfNull(contract);

        var entry = new LedgerEntry
        {
            Side = LedgerSide.Credit,
            Currency = SystemCurrency.BaseCurrencyCode,
            SourceCurrencyCode = "RUB",
            Description = $"بدهی تأمین‌کننده بابت بارگیری #{loading.Id}",
            SourceType = SourceType,
            SourceId = loading.Id,
            Reference = BuildReference(loading),
            ContractId = contract.Id,
            SupplierId = contract.SupplierId!.Value
        };

        ApplySnapshot(entry, loading);
        return entry;
    }

    /// <summary>
    /// مبلغ و نرخِ سطر موجود را با snapshot فعلی بارگیری هماهنگ می‌کند.
    /// فقط فیلدهایی که با اصلاح قیمت کهنه می‌شوند نوشته می‌شوند؛ هویت سطر (SourceType/SourceId/طرف حساب)
    /// دست‌نخورده می‌ماند. اگر چیزی عوض نشده باشد false برمی‌گرداند تا فراخوان لاگ بی‌مورد ثبت نکند.
    /// </summary>
    public static bool ApplySnapshot(LedgerEntry entry, LoadingRegister loading)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(loading);

        var entryDate = loading.LoadingDate.Date;
        var amountUsd = loading.AmountUsdAtRubLock!.Value;
        var amountRub = loading.AmountRubAtRubLock!.Value;
        var fxRateToUsd = decimal.Round(1m / loading.RubPerUsdRate!.Value, 6, MidpointRounding.AwayFromZero);
        var fxRateDate = loading.RubRateDate?.Date ?? loading.LoadingDate.Date;
        var fxRateSource = loading.RubRateSource ?? "Loading RUB settlement";

        var changed = entry.EntryDate != entryDate
            || entry.AmountUsd != amountUsd
            || entry.SourceAmount != amountRub
            || entry.AppliedFxRateToUsd != fxRateToUsd
            || entry.AppliedFxRateDate != fxRateDate
            || entry.AppliedFxRateSource != fxRateSource;

        entry.EntryDate = entryDate;
        entry.AmountUsd = amountUsd;
        entry.SourceAmount = amountRub;
        entry.AppliedFxRateToUsd = fxRateToUsd;
        entry.AppliedFxRateDate = fxRateDate;
        entry.AppliedFxRateSource = fxRateSource;

        return changed;
    }
}
