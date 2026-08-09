using NgoFund.Domain.Common;
using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Entities;

public class Donor : BaseEntity, IUpdateAuditable, ISoftDeletable
{
    /// <summary>Human-readable unique code, e.g. "DNR-2026-00001", from <see cref="NumberSequence"/>.</summary>
    public string DonorCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public DonorType DonorType { get; set; } = DonorType.Individual;

    public string? Cnic { get; set; }

    public string? Ntn { get; set; }

    public string? MembershipNumber { get; set; }

    public string? Phone { get; set; }

    public string? AlternatePhone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public bool IsAnonymous { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public ICollection<Donation> Donations { get; set; } = [];

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
