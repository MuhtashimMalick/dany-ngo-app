namespace NgoFund.Contracts.Applications;

/// <summary>Mirror direction of item 4's guarantor-row override: staff explicitly acknowledging that
/// this application's own applicant is a guarantor on another active application and approving
/// proceeding anyway. Posted to
/// <c>POST /api/applications/{id}/applicant-guarantor-conflict-override</c>. Kept distinct from
/// <see cref="ApproveGuarantorConflictOverrideRequest"/> even though the shape is identical — this
/// one is application-level, not guarantor-row-level.</summary>
public record ApproveApplicantGuarantorConflictOverrideRequest(string Reason);
