using NgoFund.Contracts.Documents;

namespace NgoFund.Contracts.Applications;

/// <summary>One document slot's status, satisfied or not — the wizard's upload step and the
/// manage-view checklist both render from this list rather than re-deriving it. <see cref="DefaultDocumentType"/>
/// is the first accepted type, so an upload control knows what type to tag a file with without
/// re-encoding the manifest client-side. <see cref="SatisfiedByApplicantProfile"/> is true when an
/// Applicant-scoped slot (v1.4) was satisfied by a document already on the applicant's profile — the
/// UI should render "already on file from the applicant's profile" instead of an upload prompt.</summary>
public record DocumentSlotStatusDto(
    string SlotKey,
    string Label,
    bool IsRequired,
    int MinCount,
    string OwnerScope,
    IReadOnlyList<string> AcceptedTypes,
    string DefaultDocumentType,
    Guid? GuarantorId,
    int? GuarantorSequenceNumber,
    string? GuarantorName,
    IReadOnlyList<DocumentDto> Documents,
    bool IsSatisfied,
    bool SatisfiedByApplicantProfile);
