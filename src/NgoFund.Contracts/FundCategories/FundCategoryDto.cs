namespace NgoFund.Contracts.FundCategories;

public record FundCategoryDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsZakat,
    bool IsActive,
    int DisplayOrder);
