using NgoFund.Domain.Common;
using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Entities;

/// <summary>
/// Metadata for an uploaded/captured file (webcam photo, CNIC scan, supporting document, donor
/// receipt, payment proof, guarantor CNIC, ...). The file bytes live on the API's storage volume
/// at <c>/app/storage/{storage_key}</c>, never in the database. Exactly one of the five owner FKs
/// is set — enforced by a DB CHECK constraint, not a polymorphic FK.
/// </summary>
public class Document : BaseEntity
{
    public string FileName { get; set; } = null!;

    /// <summary>UUID-named path on the storage volume — never trusts the original file name.</summary>
    public string StorageKey { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }

    public string Sha256 { get; set; } = null!;

    public DocumentType DocumentType { get; set; }

    public Guid? ApplicantId { get; set; }
    public Applicant? Applicant { get; set; }

    public Guid? ApplicationId { get; set; }
    public FundApplication? Application { get; set; }

    public Guid? DonationId { get; set; }
    public Donation? Donation { get; set; }

    public Guid? PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public Guid? ApplicationGuarantorId { get; set; }
    public ApplicationGuarantor? ApplicationGuarantor { get; set; }

    public string? Description { get; set; }

    /// <summary>Which <see cref="Applications.RequiredDocumentSlot"/> (by
    /// <see cref="Applications.RequiredDocumentSlot.SlotKey"/>) this upload satisfies, e.g.
    /// "SHAADI.BRIDE_CNIC_OR_BFORM" — disambiguates same-<see cref="DocumentType"/> uploads that
    /// mean different things (Applicant's CNIC vs. Bride's CNIC vs. Groom's CNIC are all
    /// <see cref="Enums.DocumentType.CnicFront"/>). Null satisfies no slot at all (fail-closed) —
    /// documents uploaded before this column existed correctly read as not counting toward
    /// completeness.</summary>
    public string? SlotKey { get; set; }

    public DateTimeOffset UploadedAt { get; set; }
    public Guid? UploadedBy { get; set; }
}
