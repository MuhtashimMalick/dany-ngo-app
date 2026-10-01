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
            "num_nonnulls(applicant_id, application_id, donation_id, payment_id, application_guarantor_id) = 1"));

        builder.Property(e => e.FileName).HasMaxLength(260).IsRequired();
        builder.Property(e => e.StorageKey).HasMaxLength(260).IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.SlotKey).HasMaxLength(60);
        builder.Property(e => e.ExternalFileReference).HasMaxLength(200);

        builder.HasIndex(e => e.StorageKey).IsUnique();
        builder.HasIndex(e => new { e.ApplicationId, e.SlotKey });
        builder.HasIndex(e => new { e.ApplicationGuarantorId, e.SlotKey });

        // A6: upload idempotency for Google Form intake — Apps Script retries a submission's file
        // uploads on every 15-minute retry pass until they've all succeeded; the Drive file id lets
        // a retried upload resolve to the existing row instead of creating a duplicate.
        builder.HasIndex(e => e.ExternalFileReference).IsUnique().HasFilter("external_file_reference IS NOT NULL");

        // No explicit HasIndex() for the five owner FK columns below — EF Core's convention
        // already creates a non-unique index backing every FK by default. An earlier explicit
        // `HasIndex(e => e.ApplicationId)` call here was redundant and, once this table grew
        // several sibling 1:1 relationships elsewhere in the model (the details tables below), it
        // started merging into a wrongly-unique index; removing the redundant call fixed it.

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

        builder.HasOne(e => e.ApplicationGuarantor)
            .WithMany()
            .HasForeignKey(e => e.ApplicationGuarantorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
