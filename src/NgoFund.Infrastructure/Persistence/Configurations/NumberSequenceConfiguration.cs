using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        builder.ToTable("number_sequences");

        builder.Property(e => e.EntityType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Prefix).HasMaxLength(10).IsRequired();

        // One counter per (entity type, year) — this is what INumberGenerator locks via
        // `SELECT ... FOR UPDATE` to hand out collision-free numbers under concurrency.
        builder.HasIndex(e => new { e.EntityType, e.Year }).IsUnique();
    }
}
