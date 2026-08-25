using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Entities;

/// <summary>
/// One row per data-changing operation, written by <c>AuditSaveChangesInterceptor</c>. Immutable
/// — never updated, never deleted, so it deliberately does not derive from
/// <c>BaseEntity</c>/<c>IUpdateAuditable</c>/<c>ISoftDeletable</c>; its shape (OccurredAt vs
/// CreatedAt, UserId snapshot vs CreatedBy FK) is intentionally self-contained.
///
/// Two audiences share this one row:
///  - Forensic: <see cref="Action"/>, <see cref="EntityName"/>/<see cref="EntityId"/>,
///    <see cref="OldValues"/>/<see cref="NewValues"/>/<see cref="ChangedColumns"/> — raw EF-level
///    detail (C# type/property names, GUIDs, a redacted jsonb diff), for admins/support digging
///    into exactly what changed on a given row.
///  - Client-facing: <see cref="EntityLabel"/>/<see cref="EntityNumber"/>/<see cref="Verb"/>/
///    <see cref="Summary"/> — a human sentence ("Donation "DON-2026-00001" was recorded"),
///    populated only for the entities/triggers the Infrastructure-layer <c>ActivityNarrator</c>
///    recognizes. The
///    client-facing activity feed is every row where <see cref="Summary"/> is not null; rows with
///    no narration (noise, or an entity nobody asked to see in the feed) stay in the table for
///    forensics but never surface there.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid? UserId { get; set; }

    /// <summary>Snapshot of the acting user's display name at the time of the action, so the log stays readable even if the user is later renamed/deleted.</summary>
    public string? UserName { get; set; }

    /// <summary>"Create" | "Update" | "Delete" | "SoftDelete" — the EF-level operation. Forensic only, never shown to the client.</summary>
    public string Action { get; set; } = null!;

    public string EntityName { get; set; } = null!;

    public string EntityId { get; set; } = null!;

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string[]? ChangedColumns { get; set; }

    public System.Net.IPAddress? IpAddress { get; set; }

    public string? MachineName { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Human-readable kind of thing that changed, e.g. "Donation", "Applicant". Null when the change wasn't recognized/narrated.</summary>
    public string? EntityLabel { get; set; }

    /// <summary>The entity's own human-facing number/code (DonationNumber, ApplicationNumber, ...), not its GUID. Null when the entity has none or narration didn't run.</summary>
    public string? EntityNumber { get; set; }

    /// <summary>The client-facing business verb, distinct from <see cref="Action"/>'s EF-level operation. Null when narration didn't run.</summary>
    public ActivityVerb? Verb { get; set; }

    /// <summary>The one-sentence narration shown in the client-facing activity feed. Null means this row is forensic-only and invisible to that feed.</summary>
    public string? Summary { get; set; }
}
