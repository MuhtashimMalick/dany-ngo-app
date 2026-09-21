namespace NgoFund.Contracts.Applications;

public record ApplicationGuarantorDto(
    Guid Id,
    Guid ApplicationId,
    int SequenceNumber,
    string? MembershipNumber,
    string FullName,
    string? FatherName,
    string? GrandfatherName,
    string? Surname,
    string? Cnic,
    string? ResidentialAddress,
    string? BusinessAddress,
    string? BusinessNature,
    string? PhoneHome,
    string? PhoneOffice,
    string? PhoneMobile,
    DateTimeOffset? DeclarationAcceptedAt,
    /// <summary>Item 4 (2026-09 feedback): application numbers of OTHER, currently-active
    /// applications whose guarantor list contains the same CNIC — computed read-side, never
    /// persisted. Empty when this guarantor has no CNIC or no conflict.</summary>
    IReadOnlyList<string> ConflictingApplicationNumbers,
    DateTimeOffset? ConflictOverrideApprovedAt,
    string? ConflictOverrideApprovedByName,
    string? ConflictOverrideReason);
