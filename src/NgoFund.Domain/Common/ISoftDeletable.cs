namespace NgoFund.Domain.Common;

/// <summary>
/// Opt-in for entities that are soft-deleted instead of removed from the table. Applied via a
/// single global EF Core query filter — never a per-query WHERE clause. Financial/workflow
/// records (donations, payments, applications, the fund ledger) do NOT implement this; they are
/// voided/reversed instead, which preserves the audit trail.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
}
