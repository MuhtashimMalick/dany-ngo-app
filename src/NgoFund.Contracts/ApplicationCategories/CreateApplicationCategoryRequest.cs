namespace NgoFund.Contracts.ApplicationCategories;

public record CreateApplicationCategoryRequest(string Code, string Name, string FundEligibility, decimal? DefaultMaxAmount, int DisplayOrder);
