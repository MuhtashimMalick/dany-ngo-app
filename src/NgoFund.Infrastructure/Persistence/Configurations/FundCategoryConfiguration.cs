using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class FundCategoryConfiguration : IEntityTypeConfiguration<FundCategory>
{
    public void Configure(EntityTypeBuilder<FundCategory> builder)
    {
        builder.ToTable("fund_categories");

        builder.Property(e => e.Code).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(e => e.Code).IsUnique();
    }
}
