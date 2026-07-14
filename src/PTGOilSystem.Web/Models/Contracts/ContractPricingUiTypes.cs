namespace PTGOilSystem.Web.Models.Contracts;

public enum UiPricingType
{
    Agreed = 1,
    Platts = 2
}

public enum PlattsUiMode
{
    Daily = 1,
    MonthlyAverage = 2,
    ManualDescriptive = 3
}

public enum PricingCompletionStatus
{
    Pending = 0,
    Completed = 1
}
