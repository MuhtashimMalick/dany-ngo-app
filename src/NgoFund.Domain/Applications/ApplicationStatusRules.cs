using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Applications;

/// <summary>
/// The single canonical set of "active" (non-terminal) application statuses — every
/// <see cref="ApplicationStatus"/> except the two terminal ones, <see cref="ApplicationStatus.Rejected"/>
/// and <see cref="ApplicationStatus.Paid"/> (see <c>FundApplication.AllowedTransitions</c>, whose
/// terminal rows have no outgoing transitions at all). Read by both the duplicate-active-application
/// badge (<c>FundApplicationService.GetApplicationsAsync</c>) and the guarantor-conflict check
/// (<c>FundApplication.EnsureGuarantorConflictsResolved</c>) so "active" is defined in exactly one
/// place and can never drift between the two call sites.
/// </summary>
public static class ApplicationStatusRules
{
    public static readonly IReadOnlyCollection<ApplicationStatus> ActiveStatuses =
    [
        ApplicationStatus.Pending,
        ApplicationStatus.UnderReview,
        ApplicationStatus.Approved,
        ApplicationStatus.PartiallyPaid,
        ApplicationStatus.OnHold,
    ];
}
