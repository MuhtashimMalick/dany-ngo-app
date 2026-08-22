namespace NgoFund.Domain.Exceptions;

/// <summary>
/// Raised at the Approved-transition call site when a category requires guarantors
/// (<c>ApplicationCategory.RequiresGuarantors</c>, 2 for ROZGAR) and fewer than that many are on
/// file for the application. Data-driven, not hardcoded to ROZGAR — any category could opt in by
/// setting <c>RequiresGuarantors</c> &gt; 0.
/// </summary>
public sealed class GuarantorsRequiredException(string applicationNumber, int required, int actual)
    : DomainException($"Application {applicationNumber} requires {required} guarantor(s) before it can be Approved; {actual} on file.");
