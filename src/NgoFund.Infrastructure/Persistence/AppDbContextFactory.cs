using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NgoFund.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` create an <see cref="AppDbContext"/> directly at design time (migrations
/// add/remove/script) without spinning up the full app host — the host's Identity/DataProtection
/// DI graph isn't needed just to read the model. Not used at runtime; the real app always goes
/// through <see cref="DependencyInjection.AddInfrastructure"/>.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=ngofund;Username=ngofund;Password=changeme-in-env";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention();

        return new AppDbContext(optionsBuilder.Options);
    }
}
