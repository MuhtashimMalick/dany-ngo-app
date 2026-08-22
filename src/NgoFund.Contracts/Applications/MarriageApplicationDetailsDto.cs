namespace NgoFund.Contracts.Applications;

public record MarriageApplicationDetailsDto(
    Guid ApplicationId,
    string? GuardianRelationshipToBride,
    string BrideName,
    string? BrideFatherName,
    string? BrideFamilyName,
    string? BrideCnic,
    string? BrideMaritalStatus,
    string? BridePreviousHusbandName,
    string? BrideJamaat,
    string? BridePriorTrustAssistance,
    string GroomName,
    string? GroomFatherName,
    string? GroomGrandfatherName,
    string? GroomJamaat,
    string? GroomMaritalStatus,
    string? GroomPreviousWifeName,
    string? GroomAddress,
    string? GroomMobile,
    string? GroomBusinessAddress,
    DateOnly? NikahDate,
    DateOnly? RukhsatiDate);
