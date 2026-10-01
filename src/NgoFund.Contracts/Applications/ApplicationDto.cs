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
    /// <summary>Nullable because Google Form intake only asks for an amount on the ROZGAR form —
    /// every other category's form has no amount field. The staff Create/Update validators still
    /// require it &gt; 0 for in-app data entry; <c>ApplicationCompletenessEvaluator.MissingBaseFields</c>
    /// blocks Approved until it's filled in.</summary>
    decimal? RequestedAmount,
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
    /// True when this application's category is dual-eligible (<c>FundEligibility.Either</c> —
    /// under the current mapping, only OTHER) but its currently-selected fund is the General
    /// (non-Zakat) fund — the signal the Desktop needs to show the blocking confirmation dialog
    /// before letting staff save that combination, since it makes this a repayable loan rather
    /// than a grant. Distinct from <see cref="RequiresLoanPlan"/>: that flag is about whether a
    /// loan agreement is needed before payments, this one is purely "did the operator have a
    /// genuine fund choice and pick General". Never true for a <c>GeneralOnly</c> category
    /// (e.g. ROZGAR), where General is the only legal choice, not an unusual one.
    /// </summary>
    bool DualEligibleCategoryOnGeneralFund,
    /// <summary>Item 3 (2026-09 feedback), the duplicate-active-application badge: how many
    /// applications this SAME applicant has whose status is "active" (Pending/UnderReview/Approved/
    /// PartiallyPaid/OnHold — i.e. not Rejected/Paid), INCLUDING this one when it is itself active —
    /// so a newly created application that is the applicant's second active one already shows 2,
    /// not 1.</summary>
    int ActiveApplicationCount,
    /// <summary>Mirror direction of item 4's guarantor-conflict feature: application numbers of
    /// OTHER, currently-active applications on whose guarantor list THIS application's own
    /// applicant's CNIC appears — computed read-side, never persisted. Empty when there is no such
    /// conflict.</summary>
    IReadOnlyList<string> ApplicantGuarantorConflictApplicationNumbers,
    DateTimeOffset? ApplicantGuarantorConflictOverrideApprovedAt,
    string? ApplicantGuarantorConflictOverrideApprovedByName,
    string? ApplicantGuarantorConflictOverrideReason);
