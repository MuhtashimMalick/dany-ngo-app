namespace NgoFund.Contracts.ApplicationCategories;

public record CreateApplicationCategoryRequest(string Code, string Name, bool IsZakatEligible, decimal? DefaultMaxAmount, int DisplayOrder);
