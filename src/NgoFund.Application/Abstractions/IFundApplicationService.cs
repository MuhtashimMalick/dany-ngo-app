using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Common;

namespace NgoFund.Application.Abstractions;

/// <summary>
/// Named after the <c>FundApplication</c> domain entity (itself named that way to avoid clashing
/// with the <c>NgoFund.Application</c> project/namespace) rather than the more natural
/// "IApplicationService".
/// </summary>
public interface IFundApplicationService
{
    /// <summary>
    /// Backs both the Applications screen and the global search bar — <paramref name="query"/>'s
    /// Search term matches application number, membership number, applicant name, or CNIC;
    /// <paramref name="categoryId"/>/<paramref name="dateFrom"/>/<paramref name="dateTo"/> narrow
    /// further by application category and application-date range.
    /// </summary>
    Task<PagedResult<ApplicationDto>> GetApplicationsAsync(
        PagedQuery query, string? status, Guid? applicantId, Guid? categoryId, DateOnly? dateFrom, DateOnly? dateTo,
        CancellationToken cancellationToken);

    Task<ApplicationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Enforces the Zakat rule (see FundApplication.EnsureFundIsCompatible) before creating.</summary>
    Task<ApplicationDto> CreateAsync(CreateApplicationRequest request, CancellationToken cancellationToken);

    Task<ApplicationDto> UpdateAsync(Guid id, UpdateApplicationRequest request, CancellationToken cancellationToken);

    /// <summary>Validates the transition via FundApplication.TransitionTo and logs a status-history row.</summary>
    Task ChangeStatusAsync(Guid id, ChangeApplicationStatusRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApplicationStatusHistoryDto>> GetStatusHistoryAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApplicationRemarkDto>> GetRemarksAsync(Guid id, CancellationToken cancellationToken);

    Task<ApplicationRemarkDto> AddRemarkAsync(Guid id, AddRemarkRequest request, CancellationToken cancellationToken);

    /// <summary>The completeness gate's evaluation, exposed read-only — backs the wizard's upload
    /// step and the manage-view checklist, using the exact same evaluator the Approved-transition
    /// gate in <see cref="ChangeStatusAsync"/> runs, so the two can never disagree.</summary>
    Task<ApplicationCompletenessDto> GetCompletenessAsync(Guid id, CancellationToken cancellationToken);
}
