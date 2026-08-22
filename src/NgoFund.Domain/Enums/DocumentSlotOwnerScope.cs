namespace NgoFund.Domain.Enums;

/// <summary>Whose documents a <see cref="Applications.RequiredDocumentSlot"/> is satisfied by:
/// the application itself, (ROZGAR only) each guarantor row individually, or the applicant's own
/// profile documents (v1.4: the applicant's CNIC/membership-card scans satisfy the equivalent
/// per-application slot so staff don't re-upload them on every application — see
/// <see cref="Applications.ApplicationCompletenessEvaluator.EvaluateSlots"/>).</summary>
public enum DocumentSlotOwnerScope
{
    Application,
    Guarantor,
    Applicant
}
