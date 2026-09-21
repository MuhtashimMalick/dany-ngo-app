using NgoFund.Domain.Common;

namespace NgoFund.Domain.Entities;

/// <summary>
/// One guarantor on a Business Loan (ROZGAR) application, re-keyed from the Google Form. A ROZGAR
/// application needs exactly <see cref="ApplicationCategory.RequiresGuarantors"/> of these before
/// it can reach <see cref="Enums.ApplicationStatus.Approved"/> — see
/// <see cref="FundApplication"/>'s Approved-transition call site. Soft-deletable (unlike the
/// details tables) since a guarantor can be individually removed/replaced without touching the
/// rest of the application.
/// </summary>
public class ApplicationGuarantor : BaseEntity, IUpdateAuditable, ISoftDeletable
{
    public Guid ApplicationId { get; set; }
    public FundApplication Application { get; set; } = null!;

    public int SequenceNumber { get; set; }

    public string? MembershipNumber { get; set; }
    public string FullName { get; set; } = null!;
    public string? FatherName { get; set; }
    public string? GrandfatherName { get; set; }
    public string? Surname { get; set; }
    public string? Cnic { get; set; }

    public string? ResidentialAddress { get; set; }
    public string? BusinessAddress { get; set; }
    public string? BusinessNature { get; set; }

    public string? PhoneHome { get; set; }
    public string? PhoneOffice { get; set; }
    public string? PhoneMobile { get; set; }

    public DateTimeOffset? DeclarationAcceptedAt { get; set; }

    // --- CNIC-conflict override (item 4, 2026-09 feedback) ---
    //
    // A guarantor's CNIC conflicting with another currently-active application is detected
    // read-side (see GuarantorConflictLookup), never stored — these three columns record only
    // whether staff have explicitly approved proceeding despite that conflict. "Approved" is
    // derived from ConflictOverrideApprovedAt being non-null; there is no separate status/enum
    // column. Whenever Cnic is edited to a different value, all three must be cleared (see
    // ApplicationDetailsService.ReplaceGuarantorsAsync) — an override approved for one person must
    // never silently carry over to a different person typed into the same row.
    public DateTimeOffset? ConflictOverrideApprovedAt { get; set; }
    public Guid? ConflictOverrideApprovedBy { get; set; }
    public string? ConflictOverrideReason { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
