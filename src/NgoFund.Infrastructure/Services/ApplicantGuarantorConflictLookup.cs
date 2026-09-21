using Microsoft.EntityFrameworkCore;
using NgoFund.Domain.Applications;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

/// <summary>
/// Mirror direction of <see cref="GuarantorConflictLookup"/>: instead of "two guarantor rows share a
/// CNIC," this finds applications whose OWN applicant's CNIC also appears as a guarantor on a
/// DIFFERENT application that is currently active (<see cref="ApplicationStatusRules.ActiveStatuses"/>).
/// One batched query for the whole input set, not one per application — same standard as
/// <c>FundApplicationService.GetActiveApplicationCountAsync</c>. Relies on
/// <see cref="AppDbContext.ApplicationGuarantors"/>'s existing soft-delete query filter rather than
/// hand-writing an <c>is_deleted</c> condition.
/// </summary>
internal static class ApplicantGuarantorConflictLookup
{
    public static async Task<Dictionary<Guid, List<string>>> FindConflictsAsync(
        AppDbContext dbContext, IReadOnlyCollection<(Guid ApplicationId, string ApplicantCnic)> applications, CancellationToken cancellationToken)
    {
        var cnics = applications.Select(a => a.ApplicantCnic).Distinct().ToList();
        if (cnics.Count == 0)
        {
            return [];
        }

        var activeStatuses = ApplicationStatusRules.ActiveStatuses;

        var guarantorRows = await dbContext.ApplicationGuarantors
            .Where(g => g.Cnic != null && cnics.Contains(g.Cnic))
            .Join(dbContext.Applications, g => g.ApplicationId, a => a.Id,
                (g, a) => new { g.Cnic, GuarantorOwningApplicationId = a.Id, a.ApplicationNumber, a.Status })
            .Where(x => activeStatuses.Contains(x.Status))
            .ToListAsync(cancellationToken);

        var applicationNumbersByCnic = guarantorRows
            .GroupBy(x => x.Cnic!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new Dictionary<Guid, List<string>>();
        foreach (var (applicationId, applicantCnic) in applications)
        {
            if (!applicationNumbersByCnic.TryGetValue(applicantCnic, out var rows))
            {
                continue;
            }

            // Excludes self-guarantee: a person guaranteeing their own application is not a conflict.
            var conflicting = rows
                .Where(r => r.GuarantorOwningApplicationId != applicationId)
                .Select(r => r.ApplicationNumber)
                .Distinct()
                .ToList();

            if (conflicting.Count > 0)
            {
                result[applicationId] = conflicting;
            }
        }

        return result;
    }
}
