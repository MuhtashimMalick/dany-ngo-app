using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserName).HasMaxLength(200);
        builder.Property(e => e.Action).HasMaxLength(20).IsRequired();
        builder.Property(e => e.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.MachineName).HasMaxLength(100);

        // jsonb, not text, so old/new values are queryable in raw SQL reports.
        builder.Property(e => e.OldValues).HasColumnType("jsonb");
        builder.Property(e => e.NewValues).HasColumnType("jsonb");

        builder.HasIndex(e => new { e.EntityName, e.EntityId });
        builder.HasIndex(e => e.OccurredAt).IsDescending();
        builder.HasIndex(e => e.UserId);
    }
}
