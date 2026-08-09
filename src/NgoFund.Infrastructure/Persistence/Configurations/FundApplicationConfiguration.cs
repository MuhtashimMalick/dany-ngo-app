using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class FundApplicationConfiguration : IEntityTypeConfiguration<FundApplication>
{
    public void Configure(EntityTypeBuilder<FundApplication> builder)
    {
        builder.ToTable("applications", t =>
        {
            t.HasCheckConstraint("ck_applications_requested_amount_positive", "requested_amount > 0");
            t.HasCheckConstraint("ck_applications_approved_amount_le_requested", "approved_amount IS NULL OR approved_amount <= requested_amount");
        });

        builder.Property(e => e.ApplicationNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Purpose).HasMaxLength(1000);
        builder.Property(e => e.RejectionReason).HasMaxLength(500);

        builder.HasIndex(e => e.ApplicationNumber).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.ApplicationDate);

        builder.HasOne(e => e.Applicant)
            .WithMany(a => a.Applications)
            .HasForeignKey(e => e.ApplicantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ApplicationCategory)
            .WithMany()
            .HasForeignKey(e => e.ApplicationCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.FundCategory)
            .WithMany()
            .HasForeignKey(e => e.FundCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
