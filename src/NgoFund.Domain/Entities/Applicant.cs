using NgoFund.Domain.Common;
using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Entities;

/// <summary>
/// A needy person. Kept separate from <see cref="FundApplication"/> so one person can file
/// multiple applications over time and CNIC/membership-number search works across their whole
/// history.
/// </summary>
public class Applicant : BaseEntity, IUpdateAuditable, ISoftDeletable
{
    public string? MembershipNumber { get; set; }

    public string FullName { get; set; } = null!;

    public string? FatherOrHusbandName { get; set; }

    public string? GrandfatherName { get; set; }

    public string? Surname { get; set; }

    public string? AncestralVillage { get; set; }

    /// <summary>Membership number of the applicant's father, as re-keyed from paper/Google Form intake — distinct from the applicant's own <see cref="MembershipNumber"/>.</summary>
    public string? FatherMembershipNumber { get; set; }

    public string? WhatsappNumber { get; set; }

    public string Cnic { get; set; } = null!;

    /// <summary>Nullable because no Google Form asks for gender — an intake-created applicant has
    /// it null until staff fill it in. Staff Create/Update requests keep requiring it.</summary>
    public Gender? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public MaritalStatus? MaritalStatus { get; set; }

    public string? Phone { get; set; }

    public string? AlternatePhone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? District { get; set; }

    public string? Province { get; set; }

    public string? Occupation { get; set; }

    public decimal? MonthlyIncome { get; set; }

    public int? DependentsCount { get; set; }

    public int? HouseholdSize { get; set; }

    /// <summary>The applicant's profile photo, whether webcam-captured or uploaded from a file.</summary>
    public Guid? PhotoDocumentId { get; set; }
    public Document? PhotoDocument { get; set; }

    public bool IsBlacklisted { get; set; }

    public string? BlacklistReason { get; set; }

    public string? Notes { get; set; }

    public ICollection<FundApplication> Applications { get; set; } = [];

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
