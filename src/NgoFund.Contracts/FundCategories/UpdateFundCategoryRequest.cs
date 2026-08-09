namespace NgoFund.Contracts.FundCategories;

public record UpdateFundCategoryRequest(string Name, string? Description, bool IsActive, int DisplayOrder);
