using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NgoFund.Domain.Common;
using NgoFund.Infrastructure.Identity;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Persistence.Seed;

/// <summary>
/// Creates the initial Super Admin user on first run. Runs after migrations, via
/// <c>UserManager</c> so the password goes through ASP.NET Core Identity's real hasher — static
/// <c>HasData</c> seeding (used for roles/permissions/fund categories) can't do that safely.
/// </summary>
public static class IdentitySeeder
{
    private const string DefaultAdminEmail = "admin@ngofund.local";
    private const string DefaultAdminPassword = "ChangeMe123!";

    public static async Task SeedSuperAdminAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(IdentitySeeder));

        if (await userManager.Users.AnyAsync())
        {
            return;
        }

        var admin = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = DefaultAdminEmail,
            Email = DefaultAdminEmail,
            EmailConfirmed = true,
            FullName = "System Administrator",
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var createResult = await userManager.CreateAsync(admin, DefaultAdminPassword);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to seed the Super Admin user: {errors}");
        }

        await userManager.AddToRoleAsync(admin, "SuperAdmin");

        logger.LogWarning(
            "Seeded initial Super Admin ({Email}) with a temporary password — MustChangePassword is set; change it on first login.",
            DefaultAdminEmail);
    }

    /// <summary>
    /// A8: seeds the non-login system user that attributes every write the Google Form intake
    /// pipeline makes. Written directly via <see cref="AppDbContext"/> (not <c>UserManager</c>) —
    /// it deliberately has no password at all, and <c>UserManager.CreateAsync(user)</c> without a
    /// password only works when Identity's password requirement is disabled globally, which it
    /// isn't. IsActive=false and no role grants mean it can never sign in through the normal JWT
    /// login path even if someone tried; only the "GoogleFormIntake" API-key auth scheme (C7) ever
    /// impersonates it. Idempotent — a no-op once the row exists.
    /// </summary>
    public static async Task SeedGoogleFormIntakeUserAsync(IServiceProvider services)
    {
        var dbContext = services.GetRequiredService<AppDbContext>();

        if (await dbContext.Users.AnyAsync(u => u.Id == SystemUsers.GoogleFormIntakeUserId))
        {
            return;
        }

        dbContext.Users.Add(new ApplicationUser
        {
            Id = SystemUsers.GoogleFormIntakeUserId,
            UserName = "google-form-intake",
            NormalizedUserName = "GOOGLE-FORM-INTAKE",
            Email = null,
            EmailConfirmed = false,
            FullName = "Google Form Intake",
            IsActive = false,
            MustChangePassword = false,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await dbContext.SaveChangesAsync();
    }
}
