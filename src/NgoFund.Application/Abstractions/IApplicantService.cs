using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Documents;

namespace NgoFund.Application.Abstractions;

public interface IApplicantService
{
    Task<PagedResult<ApplicantDto>> GetApplicantsAsync(PagedQuery query, CancellationToken cancellationToken);

    Task<ApplicantDto> GetApplicantByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ApplicantDto> CreateAsync(CreateApplicantRequest request, CancellationToken cancellationToken);

    Task<ApplicantDto> UpdateAsync(Guid id, UpdateApplicantRequest request, CancellationToken cancellationToken);

    /// <summary>"Delete" per the scope document — a real soft delete (ISoftDeletable), not a hard delete.</summary>
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Links an already-uploaded document (e.g. a webcam capture) as this applicant's profile photo.</summary>
    Task SetProfilePhotoAsync(Guid applicantId, Guid documentId, CancellationToken cancellationToken);

    /// <summary>
    /// "Set" semantics: uploads <paramref name="content"/> as the applicant's document of
    /// <paramref name="documentType"/>, replacing in place whatever was previously on file for that
    /// category (same <c>documents</c> row/id — see <see cref="Domain.Entities.Document"/>'s
    /// <c>ReplaceProfileDocumentAsync</c> implementation for why) and hard-deleting any extra
    /// duplicates of that type (e.g. from a repeated Google Form submission), so the category always
    /// converges to exactly one document. Creates a new document when none existed yet.
    /// <paramref name="documentType"/> must be one of <see cref="ApplicantProfileDocumentTypes.All"/>
    /// (<c>CnicFront</c>/<c>CnicBack</c>/<c>MembershipCard</c>) — the profile photo
    /// (<c>ApplicantPhoto</c>) is not accepted here, it has its own separate
    /// <see cref="SetProfilePhotoAsync"/> flow.
    /// </summary>
    Task<DocumentDto> ReplaceProfileDocumentAsync(
        Guid applicantId, string documentType, Stream content, string fileName, string contentType, CancellationToken cancellationToken);
}
