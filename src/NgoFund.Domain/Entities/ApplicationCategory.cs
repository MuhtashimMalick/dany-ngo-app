using NgoFund.Domain.Common;

namespace NgoFund.Domain.Entities;

/// <summary>
/// A category of needy-people application (Shaadi, Health, Education, ...). Drives the Zakat
/// rule together with <see cref="FundCategory.IsZakat"/>.
/// </summary>
public class ApplicationCategory : BaseEntity, IUpdateAuditable, ISoftDeletable
{
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    /// <summary>
    /// If true, this category may be paid from either a Zakat or a General fund. If false, it
    /// may only be paid from a non-Zakat (General) fund. Enforced by the domain layer and
    /// mirrored by the <c>fn_enforce_zakat_eligibility</c> DB trigger.
    /// </summary>
    public bool IsZakatEligible { get; set; }

    public decimal? DefaultMaxAmount { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    /// <summary>
    /// How many <see cref="ApplicationGuarantor"/> rows an application in this category needs
    /// before it can reach <see cref="Enums.ApplicationStatus.Approved"/>. Data-driven: 0 means no
    /// gate at all (every category except ROZGAR, seeded at 2). See
    /// <see cref="FundApplication"/>'s Approved-transition call site.
    /// </summary>
    public int RequiresGuarantors { get; set; }

    /// <summary>Terms &amp; conditions text shown before an applicant/staff accepts on this category's application. Placeholder until the client supplies real copy — see docs/schema.md.</summary>
    public string? TermsText { get; set; }

    public string? TermsVersion { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
