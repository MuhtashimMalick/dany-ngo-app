using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Entities;

/// <summary>
/// Category-specific fields for a SHAADI application, re-keyed from the Marriage Google Form.
/// Shares its primary key with <see cref="FundApplication"/> (true 1:1). Bride/groom marital
/// status reuses the existing <see cref="Enums.MaritalStatus"/> enum (First Marriage -> Single,
/// Widow -> Widowed, Divorced -> Divorced) rather than a second, form-specific enum.
/// </summary>
public class MarriageApplicationDetails
{
    public Guid ApplicationId { get; set; }
    public FundApplication Application { get; set; } = null!;

    public string? GuardianRelationshipToBride { get; set; }

    public string BrideName { get; set; } = null!;
    public string? BrideFatherName { get; set; }
    public string? BrideFamilyName { get; set; }
    public string? BrideCnic { get; set; }
    public MaritalStatus? BrideMaritalStatus { get; set; }
    public string? BridePreviousHusbandName { get; set; }
    public string? BrideJamaat { get; set; }
    public string? BridePriorTrustAssistance { get; set; }

    public string GroomName { get; set; } = null!;
    public string? GroomFatherName { get; set; }
    public string? GroomGrandfatherName { get; set; }
    /// <summary>Covers both "Groom's Jamaat/Community" and "Groom's Jamaat" from the form transcription.</summary>
    public string? GroomJamaat { get; set; }
    public MaritalStatus? GroomMaritalStatus { get; set; }
    public string? GroomPreviousWifeName { get; set; }
    public string? GroomAddress { get; set; }
    public string? GroomMobile { get; set; }
    public string? GroomBusinessAddress { get; set; }

    public DateOnly? NikahDate { get; set; }
    public DateOnly? RukhsatiDate { get; set; }
}
