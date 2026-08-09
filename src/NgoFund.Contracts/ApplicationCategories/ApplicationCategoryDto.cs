namespace NgoFund.Contracts.ApplicationCategories;

public record ApplicationCategoryDto(
    Guid Id,
    string Code,
    string Name,
    bool IsZakatEligible,
    decimal? DefaultMaxAmount,
    bool IsActive,
    int DisplayOrder);
