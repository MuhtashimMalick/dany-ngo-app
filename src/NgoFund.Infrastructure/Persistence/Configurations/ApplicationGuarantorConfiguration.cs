using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Contracts.Common;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class ApplicationGuarantorConfiguration : IEntityTypeConfiguration<ApplicationGuarantor>
{
    public void Configure(EntityTypeBuilder<ApplicationGuarantor> builder)
    {
        builder.ToTable("application_guarantors", t => t.HasCheckConstraint(
            "ck_application_guarantors_sequence_positive", "sequence_no > 0"));

        builder.Property(e => e.SequenceNumber).HasColumnName("sequence_no");

        builder.Property(e => e.MembershipNumber).HasMaxLength(PakistaniFormats.JamaatMembershipMaxLength);
        builder.Property(e => e.FullName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.FatherName).HasMaxLength(200);
        builder.Property(e => e.GrandfatherName).HasMaxLength(200);
        builder.Property(e => e.Surname).HasMaxLength(100);
        builder.Property(e => e.Cnic).HasMaxLength(15);
        builder.Property(e => e.ResidentialAddress).HasColumnType("text");
        builder.Property(e => e.BusinessAddress).HasColumnType("text");
        builder.Property(e => e.BusinessNature).HasMaxLength(200);
        builder.Property(e => e.PhoneHome).HasMaxLength(30);
        builder.Property(e => e.PhoneOffice).HasMaxLength(30);
        builder.Property(e => e.PhoneMobile).HasMaxLength(12);

        // Partial, not a plain unique index: ApplicationGuarantor is ISoftDeletable, and
        // ReplaceGuarantorsAsync soft-deletes guarantors dropped from the incoming list (via the
        // AuditSaveChangesInterceptor's Remove -> soft-delete conversion) — a hard unique index
        // here would collide with the still-present soft-deleted rows the moment a later save
        // reuses their sequence_no (e.g. a new guarantor 1 added after the old guarantor 1 was
        // removed). Same "unique when present" pattern  prescribes for
        // applicants.membership_number.
        builder.HasIndex(e => new { e.ApplicationId, e.SequenceNumber }).IsUnique().HasFilter("is_deleted = false");
        builder.HasIndex(e => e.Cnic);

        builder.HasOne(e => e.Application)
            .WithMany(a => a.Guarantors)
            .HasForeignKey(e => e.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
