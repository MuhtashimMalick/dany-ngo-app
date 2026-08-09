using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents", t => t.HasCheckConstraint(
            "ck_documents_exactly_one_owner",
            "num_nonnulls(applicant_id, application_id, donation_id, payment_id) = 1"));

        builder.Property(e => e.FileName).HasMaxLength(260).IsRequired();
        builder.Property(e => e.StorageKey).HasMaxLength(260).IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);

        builder.HasIndex(e => e.StorageKey).IsUnique();
        builder.HasIndex(e => e.ApplicantId);
        builder.HasIndex(e => e.ApplicationId);
        builder.HasIndex(e => e.DonationId);
        builder.HasIndex(e => e.PaymentId);

        // Restrict, not Cascade: deleting an applicant/application/etc. must not silently delete
        // its documents — soft-delete the owner and keep the file record intact.
        builder.HasOne(e => e.Applicant)
            .WithMany()
            .HasForeignKey(e => e.ApplicantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Application)
            .WithMany()
            .HasForeignKey(e => e.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Donation)
            .WithMany()
            .HasForeignKey(e => e.DonationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Payment)
            .WithMany()
            .HasForeignKey(e => e.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
