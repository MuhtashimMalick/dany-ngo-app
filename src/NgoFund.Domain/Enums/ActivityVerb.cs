namespace NgoFund.Domain.Enums;

/// <summary>
/// The client-facing business verb for one narrated activity-log row (see
/// <see cref="Entities.AuditLog.Verb"/>) — distinct from <see cref="Entities.AuditLog.Action"/>,
/// which records the EF-level operation (Create/Update/Delete/SoftDelete). Stored as varchar +
/// CHECK, mapped with <c>HasConversion&lt;string&gt;()</c> per .
/// </summary>
public enum ActivityVerb
{
    Created,
    Updated,
    Deleted,
    Recorded,
    Voided,
    StatusChanged,
    Cancelled,
    WrittenOff,
    Deactivated,
    Reactivated,
    Blacklisted,
    PermissionGranted,
    PermissionRevoked
}
