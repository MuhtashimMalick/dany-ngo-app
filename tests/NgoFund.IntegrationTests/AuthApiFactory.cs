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

    public async Task InitializeAsync() => await _container.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting (not ConfigureAppConfiguration/AddInMemoryCollection) — it's applied after
        // the app's own appsettings.json is composed, so it actually wins instead of being
        // silently overridden back to the appsettings.json fallback value.
        builder.UseSetting("ConnectionStrings:Default", _container.GetConnectionString());
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
