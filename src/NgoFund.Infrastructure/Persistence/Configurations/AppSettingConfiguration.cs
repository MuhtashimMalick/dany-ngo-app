using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("app_settings");

        builder.Property(e => e.Key).HasMaxLength(100).IsRequired();
        builder.Property(e => e.DataType).HasMaxLength(20).IsRequired();

        builder.HasIndex(e => e.Key).IsUnique();
    }
}
