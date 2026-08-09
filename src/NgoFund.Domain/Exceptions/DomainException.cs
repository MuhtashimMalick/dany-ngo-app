namespace NgoFund.Domain.Exceptions;

/// <summary>
/// Base type for every business-rule violation raised by the domain layer. The API's single
/// <c>IExceptionHandler</c> maps these to RFC-9457 <c>ProblemDetails</c> — this is the only error
/// mechanism in the system, callers never invent a second error-response shape.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}
