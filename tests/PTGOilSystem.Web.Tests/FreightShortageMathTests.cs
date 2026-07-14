using PTGOilSystem.Web.Services;
using Xunit;

namespace PTGOilSystem.Web.Tests;

// این تست‌ها خروجی عددیِ فرمول‌های استخراج‌شده به FreightShortageMath را قفل می‌کنند
// تا تضمین شود رفتار مالیِ قبل و بعد از refactor یکسان مانده است.
public class FreightShortageMathTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(10, 5, 50)]
    [InlineData(2.5, 4, 10)]
    public void GrossFreightUsd_Matches_Expected(decimal quantity, decimal rate, decimal expected)
    {
        Assert.Equal(expected, FreightShortageMath.GrossFreightUsd(quantity, rate));
    }

    [Fact]
    public void GrossFreightUsd_Rounds_To_Four_Decimals_AwayFromZero()
    {
        // ۱.۲۳۴۵۵ × ۱ ⇒ رقم پنجم ۵ ⇒ رو به بالا ⇒ ۱.۲۳۴۶
        Assert.Equal(1.2346m, FreightShortageMath.GrossFreightUsd(1.23455m, 1m));
        // ۱.۲۳۴۵۴ × ۱ ⇒ رقم پنجم ۴ ⇒ رو به پایین ⇒ ۱.۲۳۴۵
        Assert.Equal(1.2345m, FreightShortageMath.GrossFreightUsd(1.23454m, 1m));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(5, 2, 3)]
    [InlineData(2, 5, 0)]       // کسری کمتر از تلورانس ⇒ صفر (منفی نمی‌شود)
    [InlineData(2, 2, 0)]       // برابر ⇒ صفر
    [InlineData(1.2345, 0.2345, 1.0)]
    public void ChargeableShortage_Matches_Expected(decimal shortage, decimal allowance, decimal expected)
    {
        Assert.Equal(expected, FreightShortageMath.ChargeableShortage(shortage, allowance));
    }

    [Theory]
    [InlineData(3, 10, 30)]
    [InlineData(0, 10, 0)]      // کسری صفر ⇒ صفر
    [InlineData(3, 0, 0)]       // نرخ صفر ⇒ صفر
    [InlineData(3, -5, 0)]      // نرخ منفی ⇒ صفر
    public void ShortageChargeUsd_Matches_Expected(decimal chargeable, decimal rate, decimal expected)
    {
        Assert.Equal(expected, FreightShortageMath.ShortageChargeUsd(chargeable, rate));
    }

    [Fact]
    public void ShortageChargeUsd_Null_Rate_Is_Zero()
    {
        Assert.Equal(0m, FreightShortageMath.ShortageChargeUsd(3m, null));
    }

    [Fact]
    public void FreightPayable_Null_When_No_Freight_Cost()
    {
        Assert.Null(FreightShortageMath.FreightPayableUsd(null, 5m, 1m, 10m));
    }

    [Fact]
    public void FreightPayable_Returns_Gross_When_No_Shortage_Rate()
    {
        Assert.Equal(100m, FreightShortageMath.FreightPayableUsd(100m, 5m, 1m, null));
        Assert.Equal(100m, FreightShortageMath.FreightPayableUsd(100m, 5m, 1m, 0m));
    }

    [Fact]
    public void FreightPayable_Deducts_From_Shortage_Minus_Tolerance()
    {
        // chargeable = max(0, 5 - 1) = 4 ⇒ deduction = 4 × 10 = 40 ⇒ 100 - 40 = 60
        Assert.Equal(60m, FreightShortageMath.FreightPayableUsd(100m, 5m, 1m, 10m));
    }

    [Fact]
    public void FreightPayable_Uses_Tolerance_Over_Allowance_When_Both_Present()
    {
        // effectiveTolerance = toleranceMt (2) نه allowanceMt (1) ⇒ chargeable = 5 - 2 = 3 ⇒ 100 - 30 = 70
        Assert.Equal(70m, FreightShortageMath.FreightPayableUsd(100m, 5m, 1m, 10m, toleranceMt: 2m));
    }

    [Fact]
    public void FreightPayable_Uses_Chargeable_Override_When_Provided()
    {
        // override = 1.5 ⇒ deduction = 1.5 × 10 = 15 ⇒ 100 - 15 = 85 (بدون توجه به shortage/tolerance)
        Assert.Equal(85m, FreightShortageMath.FreightPayableUsd(100m, 5m, 1m, 10m, toleranceMt: 2m, chargeableShortageMtOverride: 1.5m));
    }

    [Fact]
    public void FreightPayable_Override_Negative_Clamped_To_Zero()
    {
        // override منفی ⇒ chargeable = 0 ⇒ deduction = 0 ⇒ خودِ کرایه
        Assert.Equal(100m, FreightShortageMath.FreightPayableUsd(100m, 5m, 1m, 10m, chargeableShortageMtOverride: -3m));
    }
}
