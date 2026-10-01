namespace NgoFund.Domain.Entities;

/// <summary>
/// Category-specific fields for a HEALTH application. Shares its primary key with
/// <see cref="FundApplication"/> (true 1:1), same pattern as <see cref="HousingApplicationDetails"/>.
/// Deliberately a single column — follows the Housing <c>ApplicantAge</c> precedent and keeps the
/// one-details-table-per-category pattern rather than folding this one field onto <c>FundApplication</c> itself.
/// </summary>
public class HealthApplicationDetails
{
    public Guid ApplicationId { get; set; }
    public FundApplication Application { get; set; } = null!;

    public int? ApplicantAge { get; set; }
}
