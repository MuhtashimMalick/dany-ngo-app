namespace NgoFund.Contracts.Applications;

public record UpdateApplicationRequest(
    Guid ApplicationCategoryId,
    Guid FundCategoryId,
    decimal RequestedAmount,
    decimal? ApprovedAmount,
    string Priority,
    string? Purpose,
    decimal? DeclaredMonthlyIncome = null,
    int? DeclaredHouseholdSize = null,
    int? DeclaredEarningMembers = null,
    string? DeclaredResidentialAddress = null,
    string? DeclaredBusinessAddress = null,
    string? DeclaredHouseStatus = null,
    DateTimeOffset? DeclarationAcceptedAt = null,
    DateTimeOffset? TermsAcceptedAt = null,
    string? TermsVersion = null);
