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
    DateTimeOffset? DeclarationAcceptedAt);
