namespace PTGOilSystem.Web.Models.Entities;

public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5
}

public enum NormalBalance
{
    Debit = 1,
    Credit = 2
}

public enum JournalEntryStatus
{
    Draft = 0,
    Posted = 1
}

public enum FiscalYearStatus
{
    Draft = 0,
    Open = 1,
    Closing = 2,
    Closed = 3
}

public enum FiscalPeriodStatus
{
    Open = 1,
    Closed = 2
}

public enum FiscalYearCloseRunStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3
}

public enum AccountingPartyType
{
    Customer = 1,
    Supplier = 2,
    ServiceProvider = 3,
    Sarraf = 4,
    Driver = 5,
    Employee = 6,
    Partner = 7
}
