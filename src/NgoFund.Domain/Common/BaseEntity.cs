namespace NgoFund.Domain.Common;

/// <summary>
/// Root base type for every entity: a UUIDv7 primary key plus creation audit stamps.
/// Composed with <see cref="IUpdateAuditable"/> and <see cref="ISoftDeletable"/> for entities
/// that also need update tracking / soft delete — not every entity needs both (e.g. the
/// append-only fund ledger needs neither).
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>User id of the creator. Null for system/seed-generated rows.</summary>
    public Guid? CreatedBy { get; set; }
}
