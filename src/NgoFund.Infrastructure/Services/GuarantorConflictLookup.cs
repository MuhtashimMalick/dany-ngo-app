using Microsoft.EntityFrameworkCore;
using NgoFund.Domain.Applications;
using NgoFund.Domain.Entities;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

/// <summary>
/// Item 4 (2026-09 feedback) read-side conflict detection: for a set of guarantors already loaded
/// for one application, finds OTHER <c>application_guarantors</c> rows sharing the same CNIC whose
/// owning application is currently active (<see cref="ApplicationStatusRules.ActiveStatuses"/>).
/// One batched query, not one per guarantor — shared by <see cref="ApplicationDetailsService"/>
/// (guarantor read/replace responses) and <see cref="FundApplicationService"/> (the
/// Approved-transition gate) so the two can never disagree about what counts as a conflict.
/// Guarantors with a null CNIC never conflict. Relies on <see cref="AppDbContext.ApplicationGuarantors"/>'s
/// existing soft-delete query filter rather than hand-writing an <c>is_deleted</c> condition.
/// </summary>
internal static class GuarantorConflictLookup
{
    public static async Task<Dictionary<Guid, List<string>>> FindConflictsAsync(
        AppDbContext dbContext, Guid applicationId, IReadOnlyList<ApplicationGuarantor> guarantors, CancellationToken cancellationToken)
    {
        var cnics = guarantors.Where(g => g.Cnic != null).Select(g => g.Cnic!).Distinct().ToList();
        if (cnics.Count == 0)
        {
            return [];
        }

        var activeStatuses = ApplicationStatusRules.ActiveStatuses;

        var otherActiveGuarantorCnics = await dbContext.ApplicationGuarantors
            .Where(g => g.ApplicationId != applicationId && g.Cnic != null && cnics.Contains(g.Cnic))
            .Join(dbContext.Applications, g => g.ApplicationId, a => a.Id,
                (g, a) => new { g.Cnic, a.ApplicationNumber, a.Status })
            .Where(x => activeStatuses.Contains(x.Status))
            .ToListAsync(cancellationToken);

        var applicationNumbersByCnic = otherActiveGuarantorCnics
            .GroupBy(x => x.Cnic!)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ApplicationNumber).Distinct().ToList());

        return guarantors
            .Where(g => g.Cnic != null && applicationNumbersByCnic.ContainsKey(g.Cnic))
            .ToDictionary(g => g.Id, g => applicationNumbersByCnic[g.Cnic!]);
    }
}
