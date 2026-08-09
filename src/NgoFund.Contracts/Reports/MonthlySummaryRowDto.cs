namespace NgoFund.Contracts.Reports;

public record MonthlySummaryRowDto(int Year, int Month, Guid FundCategoryId, string FundCategoryName, decimal Total);
