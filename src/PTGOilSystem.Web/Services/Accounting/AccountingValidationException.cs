namespace PTGOilSystem.Web.Services.Accounting;

public sealed class AccountingValidationException : InvalidOperationException
{
    public AccountingValidationException(string code, string message)
        : base(message)
        => Code = code;

    public string Code { get; }
}
