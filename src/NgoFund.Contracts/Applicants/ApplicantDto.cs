namespace NgoFund.Contracts.Applicants;

public record ApplicantDto(
    Guid Id,
    string? MembershipNumber,
    string FullName,
    string? FatherOrHusbandName,
    string Cnic,
    /// <summary>Nullable because no Google Form asks for gender — an intake-created applicant has
    /// it null until staff fill it in. Staff Create/Update flows still require it.</summary>
    string? Gender,
    DateOnly? DateOfBirth,
    string? MaritalStatus,
    string? Phone,
    string? AlternatePhone,
    string? Email,
    string? Address,
    string? City,
    string? District,
    string? Province,
    string? Occupation,
    decimal? MonthlyIncome,
    int? DependentsCount,
    int? HouseholdSize,
    Guid? PhotoDocumentId,
    bool IsBlacklisted,
    string? BlacklistReason,
    string? Notes,
    string? GrandfatherName,
    string? Surname,
    string? AncestralVillage,
    string? FatherMembershipNumber,
    string? WhatsappNumber);
