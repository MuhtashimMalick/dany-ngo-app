namespace NgoFund.Domain.Exceptions;

/// <summary>
/// Raised at the Approved-transition call site (mirror of item 4, 2026-09 feedback) when this
/// application's own applicant is a guarantor on another application that is currently active (see
/// <see cref="Applications.ApplicationStatusRules.ActiveStatuses"/>) and nobody has approved an
/// applicant-guarantor conflict override for this application
/// (<c>applications.applicant_guarantor_conflict_override_approved_at</c> is null, or was approved
/// for a since-edited CNIC). Resolved either by approving the override
/// (<c>POST .../applicant-guarantor-conflict-override</c>) or by removing/replacing the applicant's
/// guarantor row on the other application.
/// </summary>
public sealed class ApplicantIsActiveGuarantorElsewhereException(string applicationNumber, IReadOnlyCollection<string> conflictingApplicationNumbers)
    : DomainException(
        $"Application {applicationNumber}'s applicant is a guarantor on another currently active application ({string.Join(", ", conflictingApplicationNumbers)}), with no approved conflict override on file.");
