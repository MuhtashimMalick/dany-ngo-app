using System.Security.Cryptography;
using System.Text;

namespace NgoFund.Infrastructure.Security;

/// <summary>SHA-256 of a refresh token — only the hash is ever persisted (see RefreshToken.TokenHash).</summary>
internal static class TokenHasher
{
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
