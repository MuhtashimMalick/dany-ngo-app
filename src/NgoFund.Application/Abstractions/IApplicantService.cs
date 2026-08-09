using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.Common;

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
}
