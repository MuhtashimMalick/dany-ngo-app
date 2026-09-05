using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Exceptions;

/// <summary>
/// Raised when an application's application-category/fund-category pairing violates the Zakat
/// rule: a <see cref="FundEligibility.ZakatOnly"/> category may only draw from a Zakat fund, and
/// a <see cref="FundEligibility.GeneralOnly"/> category may only draw from a non-Zakat (General)
/// fund. Mirrored by the <c>fn_enforce_zakat_eligibility</c> DB trigger as defence in depth.
/// </summary>
public sealed class ZakatFundMismatchException(string applicationCategoryName, FundEligibility fundEligibility, string fundCategoryName)
    : DomainException(BuildMessage(applicationCategoryName, fundEligibility, fundCategoryName))
{
    private static string BuildMessage(string applicationCategoryName, FundEligibility fundEligibility, string fundCategoryName) =>
        fundEligibility == FundEligibility.ZakatOnly
            ? $"'{applicationCategoryName}' requires a Zakat fund and cannot be funded from '{fundCategoryName}', which is not a Zakat fund."
            : $"'{applicationCategoryName}' is not Zakat-eligible and cannot be funded from '{fundCategoryName}', which is a Zakat fund.";
}
