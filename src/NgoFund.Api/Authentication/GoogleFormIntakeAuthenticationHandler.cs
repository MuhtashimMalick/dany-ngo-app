using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NgoFund.Domain.Common;

namespace NgoFund.Api.Authentication;

/// <summary>
/// C7: the "GoogleFormIntake" authentication scheme — reads header <c>X-Intake-Key</c> and
/// compares it against config <c>GoogleFormIntake:ApiKey</c> (env
/// <c>GoogleFormIntake__ApiKey</c>, from <c>.env</c>'s <c>GOOGLE_FORM_INTAKE_API_KEY</c>) using a
/// fixed-time comparison. If the configured key is unset or empty, this scheme ALWAYS fails —
/// deploying without the key effectively disables the whole intake surface rather than opening it
/// with no key. On success, builds a principal with <see cref="ClaimTypes.NameIdentifier"/> set to
/// <see cref="SystemUsers.GoogleFormIntakeUserId"/> and <see cref="ClaimTypes.Name"/> "Google Form
/// Intake" — <c>HttpContextCurrentUserService</c> reads those two claims generically regardless of
/// scheme, so every write on this path is attributed correctly with no changes there. Only
/// <see cref="Controllers.IntakeController"/> accepts this scheme; every other controller stays on
/// the JWT default scheme, which in turn never accepts an <c>X-Intake-Key</c> header.
/// </summary>
public class GoogleFormIntakeAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "GoogleFormIntake";
    private const string HeaderName = "X-Intake-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredKey = configuration["GoogleFormIntake:ApiKey"];
        if (string.IsNullOrEmpty(configuredKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Google Form intake is not configured."));
        }

        if (!Request.Headers.TryGetValue(HeaderName, out var provided) || string.IsNullOrEmpty(provided))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Missing '{HeaderName}' header."));
        }

        if (!FixedTimeEquals(provided.ToString(), configuredKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid intake key."));
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, SystemUsers.GoogleFormIntakeUserId.ToString()),
                new Claim(ClaimTypes.Name, "Google Form Intake"),
            ], SchemeName);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool FixedTimeEquals(string provided, string configured)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var configuredBytes = Encoding.UTF8.GetBytes(configured);

        // Different lengths would short-circuit FixedTimeEquals's own length check without
        // touching the byte contents, which is fine — the length itself isn't the secret here.
        return providedBytes.Length == configuredBytes.Length && CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }
}
