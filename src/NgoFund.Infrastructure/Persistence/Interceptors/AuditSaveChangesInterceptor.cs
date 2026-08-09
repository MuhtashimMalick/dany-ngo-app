using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NgoFund.Application.Abstractions;
using NgoFund.Domain.Common;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Interceptors;

/// <summary>
/// The single place that stamps CreatedAt/CreatedBy/UpdatedAt/UpdatedBy, turns a hard delete of
/// an <see cref="ISoftDeletable"/> entity into a soft delete, and writes one <see cref="AuditLog"/>
/// row per changed entity — applied once here instead of being duplicated across every service
/// that calls SaveChanges.
/// </summary>
public class AuditSaveChangesInterceptor(ICurrentUserService currentUser) : SaveChangesInterceptor
{
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

            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity is BaseEntity added)
                    {
                        added.CreatedAt = now;
                        added.CreatedBy = currentUser.UserId;
                    }
                    auditLogs.Add(BuildLog(entry, "Create", now));
                    break;

                case EntityState.Modified:
                    if (entry.Entity is IUpdateAuditable updated)
                    {
                        updated.UpdatedAt = now;
                        updated.UpdatedBy = currentUser.UserId;
                    }
                    auditLogs.Add(BuildLog(entry, "Update", now));
                    break;

                case EntityState.Deleted:
                    if (entry.Entity is ISoftDeletable softDeletable)
                    {
                        entry.State = EntityState.Modified;
                        softDeletable.IsDeleted = true;
                        softDeletable.DeletedAt = now;
                        softDeletable.DeletedBy = currentUser.UserId;
                        auditLogs.Add(BuildLog(entry, "SoftDelete", now));
                    }
                    else
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

        var changedProperties = action switch
        {
            "Update" or "SoftDelete" => entry.Properties.Where(p => p.IsModified).ToList(),
            _ => entry.Properties.ToList(),
        };

        return new AuditLog
        {
            UserId = currentUser.UserId,
            UserName = currentUser.UserName,
            Action = action,
            EntityName = entry.Entity.GetType().Name,
            EntityId = keyValue?.ToString() ?? string.Empty,
            OldValues = action is "Update" or "SoftDelete"
                ? JsonSerializer.Serialize(changedProperties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue))
                : null,
            NewValues = action is "Create" or "Update" or "SoftDelete"
                ? JsonSerializer.Serialize(changedProperties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue))
                : null,
            ChangedColumns = changedProperties.Select(p => p.Metadata.Name).ToArray(),
            IpAddress = TryParseIp(currentUser.IpAddress),
            MachineName = currentUser.MachineName,
            OccurredAt = now,
        };
    }

    private static IPAddress? TryParseIp(string? ip) => IPAddress.TryParse(ip, out var parsed) ? parsed : null;
}
