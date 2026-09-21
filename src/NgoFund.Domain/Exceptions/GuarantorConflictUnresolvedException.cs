namespace NgoFund.Domain.Exceptions;

/// <summary>
/// Raised at the Approved-transition call site (item 4, 2026-09 feedback) when this application has
/// a guarantor whose CNIC also appears on another application that is currently active (see
/// <see cref="Applications.ApplicationStatusRules.ActiveStatuses"/>) and nobody has approved a
/// conflict override for that guarantor row (<c>application_guarantors.conflict_override_approved_at</c>
/// is null). Resolved either by approving the override
/// (<c>POST .../guarantors/{guarantorId}/conflict-override</c>) or by removing/replacing the
/// conflicting guarantor.
/// </summary>
public sealed class GuarantorConflictUnresolvedException(string applicationNumber, int unresolvedGuarantorCount)
    : DomainException(
        $"Application {applicationNumber} has {unresolvedGuarantorCount} guarantor(s) whose CNIC also appears on another active application, with no approved conflict override on file.");
