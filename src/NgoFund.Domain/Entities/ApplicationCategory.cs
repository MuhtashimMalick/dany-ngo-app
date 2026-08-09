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

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
