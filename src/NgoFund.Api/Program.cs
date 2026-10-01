using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using NgoFund.Api.Authentication;
using NgoFund.Api.Authorization;
using NgoFund.Api.ExceptionHandling;
using NgoFund.Api.Services;
using NgoFund.Application.Abstractions;
using NgoFund.Infrastructure;
using NgoFund.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["SigningKey"]!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    })
    // C7: a second, independent scheme — an X-Intake-Key header, never a JWT. The JWT scheme above
    // stays the [Authorize] default for every other controller, so it never accepts an intake key,
    // and IntakeController is the only place that opts into this scheme, so it never accepts a JWT.
    .AddScheme<AuthenticationSchemeOptions, GoogleFormIntakeAuthenticationHandler>(GoogleFormIntakeAuthenticationHandler.SchemeName, _ => { });

// Dynamic permission-based authorization — see : adding a permission never requires
// registering a new named policy.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// Defense-in-depth against credential-stuffing/brute-force alongside AuthService's per-account
// Identity lockout: this caps attempts per source IP regardless of which account is targeted.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    // C7: the Google Form intake endpoints sit behind ngrok on the public internet — 60/min per IP
    // caps abuse of a key that, once it leaked, would otherwise have no other rate limit at all.
    options.AddPolicy("intake", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

var app = builder.Build();

app.UseExceptionHandler();

// Baseline security headers for every response.
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    await next();
});

// C7: exposure hardening, two-way (feedback round 3, item F). ngrok's public tunnel points at the
// intake port only: that port serves ONLY /api/intake/*, so a tunnel misconfiguration (or ngrok
// itself being compromised) can never reach login or any staff endpoint. Symmetrically, every
// OTHER port must never serve /api/intake/* either — the staff-facing port has no rate limiting
// or ngrok-specific hardening for that surface. Runs before UseAuthentication so a blocked
// request never even reaches the auth pipeline.
var intakePort = app.Configuration.GetValue<int?>("Intake:Port");
app.Use(async (context, next) =>
{
    if (IntakePortGate.ShouldBlock(intakePort, context.Connection.LocalPort, context.Request.Path))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>Marker so <c>WebApplicationFactory&lt;Program&gt;</c> can target this entry point from integration tests.</summary>
public partial class Program;
