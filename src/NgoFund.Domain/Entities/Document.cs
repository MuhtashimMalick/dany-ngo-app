using NgoFund.Domain.Common;
using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Entities;

/// <summary>
/// Metadata for an uploaded/captured file (webcam photo, CNIC scan, supporting document, donor
/// receipt, payment proof). The file bytes live on the API's storage volume at
/// <c>/app/storage/{storage_key}</c>, never in the database. Exactly one of the four owner FKs
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

    public string? Description { get; set; }

    public DateTimeOffset UploadedAt { get; set; }
    public Guid? UploadedBy { get; set; }
}
