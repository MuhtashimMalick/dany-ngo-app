namespace NgoFund.Contracts.Applicants;

/// <summary>
/// The three <see cref="NgoFund.Domain.Enums.DocumentType"/> values collected on an applicant's
/// own profile (not application- or guarantor-scoped) that are mandatory at creation — see
/// <see cref="NgoFund.Infrastructure.Services.ApplicantService.CreateAsync"/>. The profile photo
/// (<c>ApplicantPhoto</c>) is a separate, optional slot (<c>Applicant.PhotoDocumentId</c>),
/// deliberately not in this list and not accepted by
/// <see cref="NgoFund.Application.Abstractions.IApplicantService.ReplaceProfileDocumentAsync"/> —
/// it has its own separate upload/set flow
/// (<see cref="NgoFund.Application.Abstractions.IApplicantService.SetProfilePhotoAsync"/>).
/// </summary>
public static class ApplicantProfileDocumentTypes
{
    public const string CnicFront = "CnicFront";
    public const string CnicBack = "CnicBack";
    public const string MembershipCard = "MembershipCard";
    public static readonly IReadOnlyList<string> All = new[] { CnicFront, CnicBack, MembershipCard };
}
