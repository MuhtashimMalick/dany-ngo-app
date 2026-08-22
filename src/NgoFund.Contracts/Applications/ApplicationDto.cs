namespace NgoFund.Contracts.Applications;

public record ApplicationDto(
    Guid Id,
    string ApplicationNumber,
    Guid ApplicantId,
    string ApplicantName,
    string ApplicantCnic,
    Guid ApplicationCategoryId,
    string ApplicationCategoryName,
    /// <summary>Stable, never-edited category code (e.g. "ROZGAR") — the Desktop must switch on
    /// this, never on <see cref="ApplicationCategoryName"/>, which staff can rename.</summary>
    string ApplicationCategoryCode,
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
    IReadOnlyList<string> AllowedNextStatuses,
    bool RequiresLoanPlan,
    Guid? LoanAgreementId,
    decimal? DeclaredMonthlyIncome,
    int? DeclaredHouseholdSize,
    int? DeclaredEarningMembers,
    string? DeclaredResidentialAddress,
    string? DeclaredBusinessAddress,
    string? DeclaredHouseStatus,
    string IntakeChannel,
    string? ExternalFormReference,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? DeclarationAcceptedAt,
    DateTimeOffset? TermsAcceptedAt,
    string? TermsVersion,
    /// <summary>How many <see cref="ApplicationGuarantorDto"/> rows this category requires before
    /// Approved (0 for every category except ROZGAR) — lets the Desktop show "1 of 2 guarantors"
    /// without a second round trip to application-categories.</summary>
    int RequiresGuarantors,
    int GuarantorCount,
    /// <summary>
    /// True when this application's category is Zakat-eligible but its currently-selected fund is
    /// the General (non-Zakat) fund — the signal the Desktop needs to show the blocking
    /// confirmation dialog before letting staff save that combination. Distinct from
    /// <see cref="RequiresLoanPlan"/>: that flag is about whether a loan agreement is needed before
    /// payments, this one is purely "is this an unusual Zakat-eligible-on-General choice".
    /// </summary>
    bool ZakatEligibleCategoryOnGeneralFund);
