using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class ApplicationRemarkConfiguration : IEntityTypeConfiguration<ApplicationRemark>
{
    public void Configure(EntityTypeBuilder<ApplicationRemark> builder)
    {
        builder.ToTable("application_remarks");

        builder.Property(e => e.Remark).HasMaxLength(2000).IsRequired();

        builder.HasIndex(e => e.ApplicationId);

        builder.HasOne(e => e.Application)
            .WithMany(a => a.Remarks)
            .HasForeignKey(e => e.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
