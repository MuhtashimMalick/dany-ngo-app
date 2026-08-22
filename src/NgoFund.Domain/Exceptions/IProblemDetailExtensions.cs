namespace NgoFund.Domain.Exceptions;

/// <summary>Opt-in for a <see cref="DomainException"/> that wants extra machine-readable fields on
/// the RFC-9457 <c>ProblemDetails</c> response (e.g. structured lists, not just <c>Detail</c> text).
/// The API's <c>DomainExceptionHandler</c> copies these onto <c>ProblemDetails.Extensions</c>
/// generically — implementing this interface is the only wiring a new exception needs, no switch
/// arm to add anywhere (Open/Closed, per ).</summary>
public interface IProblemDetailExtensions
{
    IReadOnlyDictionary<string, object?> GetProblemDetailExtensions();
}
