using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class ApplicantConfiguration : IEntityTypeConfiguration<Applicant>
{
    public void Configure(EntityTypeBuilder<Applicant> builder)
    {
        builder.ToTable("applicants");

        builder.Property(e => e.MembershipNumber).HasMaxLength(30);
        builder.Property(e => e.FullName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.FatherOrHusbandName).HasMaxLength(200);
        builder.Property(e => e.Cnic).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(30);
        builder.Property(e => e.AlternatePhone).HasMaxLength(30);
        builder.Property(e => e.Email).HasMaxLength(200);
        builder.Property(e => e.City).HasMaxLength(100);
        builder.Property(e => e.District).HasMaxLength(100);
        builder.Property(e => e.Province).HasMaxLength(100);
        builder.Property(e => e.Occupation).HasMaxLength(150);
        builder.Property(e => e.BlacklistReason).HasMaxLength(500);

        builder.HasIndex(e => e.Cnic).IsUnique();
        builder.HasIndex(e => e.MembershipNumber).IsUnique().HasFilter("membership_number IS NOT NULL");
        builder.HasIndex(e => e.Phone);

        // Fuzzy name search — requires the pg_trgm extension, enabled in the initial migration.
        builder.HasIndex(e => e.FullName).HasMethod("gin").HasOperators("gin_trgm_ops");

        builder.HasOne(e => e.PhotoDocument)
            .WithMany()
            .HasForeignKey(e => e.PhotoDocumentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
