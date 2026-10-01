namespace NgoFund.Contracts.Applications;

public record CreateApplicationRequest(
    Guid ApplicantId,
    Guid ApplicationCategoryId,
    Guid FundCategoryId,
    /// <summary>Nullable so Google Form intake (which only collects an amount for ROZGAR) can
    /// create an application without one — <see cref="CreateApplicationRequestValidator"/> still
    /// requires it &gt; 0 for staff/in-app submissions.</summary>
    decimal? RequestedAmount,
    string Priority,
    DateOnly ApplicationDate,
    string? Purpose,
    decimal? DeclaredMonthlyIncome = null,
    int? DeclaredHouseholdSize = null,
    int? DeclaredEarningMembers = null,
    string? DeclaredResidentialAddress = null,
    string? DeclaredBusinessAddress = null,
    string? DeclaredHouseStatus = null,
    string IntakeChannel = "InApp",
    string? ExternalFormReference = null,
    DateTimeOffset? SubmittedAt = null,
    DateTimeOffset? DeclarationAcceptedAt = null,
    DateTimeOffset? TermsAcceptedAt = null,
    string? TermsVersion = null);
