namespace NgoFund.Contracts.Reports;

public record CategoryBreakdownDto(string CategoryName, int Count, decimal RequestedTotal, decimal ApprovedTotal);
