namespace NgoFund.Contracts.Applications;

public record UpdateApplicationRequest(
    Guid ApplicationCategoryId,
    Guid FundCategoryId,
    decimal RequestedAmount,
    decimal? ApprovedAmount,
    string Priority,
    string? Purpose);
