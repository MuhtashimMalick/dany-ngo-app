using NgoFund.Contracts.Applications;

namespace NgoFund.Application.Abstractions;

/// <summary>
/// Get/upsert for the five category-specific application-details tables (housing/marriage/
/// business-loan/education/health) plus get/replace-all for guarantors. Every method validates
/// that the target application's category matches the endpoint
/// (<see cref="Domain.Exceptions.ApplicationCategoryMismatchException"/>) before touching the
/// details table — a details payload for the wrong category is a client bug, not a valid partial
/// state.
///
/// C2: every Upsert*DetailsAsync below takes an optional <c>enforceRequiredFields</c> (default
/// true) controlling whether it throws on missing category-specific required fields (layer 2 of
/// the completeness gate, docs/schema.md). Controllers never pass it — only the Google Form
/// intake pipeline passes false, since intake legitimately can't fill every field a staff-entered
/// application would. The Approved-transition gate (layer 3,
/// <c>ApplicationCompletenessEvaluator</c> via <c>FundApplicationService.ChangeStatusAsync</c>) is
/// unaffected either way and remains the real enforcement.
/// </summary>
public interface IApplicationDetailsService
{
    Task<HousingApplicationDetailsDto?> GetHousingDetailsAsync(Guid applicationId, CancellationToken cancellationToken);
    Task<HousingApplicationDetailsDto> UpsertHousingDetailsAsync(Guid applicationId, UpsertHousingApplicationDetailsRequest request, CancellationToken cancellationToken, bool enforceRequiredFields = true);

    Task<MarriageApplicationDetailsDto?> GetMarriageDetailsAsync(Guid applicationId, CancellationToken cancellationToken);
    Task<MarriageApplicationDetailsDto> UpsertMarriageDetailsAsync(Guid applicationId, UpsertMarriageApplicationDetailsRequest request, CancellationToken cancellationToken, bool enforceRequiredFields = true);

    Task<BusinessLoanApplicationDetailsDto?> GetBusinessLoanDetailsAsync(Guid applicationId, CancellationToken cancellationToken);
    Task<BusinessLoanApplicationDetailsDto> UpsertBusinessLoanDetailsAsync(Guid applicationId, UpsertBusinessLoanApplicationDetailsRequest request, CancellationToken cancellationToken, bool enforceRequiredFields = true);

    Task<EducationApplicationDetailsDto?> GetEducationDetailsAsync(Guid applicationId, CancellationToken cancellationToken);
    Task<EducationApplicationDetailsDto> UpsertEducationDetailsAsync(Guid applicationId, UpsertEducationApplicationDetailsRequest request, CancellationToken cancellationToken, bool enforceRequiredFields = true);

    Task<HealthApplicationDetailsDto?> GetHealthDetailsAsync(Guid applicationId, CancellationToken cancellationToken);
    Task<HealthApplicationDetailsDto> UpsertHealthDetailsAsync(Guid applicationId, UpsertHealthApplicationDetailsRequest request, CancellationToken cancellationToken, bool enforceRequiredFields = true);

    Task<IReadOnlyList<ApplicationGuarantorDto>> GetGuarantorsAsync(Guid applicationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApplicationGuarantorDto>> ReplaceGuarantorsAsync(Guid applicationId, ReplaceApplicationGuarantorsRequest request, CancellationToken cancellationToken);

    /// <summary>Item 4 (2026-09 feedback): approves proceeding despite a guarantor's CNIC conflicting
    /// with another active application — sets the guarantor row's three
    /// <c>conflict_override_*</c> columns to the current user/time/reason.</summary>
    Task<ApplicationGuarantorDto> ApproveGuarantorConflictOverrideAsync(
        Guid applicationId, Guid guarantorId, ApproveGuarantorConflictOverrideRequest request, CancellationToken cancellationToken);
}
