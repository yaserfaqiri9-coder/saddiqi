namespace PTGOilSystem.Web.Models.Payments;

public sealed class FinanceMetricCardsViewModel
{
    public string AriaLabel { get; init; } = "\u0622\u0645\u0627\u0631 \u0631\u0648\u0632\u0646\u0627\u0645\u0686\u0647 \u062f\u0631\u06cc\u0627\u0641\u062a \u0648 \u067e\u0631\u062f\u0627\u062e\u062a";
    public decimal TodayReceiptUsd { get; init; }
    public decimal TodayPaymentUsd { get; init; }
    public decimal CashAccountsBalanceUsd { get; init; }
    public int TransactionCount { get; init; }
}