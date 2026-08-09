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
            (AdminId, "Admin", "Full operational access; cannot manage roles/permissions or system settings."),
            (AccountsManagerId, "AccountsManager", "Manages donors, donations, payments and financial reports."),
            (DataEntryOperatorId, "DataEntryOperator", "Creates and edits applicants and applications; no financial or approval access."),
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

    private static IEnumerable<RolePermission> BuildRolePermissions()
    {
        var all = PermissionSeed.Definitions;

        // SuperAdmin: everything.
        foreach (var p in all)
        {
            yield return Map(SuperAdminId, p.Module, p.Action);
        }

        // Admin: everything except role/permission and settings management (reserved for SuperAdmin).
        foreach (var p in all.Where(p => !(p.Module == "roles" && p.Action == "manage") && !(p.Module == "settings" && p.Action == "manage")))
        {
            yield return Map(AdminId, p.Module, p.Action);
        }

        // AccountsManager: donors/donations/payments/fund categories/dashboard/reports, plus read-only applications.
        string[] accountsManagerModules = ["donors", "donations", "payments", "fundcategories", "dashboard", "reports", "documents"];
        foreach (var p in all.Where(p => accountsManagerModules.Contains(p.Module)))
        {
            yield return Map(AccountsManagerId, p.Module, p.Action);
        }
        yield return Map(AccountsManagerId, "applications", "view");

        // DataEntryOperator: create/edit applicants & applications, upload documents, view dashboard/donors.
        yield return Map(DataEntryOperatorId, "applicants", "view");
        yield return Map(DataEntryOperatorId, "applicants", "create");
        yield return Map(DataEntryOperatorId, "applicants", "edit");
        yield return Map(DataEntryOperatorId, "applications", "view");
        yield return Map(DataEntryOperatorId, "applications", "create");
        yield return Map(DataEntryOperatorId, "applications", "edit");
        yield return Map(DataEntryOperatorId, "documents", "upload");
        yield return Map(DataEntryOperatorId, "documents", "view");
        yield return Map(DataEntryOperatorId, "donors", "view");
        yield return Map(DataEntryOperatorId, "donations", "view");
        yield return Map(DataEntryOperatorId, "dashboard", "view");

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
