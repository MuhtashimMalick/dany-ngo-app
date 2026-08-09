using NgoFund.Contracts.Common;
using NgoFund.Contracts.Donors;

namespace NgoFund.Application.Abstractions;

public interface IDonorService
{
    Task<PagedResult<DonorDto>> GetDonorsAsync(PagedQuery query, bool? activeOnly, CancellationToken cancellationToken);

    Task<DonorDto> GetDonorByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<DonorDto> CreateAsync(CreateDonorRequest request, CancellationToken cancellationToken);

    Task<DonorDto> UpdateAsync(Guid id, UpdateDonorRequest request, CancellationToken cancellationToken);

    /// <summary>"Delete" per the scope document — implemented as deactivation (IsActive/soft-delete), never a hard delete.</summary>
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);
}
