using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NgoFund.Application.Abstractions;
using NgoFund.Domain.Common;
using NgoFund.Domain.Entities;
using NgoFund.Infrastructure.Identity;
using NgoFund.Infrastructure.Persistence.Auditing;

namespace NgoFund.Infrastructure.Persistence.Interceptors;

/// <summary>
/// The single place that stamps CreatedAt/CreatedBy/UpdatedAt/UpdatedBy, turns a hard delete of
/// an <see cref="ISoftDeletable"/> entity into a soft delete, and writes one <see cref="AuditLog"/>
/// row per changed entity — applied once here instead of being duplicated across every service
/// that calls SaveChanges.
/// </summary>
public class AuditSaveChangesInterceptor(ICurrentUserService currentUser) : SaveChangesInterceptor
{
    /// <summary>Entity types that are pure plumbing (login sessions, number-sequence counters) —
    /// never worth an audit row even when they change. Audit-column stamping still runs for these
    /// first when applicable; this only suppresses the <see cref="AuditLog"/> row itself.</summary>
    private static readonly HashSet<Type> NeverAudited = [typeof(RefreshToken), typeof(NumberSequence)];

    /// <summary>Values written as <c>"***"</c> in the forensic jsonb instead of the real secret —
    /// the property's presence/change stays visible, the value never leaks into audit_logs.</summary>
    private static readonly HashSet<string> RedactedProperties =
        new(StringComparer.Ordinal) { "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "TokenHash", "ReplacedByTokenHash" };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var auditLogs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog)
            {
                continue; // never audit the audit log itself
            }

            var neverAudited = NeverAudited.Contains(entry.Entity.GetType());

            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity is BaseEntity added)
                    {
                        added.CreatedAt = now;
                        added.CreatedBy = currentUser.UserId;
                    }

                    if (!neverAudited)
                    {
                        auditLogs.Add(BuildLog(entry, "Create", now));
                    }

                    break;

                case EntityState.Modified:
                    if (entry.Entity is IUpdateAuditable updated)
                    {
                        updated.UpdatedAt = now;
                        updated.UpdatedBy = currentUser.UserId;
                    }

                    if (!neverAudited && !AuditNoiseFilter.IsNoiseOnlyChange(entry))
                    {
                        auditLogs.Add(BuildLog(entry, "Update", now));
                    }

                    break;

                case EntityState.Deleted:
                    if (entry.Entity is ISoftDeletable softDeletable)
                    {
                        entry.State = EntityState.Modified;
                        softDeletable.IsDeleted = true;
                        softDeletable.DeletedAt = now;
                        softDeletable.DeletedBy = currentUser.UserId;

                        if (!neverAudited)
                        {
                            auditLogs.Add(BuildLog(entry, "SoftDelete", now));
                        }
                    }
                    else if (!neverAudited)
                    {
                        auditLogs.Add(BuildLog(entry, "Delete", now));
                    }

                    break;
            }
        }

        foreach (var log in auditLogs)
        {
            context.Set<AuditLog>().Add(log);
        }
    }

    private AuditLog BuildLog(EntityEntry entry, string action, DateTimeOffset now)
    {
        var keyValue = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue;

        // "actually changed" (OriginalValue != CurrentValue), not just IsModified — Identity's EF
        // UserStore calls Context.Update(user) internally on every UserManager.UpdateAsync call,
        // which blanket-flags every property Modified even when only LastLoginAt really changed.
        IReadOnlyList<PropertyEntry> changedProperties = action switch
        {
            "Update" or "SoftDelete" => entry.GetActuallyChangedProperties(),
            _ => entry.Properties.ToList(),
        };

        var narration = ActivityNarrator.Narrate(entry, action);

        return new AuditLog
        {
            UserId = currentUser.UserId,
            UserName = currentUser.UserName,
            Action = action,
            EntityName = entry.Entity.GetType().Name,
            EntityId = keyValue?.ToString() ?? string.Empty,
            OldValues = action is "Update" or "SoftDelete"
                ? JsonSerializer.Serialize(changedProperties.ToDictionary(p => p.Metadata.Name, p => Redact(p.Metadata.Name, p.OriginalValue)))
                : null,
            NewValues = action is "Create" or "Update" or "SoftDelete"
                ? JsonSerializer.Serialize(changedProperties.ToDictionary(p => p.Metadata.Name, p => Redact(p.Metadata.Name, p.CurrentValue)))
                : null,
            ChangedColumns = changedProperties.Select(p => p.Metadata.Name).ToArray(),
            IpAddress = TryParseIp(currentUser.IpAddress),
            MachineName = currentUser.MachineName,
            OccurredAt = now,
            EntityLabel = narration?.EntityLabel,
            EntityNumber = narration?.EntityNumber,
            Verb = narration?.Verb,
            Summary = narration?.Summary,
        };
    }

    private static object? Redact(string propertyName, object? value) =>
        value is not null && RedactedProperties.Contains(propertyName) ? "***" : value;

    private static IPAddress? TryParseIp(string? ip) => IPAddress.TryParse(ip, out var parsed) ? parsed : null;
}
