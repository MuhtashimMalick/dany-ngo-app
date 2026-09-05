namespace NgoFund.Contracts.ApplicationCategories;

public record ApplicationCategoryDto(
    Guid Id,
    string Code,
    string Name,
    string FundEligibility,
    decimal? DefaultMaxAmount,
    bool IsActive,
    int DisplayOrder,
    int RequiresGuarantors,
    string? TermsText,
    string? TermsVersion);
