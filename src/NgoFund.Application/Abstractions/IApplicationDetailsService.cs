using NgoFund.Contracts.Applications;

namespace NgoFund.Application.Abstractions;

/// <summary>
/// Get/upsert for the three category-specific application-details tables (housing/marriage/
/// business-loan) plus get/replace-all for guarantors. Every method validates that the target
/// application's category matches the endpoint (<see cref="Domain.Exceptions.ApplicationCategoryMismatchException"/>)
/// before touching the details table — a details payload for the wrong category is a client bug,
/// not a valid partial state.
/// </summary>
public interface IApplicationDetailsService
{
    Task<HousingApplicationDetailsDto?> GetHousingDetailsAsync(Guid applicationId, CancellationToken cancellationToken);
    Task<HousingApplicationDetailsDto> UpsertHousingDetailsAsync(Guid applicationId, UpsertHousingApplicationDetailsRequest request, CancellationToken cancellationToken);

    Task<MarriageApplicationDetailsDto?> GetMarriageDetailsAsync(Guid applicationId, CancellationToken cancellationToken);
    Task<MarriageApplicationDetailsDto> UpsertMarriageDetailsAsync(Guid applicationId, UpsertMarriageApplicationDetailsRequest request, CancellationToken cancellationToken);

    Task<BusinessLoanApplicationDetailsDto?> GetBusinessLoanDetailsAsync(Guid applicationId, CancellationToken cancellationToken);
    Task<BusinessLoanApplicationDetailsDto> UpsertBusinessLoanDetailsAsync(Guid applicationId, UpsertBusinessLoanApplicationDetailsRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApplicationGuarantorDto>> GetGuarantorsAsync(Guid applicationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApplicationGuarantorDto>> ReplaceGuarantorsAsync(Guid applicationId, ReplaceApplicationGuarantorsRequest request, CancellationToken cancellationToken);

    /// <summary>Item 4 (2026-09 feedback): approves proceeding despite a guarantor's CNIC conflicting
    /// with another active application — sets the guarantor row's three
    /// <c>conflict_override_*</c> columns to the current user/time/reason.</summary>
    Task<ApplicationGuarantorDto> ApproveGuarantorConflictOverrideAsync(
        Guid applicationId, Guid guarantorId, ApproveGuarantorConflictOverrideRequest request, CancellationToken cancellationToken);
}
