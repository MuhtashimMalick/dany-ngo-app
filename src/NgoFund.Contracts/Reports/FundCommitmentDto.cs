namespace NgoFund.Contracts.Reports;

public record FundCommitmentDto(Guid FundCategoryId, string FundName, decimal ApprovedOutstanding);
