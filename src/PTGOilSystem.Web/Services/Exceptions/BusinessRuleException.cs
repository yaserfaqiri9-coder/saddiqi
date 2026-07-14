using System;

namespace PTGOilSystem.Web.Services.Exceptions;

/// <summary>
/// Thrown when a domain/business rule is violated (e.g. insufficient free stock,
/// missing contract on a contract-bound operation, no valid pricing reference).
///
/// The message is intended to be user-facing and persian-friendly. Controllers
/// should translate it into a 400 / ModelState error rather than swallowing it.
/// </summary>
public class BusinessRuleException : Exception
{
    public string Code { get; }

    public BusinessRuleException(string code, string message) : base(message)
    {
        Code = code;
    }

    public BusinessRuleException(string code, string message, Exception inner)
        : base(message, inner)
    {
        Code = code;
    }
}
