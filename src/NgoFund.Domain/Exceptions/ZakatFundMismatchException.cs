namespace NgoFund.Domain.Exceptions;

/// <summary>
/// Raised when an application's application-category/fund-category pairing violates the Zakat
/// rule: a non-Zakat-eligible category may only draw from a non-Zakat (General) fund. Mirrored by
/// the <c>fn_enforce_zakat_eligibility</c> DB trigger as defence in depth.
/// </summary>
public sealed class ZakatFundMismatchException(string applicationCategoryName, string fundCategoryName)
    : DomainException(
        $"'{applicationCategoryName}' is not Zakat-eligible and cannot be funded from '{fundCategoryName}', which is a Zakat fund.");
