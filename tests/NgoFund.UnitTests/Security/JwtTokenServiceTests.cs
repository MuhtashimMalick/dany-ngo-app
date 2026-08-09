using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using NgoFund.Infrastructure.Security;

namespace NgoFund.UnitTests.Security;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(int accessTokenMinutes = 15) => new(Options.Create(new JwtOptions
    {
        SigningKey = "unit-test-signing-key-at-least-32-bytes-long!!",
        Issuer = "NgoFund",
        Audience = "NgoFundClients",
        AccessTokenMinutes = accessTokenMinutes,
    }));

    [Fact]
    public void GenerateAccessToken_EmbedsRoleAndPermissionClaims()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();

        var result = service.GenerateAccessToken(userId, "admin@ngofund.local", "Admin User", ["SuperAdmin"], ["users.view", "users.create"]);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        Assert.Equal(userId.ToString(), token.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("SuperAdmin", token.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(["users.view", "users.create"], token.Claims.Where(c => c.Type == "permission").Select(c => c.Value));
        Assert.Equal("NgoFund", token.Issuer);
        Assert.Contains("NgoFundClients", token.Audiences);
    }

    [Fact]
    public void GenerateAccessToken_ExpiresAtMatchesConfiguredLifetime()
    {
        var service = CreateService(accessTokenMinutes: 30);

        var before = DateTimeOffset.UtcNow;
        var result = service.GenerateAccessToken(Guid.NewGuid(), "a@b.com", "A B", [], []);
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(result.ExpiresAt, before.AddMinutes(30), after.AddMinutes(30).AddSeconds(1));
    }

    [Fact]
    public void GenerateRefreshToken_ProducesUniqueValuesEachCall()
    {
        var service = CreateService();

        var first = service.GenerateRefreshToken();
        var second = service.GenerateRefreshToken();

        Assert.NotEqual(first, second);
        Assert.False(string.IsNullOrWhiteSpace(first));
    }
}
