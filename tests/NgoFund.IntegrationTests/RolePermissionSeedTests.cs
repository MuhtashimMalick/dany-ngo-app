using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Donors;
using NgoFund.Contracts.Users;
using NgoFund.Infrastructure.Identity;
using NgoFund.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the RBAC seed data against a real, freshly migrated Postgres 18: only SuperAdmin may
/// hold a write/create/update/delete/approve/void permission — every other role's grants must all
/// be "view" or "export". Modelled on <see cref="CategorySchemaConstraintsTests"/>'s direct-EF,
/// no-API-host harness.
/// </summary>
public class RolePermissionSeedTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();

    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString(), npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        _db = new AppDbContext(options);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task NonSuperAdminRoles_OnlyHoldViewOrExportPermissions()
    {
        var grants = await (
            from rp in _db.Set<RolePermission>()
            join role in _db.Roles on rp.RoleId equals role.Id
            join permission in _db.Set<NgoFund.Domain.Entities.Permission>() on rp.PermissionId equals permission.Id
            where role.Name != "SuperAdmin"
            select new { role.Name, permission.Code }
        ).ToListAsync();

        Assert.NotEmpty(grants);

        // Code is "{module}.{action}" — split client-side (Substring/IndexOf don't translate to SQL).
        var writeGrants = grants.Where(g => g.Code.Split('.', 2)[1] is not ("view" or "export")).ToList();

        Assert.True(writeGrants.Count == 0,
            $"Non-SuperAdmin role(s) hold write permission(s): {string.Join(", ", writeGrants.Select(g => $"{g.Name}:{g.Code}"))}");
    }

    [Fact]
    public async Task SuperAdmin_HoldsEveryDefinedPermission()
    {
        // "Every defined permission" = every row in the permissions table itself (seeded from
        // PermissionSeed.Definitions), not a hardcoded count — so this stays meaningful if a
        // permission is ever added or removed.
        var allCodes = await _db.Set<NgoFund.Domain.Entities.Permission>().Select(p => p.Code).ToListAsync();

        var superAdminCodes = await (
            from rp in _db.Set<RolePermission>()
            join role in _db.Roles on rp.RoleId equals role.Id
            join permission in _db.Set<NgoFund.Domain.Entities.Permission>() on rp.PermissionId equals permission.Id
            where role.Name == "SuperAdmin"
            select permission.Code
        ).ToListAsync();

        Assert.NotEmpty(allCodes);
        Assert.Equal(allCodes.OrderBy(c => c), superAdminCodes.OrderBy(c => c));
    }
}

/// <summary>
/// Proves the seed data from <see cref="RolePermissionSeedTests"/> is actually enforced at the API
/// boundary. Every other integration test authenticates as SuperAdmin, so none of them would catch
/// <c>[HasPermission]</c> being stripped from a write endpoint entirely — this is the one test that
/// would. Modelled on <see cref="FundLedgerExportTests.ViewerRoleOnly_IsForbiddenFromExporting"/>'s
/// real-API-host harness (distinct from <see cref="RolePermissionSeedTests"/>'s direct-EF harness
/// above, which has no API host to enforce against).
/// </summary>
public class RolePermissionEnforcementTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task AdminRole_IsForbiddenFromCreatingADonor_ButCanStillViewDonors()
    {
        var adminSeedClient = await factory.CreateAuthenticatedClientAsync();

        const string adminEmail = "admin-role-write-test@ngofund.local";
        const string adminPassword = "AdminRolePass123!";
        await ReadOrFailAsync<UserSummaryDto>(
            await adminSeedClient.PostAsJsonAsync("/api/users", new CreateUserRequest(adminEmail, "Admin Role Test", null, adminPassword, ["Admin"])),
            HttpStatusCode.Created);

        var adminClient = factory.CreateClient();
        var loginResponse = await adminClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(adminEmail, adminPassword));
        var auth = await ReadOrFailAsync<AuthResponse>(loginResponse, HttpStatusCode.OK);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Admin still has donors.view (read-only across every module) ...
        var listResponse = await adminClient.GetAsync("/api/donors");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        // ... but donors.create was stripped by the RestrictWritePermissionsToSuperAdmin migration.
        var createResponse = await adminClient.PostAsJsonAsync("/api/donors", new CreateDonorRequest(
            "Admin Write Test Donor", "Individual", null, null, null, null, null, null, null, null, null, false, null));
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }
}
