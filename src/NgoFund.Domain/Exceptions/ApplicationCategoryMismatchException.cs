namespace NgoFund.Domain.Exceptions;

/// <summary>
/// Raised when a category-specific details payload (housing/marriage/business-loan) is posted
/// against an application whose <c>ApplicationCategory.Code</c> doesn't match the endpoint —
/// e.g. posting to <c>.../details/housing</c> for a SHAADI application.
/// </summary>
public sealed class ApplicationCategoryMismatchException(string applicationNumber, string expectedCategoryCode, string actualCategoryCode)
    : DomainException($"Application {applicationNumber} is category '{actualCategoryCode}', not '{expectedCategoryCode}'.");
