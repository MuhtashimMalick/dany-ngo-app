namespace NgoFund.Contracts.ApplicationCategories;

public record UpdateApplicationCategoryRequest(string Name, decimal? DefaultMaxAmount, bool IsActive, int DisplayOrder);
