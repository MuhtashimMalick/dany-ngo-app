using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs", t => t.HasCheckConstraint(
            "ck_audit_logs_verb",
            "verb IS NULL OR verb IN ('Created','Updated','Deleted','Recorded','Voided','StatusChanged','Cancelled','WrittenOff','Deactivated','Reactivated','Blacklisted','PermissionGranted','PermissionRevoked')"));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserName).HasMaxLength(200);
        builder.Property(e => e.Action).HasMaxLength(20).IsRequired();
        builder.Property(e => e.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.MachineName).HasMaxLength(100);

        // jsonb, not text, so old/new values are queryable in raw SQL reports.
        builder.Property(e => e.OldValues).HasColumnType("jsonb");
        builder.Property(e => e.NewValues).HasColumnType("jsonb");

        // Client-facing narration columns — see AuditLog's doc comment for the two-audience split.
        // Verb's HasConversion<string>()/HasMaxLength(20) come from the model-wide enum convention
        // in AppDbContext.ConfigureConventions, same as every other enum in the system.
        builder.Property(e => e.EntityLabel).HasMaxLength(60);
        builder.Property(e => e.EntityNumber).HasMaxLength(60);
        builder.Property(e => e.Summary).HasColumnType("text");

        builder.HasIndex(e => new { e.EntityName, e.EntityId });
        builder.HasIndex(e => e.OccurredAt).IsDescending();
        builder.HasIndex(e => e.UserId);

        // The client-facing activity feed's own index: `WHERE summary IS NOT NULL ORDER BY
        // occurred_at DESC` — every noisy/unnarrated row (summary IS NULL) never entering it keeps
        // it small regardless of how big the forensic table gets. A second index on the same
        // column needs a distinct model name passed to HasIndex itself (not just HasDatabaseName)
        // — otherwise EF treats it as reconfiguring the first, unfiltered OccurredAt index instead
        // of adding a new one; HasDatabaseName is still needed on top, since the model name alone
        // isn't what EF emits as the actual Postgres index name.
        builder.HasIndex(e => e.OccurredAt, "ix_audit_logs_activity")
            .IsDescending()
            .HasFilter("summary IS NOT NULL")
            .HasDatabaseName("ix_audit_logs_activity");
    }
}
