using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NgoFund.Application.Abstractions;
using NgoFund.Infrastructure;
using NgoFund.Infrastructure.Persistence;
using NgoFund.Infrastructure.Persistence.Seed;
using NgoFund.Infrastructure.Services;

// One-shot job: apply pending EF Core migrations, then seed the initial Super Admin user.
// Runs as the docker-compose "migrator" service; the "api" service only starts once this exits 0.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ICurrentUserService, SystemCurrentUserService>();

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Migrator");

try
{
    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    logger.LogInformation("Applying pending migrations...");
    await dbContext.Database.MigrateAsync();
    logger.LogInformation("Migrations applied successfully.");

    logger.LogInformation("Seeding initial Super Admin user (if none exists)...");
    await IdentitySeeder.SeedSuperAdminAsync(scope.ServiceProvider);
    logger.LogInformation("Seed step complete.");

    return 0;
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Migration/seed failed.");
    return 1;
}
