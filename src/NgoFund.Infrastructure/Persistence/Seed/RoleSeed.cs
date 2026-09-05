using Microsoft.EntityFrameworkCore;
using NgoFund.Infrastructure.Identity;

namespace NgoFund.Infrastructure.Persistence.Seed;

/// <summary>The five roles named in the client scope document, each pre-mapped to a permission set.</summary>
internal static class RoleSeed
{
    public static readonly Guid SuperAdminId = DeterministicGuid.From("role:SuperAdmin");
    public static readonly Guid AdminId = DeterministicGuid.From("role:Admin");
    public static readonly Guid AccountsManagerId = DeterministicGuid.From("role:AccountsManager");
    public static readonly Guid DataEntryOperatorId = DeterministicGuid.From("role:DataEntryOperator");
    public static readonly Guid ViewerId = DeterministicGuid.From("role:Viewer");

    public static void Apply(ModelBuilder builder)
    {
        (Guid Id, string Name, string Description)[] roles =
        [
            (SuperAdminId, "SuperAdmin", "Full system access, including role/permission and settings management."),
            (AdminId, "Admin", "Read-only access to operational data; cannot manage roles/permissions or system settings."),
            (AccountsManagerId, "AccountsManager", "Read-only access to donor, donation, payment and financial report data."),
            (DataEntryOperatorId, "DataEntryOperator", "Read-only access to applicants and applications."),
            (ViewerId, "Viewer", "Read-only access across the system."),
        ];

        builder.Entity<ApplicationRole>().HasData(roles.Select(r => new ApplicationRole
        {
            Id = r.Id,
            Name = r.Name,
            NormalizedName = r.Name.ToUpperInvariant(),
            Description = r.Description,
            IsSystem = true,
            ConcurrencyStamp = DeterministicGuid.From($"role-stamp:{r.Name}").ToString(),
        }));

        builder.Entity<RolePermission>().HasData(BuildRolePermissions());
    }

    /// <summary>
    /// Only SuperAdmin may write/create/update/delete/approve/void anything. Every other role is
    /// read-only — this is what "read-only" means everywhere below: an action of "view" or
    /// "export" (keyed off <see cref="PermissionSeed.Definitions"/>'s own Action field, so a
    /// permission added later is automatically excluded from every non-SuperAdmin role unless its
    /// action is view/export — nothing to remember to update here).
    /// </summary>
    private static bool IsReadOnly(string action) => action is "view" or "export";

    private static IEnumerable<RolePermission> BuildRolePermissions()
    {
        var all = PermissionSeed.Definitions;

        // SuperAdmin: everything.
        foreach (var p in all)
        {
            yield return Map(SuperAdminId, p.Module, p.Action);
        }

        // Admin: read-only across every module (previously full access except role/settings
        // management; module scope unchanged, now filtered to view/export only).
        foreach (var p in all.Where(p => IsReadOnly(p.Action)))
        {
            yield return Map(AdminId, p.Module, p.Action);
        }

        // AccountsManager: read-only donors/donations/payments/fund categories/dashboard/reports/
        // documents, plus read-only applications and loans (same module scope as before, now
        // view/export only).
        string[] accountsManagerModules = ["donors", "donations", "payments", "fundcategories", "dashboard", "reports", "documents", "applications", "loans"];
        foreach (var p in all.Where(p => accountsManagerModules.Contains(p.Module) && IsReadOnly(p.Action)))
        {
            yield return Map(AccountsManagerId, p.Module, p.Action);
        }

        // DataEntryOperator: read-only applicants/applications/documents/donors/donations/dashboard
        // (same module scope as before, now view/export only).
        string[] dataEntryOperatorModules = ["applicants", "applications", "documents", "donors", "donations", "dashboard"];
        foreach (var p in all.Where(p => dataEntryOperatorModules.Contains(p.Module) && IsReadOnly(p.Action)))
        {
            yield return Map(DataEntryOperatorId, p.Module, p.Action);
        }

        // Viewer: every *.view permission, plus dashboard and reports viewing.
        foreach (var p in all.Where(p => p.Action == "view"))
        {
            yield return Map(ViewerId, p.Module, p.Action);
        }
    }

    private static RolePermission Map(Guid roleId, string module, string action) => new()
    {
        RoleId = roleId,
        PermissionId = PermissionSeed.IdFor(module, action),
    };
}
