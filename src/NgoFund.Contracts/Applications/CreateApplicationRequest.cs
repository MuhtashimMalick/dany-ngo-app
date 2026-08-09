namespace NgoFund.Contracts.Applications;

public record CreateApplicationRequest(
    Guid ApplicantId,
    Guid ApplicationCategoryId,
    Guid FundCategoryId,
    decimal RequestedAmount,
    string Priority,
    DateOnly ApplicationDate,
    string? Purpose);
