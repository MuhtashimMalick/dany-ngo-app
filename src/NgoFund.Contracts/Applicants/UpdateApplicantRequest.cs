namespace NgoFund.Contracts.Applicants;

public record UpdateApplicantRequest(
    string? MembershipNumber,
    string FullName,
    string? FatherOrHusbandName,
    string Cnic,
    /// <summary>Optional — an intake-created applicant may still have no gender on file.
    /// <see cref="UpdateApplicantRequestValidator"/> only checks it's a valid enum value when present,
    /// so the blacklist/edit flow never breaks on a Google-Form applicant.</summary>
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
    bool IsBlacklisted,
    string? BlacklistReason,
    string? Notes,
    string? GrandfatherName = null,
    string? Surname = null,
    string? AncestralVillage = null,
    string? FatherMembershipNumber = null,
    string? WhatsappNumber = null);
