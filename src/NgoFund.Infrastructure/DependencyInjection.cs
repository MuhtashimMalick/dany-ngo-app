using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NgoFund.Application.Abstractions;
using NgoFund.Infrastructure.Identity;
using NgoFund.Infrastructure.Persistence;
using NgoFund.Infrastructure.Persistence.Interceptors;
using NgoFund.Infrastructure.Security;
using NgoFund.Infrastructure.Services;
using NgoFund.Infrastructure.Storage;

namespace NgoFund.Infrastructure;

/// <summary>
/// Single composition root for this layer's services, consumed by both <c>NgoFund.Api</c> and
/// <c>NgoFund.Migrator</c> so the two never configure the DbContext/Identity differently.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        // Needed by Identity's token providers (password reset, email confirmation, etc.) —
        // AddIdentityCore doesn't register it automatically the way full AddIdentity does.
        services.AddDataProtection();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;

                // Brute-force hardening: 5 failed attempts locks the account for 15 minutes.
                // AuthService checks IsLockedOutAsync/calls AccessFailedAsync explicitly since
                // AddIdentityCore (no SignInManager) doesn't wire this up automatically the way
                // PasswordSignInAsync would.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<INumberGenerator, NumberGenerator>();
        services.AddScoped<IFundCategoryService, FundCategoryService>();
        services.AddScoped<IDonorService, DonorService>();
        services.AddScoped<IDonationService, DonationService>();

        services.Configure<FileStorageOptions>(configuration.GetSection("Storage"));
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IApplicantService, ApplicantService>();
        services.AddScoped<IFundApplicationService, FundApplicationService>();
        services.AddScoped<IApplicationCategoryService, ApplicationCategoryService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAppSettingService, AppSettingService>();

        services.AddValidatorsFromAssembly(typeof(NgoFund.Application.Validators.LoginRequestValidator).Assembly);

        return services;
    }
}
