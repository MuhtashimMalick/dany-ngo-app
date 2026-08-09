namespace NgoFund.Domain.Entities;

/// <summary>
/// One row per data-changing operation, written by <c>AuditSaveChangesInterceptor</c>. Immutable
/// — never updated, never deleted, so it deliberately does not derive from
/// <c>BaseEntity</c>/<c>IUpdateAuditable</c>/<c>ISoftDeletable</c>; its shape (OccurredAt vs
/// CreatedAt, UserId snapshot vs CreatedBy FK) is intentionally self-contained.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid? UserId { get; set; }

    /// <summary>Snapshot of the acting user's display name at the time of the action, so the log stays readable even if the user is later renamed/deleted.</summary>
    public string? UserName { get; set; }

    /// <summary>"Create" | "Update" | "Delete" | "SoftDelete".</summary>
    public string Action { get; set; } = null!;

    public string EntityName { get; set; } = null!;

    public string EntityId { get; set; } = null!;

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string[]? ChangedColumns { get; set; }

    public System.Net.IPAddress? IpAddress { get; set; }

    public string? MachineName { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
