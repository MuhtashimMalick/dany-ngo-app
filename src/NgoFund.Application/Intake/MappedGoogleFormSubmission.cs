using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Intake;
using NgoFund.Domain.Enums;

namespace NgoFund.Application.Intake;

/// <summary>The applicant-level fields re-keyed from the form. <see cref="Cnic"/> is always a
/// normalized dashed CNIC by the time this is constructed — an unparseable applicant CNIC fails
/// the mapping outright (see <see cref="GoogleFormSubmissionMapper.Map"/>). Everything else is
/// exactly what the form gave, for <c>GoogleFormIntakeService</c> to either seed a brand-new
/// <c>CreateApplicantRequest</c> or fill blanks on an existing one.</summary>
public sealed record MappedApplicantFields(
    string Cnic,
    string FullName,
    string? Email,
    string? FatherOrHusbandName,
    string? GrandfatherName,
    string? Surname,
    string? MembershipNumber,
    string? FatherMembershipNumber,
    string? AncestralVillage,
    string? Phone,
    string? WhatsappNumber,
    string? Address,
    string? Occupation,
    decimal? MonthlyIncome,
    int? HouseholdSize,
    string? MaritalStatus);

/// <summary>One mapped guarantor row (ROZGAR only) — same shape as <see cref="GuarantorEntry"/>
/// (minus <c>Id</c>, always a new row on intake) so it can be handed to
/// <c>IApplicationDetailsService.ReplaceGuarantorsAsync</c> as-is.</summary>
public sealed record MappedGuarantor(
    int SequenceNumber, string? MembershipNumber, string FullName, string? FatherName, string? GrandfatherName,
    string? Surname, string? Cnic, string? ResidentialAddress, string? BusinessAddress, string? BusinessNature,
    string? PhoneMobile);

/// <summary>Where one uploaded file belongs. <see cref="SlotKey"/> is null for the ROZGAR combined
/// upload (staff re-slot it manually) and for anything the mapper couldn't place — those still get
/// stored (as an unslotted, applicant/application-owned <see cref="DocumentType.SupportingDocument"/>)
/// rather than dropped, per C5's "nothing is silently lost" rule.</summary>
public sealed record MappedFileTarget(
    GoogleFormFileManifestEntry Source, DocumentSlotOwnerScope OwnerScope, DocumentType DocumentType, string? SlotKey,
    int? GuarantorSequenceNumber, string? Description = null);

/// <summary>The full result of mapping one <see cref="GoogleFormSubmissionRequest"/> — pure data,
/// no EF/DB access. <see cref="GoogleFormIntakeService"/> (Infrastructure) turns this into actual
/// writes via the existing application/applicant/document services.</summary>
public sealed record GoogleFormMappingResult(
    string CategoryCode,
    string FundCategoryCode,
    MappedApplicantFields Applicant,
    string? Purpose,
    decimal? RequestedAmount,
    DateTimeOffset? DeclarationAcceptedAt,
    decimal? DeclaredMonthlyIncome,
    int? DeclaredHouseholdSize,
    int? DeclaredEarningMembers,
    string? DeclaredResidentialAddress,
    string? DeclaredBusinessAddress,
    string? DeclaredHouseStatus,
    UpsertHousingApplicationDetailsRequest? Housing,
    UpsertMarriageApplicationDetailsRequest? Marriage,
    UpsertBusinessLoanApplicationDetailsRequest? BusinessLoan,
    UpsertEducationApplicationDetailsRequest? Education,
    UpsertHealthApplicationDetailsRequest? Health,
    IReadOnlyList<MappedGuarantor> Guarantors,
    IReadOnlyList<MappedFileTarget> Files,
    IReadOnlyList<string> Notes);
