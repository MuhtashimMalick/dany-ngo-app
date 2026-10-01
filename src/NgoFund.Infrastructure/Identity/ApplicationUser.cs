using Microsoft.AspNetCore.Identity;

namespace NgoFund.Infrastructure.Identity;

/// <summary>
/// Extends ASP.NET Core Identity's <see cref="IdentityUser{TKey}"/> with the fields this app
/// needs. Lives in Infrastructure (not Domain) because it is inherently coupled to the Identity
/// framework choice — Domain stays free of that dependency.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = null!;

    public string? Designation { get; set; }

    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Per-user high-water mark for the Google Form unread badge; see docs/schema.md.</summary>
    public DateTimeOffset? IntakeLastSeenAt { get; set; }
}
