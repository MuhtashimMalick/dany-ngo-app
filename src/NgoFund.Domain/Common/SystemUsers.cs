namespace NgoFund.Domain.Common;

/// <summary>Deterministic ids for non-login system users, seeded once by
/// <c>IdentitySeeder</c> and referenced by both the Api (to build the authenticated principal for
/// the Google Form intake auth scheme) and Infrastructure (the seeder itself) — one constant
/// instead of a magic string/guid duplicated in both places.</summary>
public static class SystemUsers
{
    /// <summary>Attributes every write the Google Form intake pipeline makes (created_by/changed_by,
    /// the activity feed) — a real row in <c>users</c>, but IsActive=false, no password, no roles:
    /// it can never log in, only be impersonated by the "GoogleFormIntake" API-key auth scheme.</summary>
    public static readonly Guid GoogleFormIntakeUserId = new("00000000-0000-7000-a000-000000000001");
}
