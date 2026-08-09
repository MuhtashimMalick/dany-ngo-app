using Microsoft.AspNetCore.Identity;

namespace NgoFund.Infrastructure.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public string? Description { get; set; }

    /// <summary>System roles (Super Admin, Admin, Accounts Manager, Data Entry Operator, Viewer) cannot be deleted.</summary>
    public bool IsSystem { get; set; }
}
