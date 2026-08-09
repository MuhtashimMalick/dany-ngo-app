namespace NgoFund.Infrastructure.Security;

/// <summary>Bound from the "Jwt" configuration section — see appsettings.json / docker-compose.yml.</summary>
public class JwtOptions
{
    public string SigningKey { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
