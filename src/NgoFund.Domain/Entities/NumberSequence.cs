using NgoFund.Domain.Common;

namespace NgoFund.Domain.Entities;

/// <summary>
/// Backs human-readable document numbers (DNR-2026-00001, APP-2026-00001, ...). One row per
/// (entity_type, year); incremented under `SELECT ... FOR UPDATE` in
/// <c>INumberGenerator</c> so concurrent requests never collide.
/// </summary>
public class NumberSequence : BaseEntity, IUpdateAuditable
{
    /// <summary>e.g. "Donor", "Donation", "Application", "Payment".</summary>
    public string EntityType { get; set; } = null!;

    public string Prefix { get; set; } = null!;

    public int Year { get; set; }

    public long CurrentValue { get; set; }

    public int Padding { get; set; } = 5;

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
