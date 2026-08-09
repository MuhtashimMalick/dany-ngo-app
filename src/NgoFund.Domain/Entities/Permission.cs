using NgoFund.Domain.Common;

namespace NgoFund.Domain.Entities;

/// <summary>
/// A single grantable permission, e.g. "donations.create", "payments.void". Seeded at migration
/// time, not user-editable — new permissions ship as code, roles decide who has them via
/// <c>RolePermission</c> (in the Infrastructure/Identity layer, since it links to
/// <c>ApplicationRole</c>).
/// </summary>
public class Permission : BaseEntity
{
    /// <summary>"{module}.{action}", e.g. "donations.create". Unique.</summary>
    public string Code { get; set; } = null!;

    public string Module { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? Description { get; set; }
}
