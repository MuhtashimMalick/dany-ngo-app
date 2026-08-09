namespace NgoFund.Contracts.FundCategories;

public record CreateFundCategoryRequest(string Code, string Name, string? Description, bool IsZakat, int DisplayOrder);
