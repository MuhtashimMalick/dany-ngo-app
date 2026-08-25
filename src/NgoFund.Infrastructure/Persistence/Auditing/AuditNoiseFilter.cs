using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace NgoFund.Infrastructure.Persistence.Auditing;

/// <summary>
/// Login/session bookkeeping noise: a <c>Modified</c> entry with no <em>actually</em> changed
/// property (see <see cref="PropertyEntryExtensions"/>) — or whose actually-changed properties are
/// entirely within this set (e.g. a login/refresh that only bumps <c>LastLoginAt</c> and Identity's
/// own <c>ConcurrencyStamp</c>) — never becomes an audit row at all. Split out from
/// <c>AuditSaveChangesInterceptor</c> so it's unit-testable without a database.
/// </summary>
internal static class AuditNoiseFilter
{
    private static readonly HashSet<string> NoiseOnlyProperties =
        new(StringComparer.Ordinal) { "LastLoginAt", "AccessFailedCount", "LockoutEnd", "ConcurrencyStamp", "SecurityStamp" };

    internal static bool IsNoiseOnlyChange(EntityEntry entry)
    {
        var changedPropertyNames = entry.GetActuallyChangedProperties().Select(p => p.Metadata.Name).ToArray();
        return changedPropertyNames.Length == 0 || changedPropertyNames.All(NoiseOnlyProperties.Contains);
    }
}
