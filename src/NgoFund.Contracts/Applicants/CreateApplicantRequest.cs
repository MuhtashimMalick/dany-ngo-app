using NgoFund.Contracts.Documents;

namespace NgoFund.Contracts.Applicants;

public record CreateApplicantRequest(
    string? MembershipNumber,
    string FullName,
    string? FatherOrHusbandName,
    string Cnic,
    string Gender,
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
    string? Notes,
    string? GrandfatherName = null,
    string? Surname = null,
    string? AncestralVillage = null,
    string? FatherMembershipNumber = null,
    string? WhatsappNumber = null,
    // Inline uploads committed atomically with the new applicant row in the same DB transaction
    // (see ApplicantService.CreateAsync) — a CNIC/membership-card scan cannot exist before the
    // applicant it belongs to. CnicFront, CnicBack, and MembershipCard are required by
    // CreateApplicantRequestValidator; Photo stays optional. Not present on
    // UpdateApplicantRequest — editing an existing applicant is deliberately not gated by this rule.
    InlineFileUpload? CnicFront = null,
    InlineFileUpload? CnicBack = null,
    InlineFileUpload? MembershipCard = null,
    InlineFileUpload? Photo = null);
