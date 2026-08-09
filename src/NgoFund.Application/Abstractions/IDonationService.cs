using NgoFund.Contracts.Common;
using NgoFund.Contracts.Donations;

namespace NgoFund.Application.Abstractions;

public interface IDonationService
{
    Task<PagedResult<DonationDto>> GetDonationsAsync(PagedQuery query, Guid? donorId, CancellationToken cancellationToken);

    Task<DonationDto> GetDonationByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Creates the donation and posts a matching Credit to the fund ledger in one transaction.</summary>
    Task<DonationDto> CreateAsync(CreateDonationRequest request, CancellationToken cancellationToken);

    /// <summary>Marks the donation Voided and posts a reversing Debit to the fund ledger in one transaction.</summary>
    Task VoidAsync(Guid id, VoidDonationRequest request, CancellationToken cancellationToken);
}
