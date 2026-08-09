using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;

namespace NgoFund.Infrastructure.Services;

/// <summary>
/// Builds the <see cref="ApplicationStatusHistory"/> row for a status change that a service
/// triggers as a side effect of its own action — a payment being recorded/voided
/// (<see cref="PaymentService"/>), or an application immediately re-settling to ledger truth right
/// after a manual transition into Approved (<see cref="FundApplicationService"/>) — rather than a
/// direct reviewer-chosen transition. Shared so the row-construction isn't duplicated between the
/// two services ( DRY); each caller still owns adding it to its own <c>DbContext</c> and
/// wording its own remarks.
/// </summary>
internal static class AutoStatusHistoryFactory
{
    public static ApplicationStatusHistory Create(Guid applicationId, ApplicationStatus from, ApplicationStatus to, Guid? changedBy, string remarks) =>
        new()
        {
            ApplicationId = applicationId,
            FromStatus = from,
            ToStatus = to,
            Remarks = remarks,
            ChangedAt = DateTimeOffset.UtcNow,
            ChangedBy = changedBy,
        };
}
