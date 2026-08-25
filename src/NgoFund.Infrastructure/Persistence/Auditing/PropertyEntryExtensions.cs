using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace NgoFund.Infrastructure.Persistence.Auditing;

/// <summary>
/// <see cref="PropertyEntry.IsModified"/> alone is not reliable for "did this value actually
/// change": ASP.NET Core Identity's EF Core <c>UserStore</c> calls <c>Context.Update(user)</c>
/// internally on every single <c>UserManager.UpdateAsync</c> call (Attach then Update, not Attach
/// then mutate individual properties) — which unconditionally flags EVERY scalar property
/// Modified, regardless of whether its value differs from <see cref="PropertyEntry.OriginalValue"/>.
/// Without this, a login/refresh's harmless <c>LastLoginAt</c> bump looks identical to a full-row
/// rewrite, including a false "PasswordHash changed". Noise-filtering, narration, and the forensic
/// old/new-values diff all need to agree on what really changed, so they all go through this.
/// </summary>
internal static class PropertyEntryExtensions
{
    internal static bool ActuallyChanged(this PropertyEntry property) =>
        property.IsModified && !Equals(property.OriginalValue, property.CurrentValue);

    internal static IReadOnlyList<PropertyEntry> GetActuallyChangedProperties(this EntityEntry entry) =>
        entry.Properties.Where(p => p.ActuallyChanged()).ToList();
}
