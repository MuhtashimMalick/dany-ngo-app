using System.Security.Cryptography;
using System.Text;

namespace NgoFund.Infrastructure.Persistence.Seed;

/// <summary>
/// Produces a stable Guid from a human-readable name (MD5 of its UTF-8 bytes) so seed rows have
/// fixed, reviewable, reproducible primary keys across migrations instead of hand-typed random
/// GUIDs that mean nothing when read in a diff.
/// </summary>
internal static class DeterministicGuid
{
    public static Guid From(string name) => new(MD5.HashData(Encoding.UTF8.GetBytes(name))[..16]);
}
