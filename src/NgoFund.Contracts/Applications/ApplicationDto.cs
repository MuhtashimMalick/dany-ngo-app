namespace NgoFund.Contracts.Applications;

public record ApplicationDto(
    Guid Id,
    string ApplicationNumber,
    Guid ApplicantId,
    string ApplicantName,
    string ApplicantCnic,
    Guid ApplicationCategoryId,
    string ApplicationCategoryName,
    Guid FundCategoryId,
    string FundCategoryName,
    decimal RequestedAmount,
    decimal? ApprovedAmount,
    string Status,
    string Priority,
    DateOnly ApplicationDate,
    string? Purpose,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset? ApprovedAt,
    string? RejectionReason,
    IReadOnlyList<string> AllowedNextStatuses);
