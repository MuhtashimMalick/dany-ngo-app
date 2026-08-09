using Microsoft.EntityFrameworkCore;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Seed;

/// <summary>
/// Every grantable permission in the system, as (module, action, display name). Code is
/// "{module}.{action}" and drives the `[HasPermission("module.action")]` authorization attribute
/// — adding a permission here is the only step needed, no policy registration required.
/// </summary>
internal static class PermissionSeed
{
    public static readonly (string Module, string Action, string Display)[] Definitions =
    [
        ("users", "view", "View Users"), ("users", "create", "Create Users"),
        ("users", "edit", "Edit Users"), ("users", "delete", "Delete Users"),

        ("roles", "view", "View Roles"), ("roles", "manage", "Manage Roles & Permissions"),

        ("donors", "view", "View Donors"), ("donors", "create", "Create Donors"),
        ("donors", "edit", "Edit Donors"), ("donors", "delete", "Delete Donors"),

        ("donations", "view", "View Donations"), ("donations", "create", "Record Donations"),
        ("donations", "edit", "Edit Donations"), ("donations", "void", "Void Donations"),

        ("fundcategories", "view", "View Fund Categories"), ("fundcategories", "manage", "Manage Fund Categories"),

        ("applicationcategories", "view", "View Application Categories"), ("applicationcategories", "manage", "Manage Application Categories"),

        ("applicants", "view", "View Applicants"), ("applicants", "create", "Create Applicants"),
        ("applicants", "edit", "Edit Applicants"), ("applicants", "delete", "Delete Applicants"),

        ("applications", "view", "View Applications"), ("applications", "create", "Create Applications"),
        ("applications", "edit", "Edit Applications"), ("applications", "review", "Review Applications"),
        ("applications", "approve", "Approve/Reject Applications"),

        ("payments", "view", "View Payments"), ("payments", "create", "Record Payments"),
        ("payments", "void", "Void Payments"),

        ("documents", "upload", "Upload Documents"), ("documents", "view", "View Documents"),
        ("documents", "delete", "Delete Documents"),

        ("dashboard", "view", "View Dashboard"),
        ("reports", "view", "View Reports"), ("reports", "export", "Export Reports"),
        ("auditlogs", "view", "View Audit Logs"),
        ("settings", "view", "View Settings"), ("settings", "manage", "Manage Settings"),
    ];

    public static Guid IdFor(string module, string action) => DeterministicGuid.From($"permission:{module}.{action}");

    public static void Apply(ModelBuilder builder)
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        builder.Entity<Permission>().HasData(Definitions.Select(d => new Permission
        {
            Id = IdFor(d.Module, d.Action),
            Code = $"{d.Module}.{d.Action}",
            Module = d.Module,
            DisplayName = d.Display,
            CreatedAt = now,
        }));
    }
}
