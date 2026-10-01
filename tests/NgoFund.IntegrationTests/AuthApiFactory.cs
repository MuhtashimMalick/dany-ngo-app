using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NgoFund.Infrastructure.Persistence;
using NgoFund.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Hosts the real <c>NgoFund.Api</c> app against a real, freshly migrated PostgreSQL 18
/// container — the full auth pipeline (JWT issuance, claims-based permission authorization,
/// the actual controllers) rather than mocking any of it.
/// </summary>
public class AuthApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();

    /// <summary>Set before the first client is created (the host builds lazily) to enable the
    /// Google Form intake auth scheme for a test class — left null, the scheme always fails
    /// closed, same as an unconfigured production deployment.</summary>
    public string? GoogleFormIntakeApiKey { get; set; }

    public async Task InitializeAsync() => await _container.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting (not ConfigureAppConfiguration/AddInMemoryCollection) — it's applied after
        // the app's own appsettings.json is composed, so it actually wins instead of being
        // silently overridden back to the appsettings.json fallback value.
        builder.UseSetting("ConnectionStrings:Default", _container.GetConnectionString());

        // Feedback round 3, item F: TestServer requests always report LocalPort 0, which never
        // equals appsettings.json's real Intake:Port (8081) — with the port gate now two-way, every
        // /api/intake/* test request would 404 unless the gate is disabled here. An empty string
        // (not omitting the setting) is required to actually override the appsettings.json value;
        // ConfigurationBinder.GetValue<int?> turns "" back into null, so IntakePortGate.ShouldBlock
        // sees "not configured" and never gates anything in-process.
        builder.UseSetting("Intake:Port", "");

        if (GoogleFormIntakeApiKey is not null)
        {
            builder.UseSetting("GoogleFormIntake:ApiKey", GoogleFormIntakeApiKey);
        }
    }

    public async Task<HttpClient> CreateSeededClientAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
        await IdentitySeeder.SeedSuperAdminAsync(scope.ServiceProvider);

        return CreateClient();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }
}
