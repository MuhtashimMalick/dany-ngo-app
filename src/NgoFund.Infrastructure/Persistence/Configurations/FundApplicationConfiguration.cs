using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class FundApplicationConfiguration : IEntityTypeConfiguration<FundApplication>
{
    public void Configure(EntityTypeBuilder<FundApplication> builder)
    {
        builder.ToTable("applications", t =>
        {
            // A4: nullable now — only ROZGAR's Google Form asks for an amount, so intake-created
            // applications for every other category legitimately have none yet.
            t.HasCheckConstraint("ck_applications_requested_amount_positive", "requested_amount IS NULL OR requested_amount > 0");
        });

        builder.Property(e => e.ApplicationNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Purpose).HasMaxLength(1000);
        builder.Property(e => e.RejectionReason).HasMaxLength(500);

        builder.Property(e => e.DeclaredResidentialAddress).HasColumnType("text");
        builder.Property(e => e.DeclaredBusinessAddress).HasColumnType("text");
        builder.Property(e => e.ExternalFormReference).HasMaxLength(100);
        builder.Property(e => e.TermsVersion).HasMaxLength(20);

        // Mirror direction of item 4's guarantor-row override (ApplicationGuarantorConfiguration):
        // ApplicantGuarantorConflictOverrideApprovedAt/By need no explicit config — snake_case
        // convention already names them correctly.
        builder.Property(e => e.ApplicantGuarantorConflictOverrideReason).HasMaxLength(500);
        builder.Property(e => e.ApplicantGuarantorConflictOverrideCnic).HasMaxLength(15);

        // Unlike this codebase's other enum columns (whose NOT NULL default comes purely from the
        // C# property initializer, fine for CREATE TABLE where no rows pre-exist), this one is
        // spec'd as an actual DB-level DEFAULT ('InApp') and is added via ALTER TABLE to an
        // already-existing table — without HasDefaultValue, EF backfills existing rows with ""
        // (CLR default), which isn't a valid ApplicationIntakeChannel value.
        builder.Property(e => e.IntakeChannel).HasDefaultValue(ApplicationIntakeChannel.InApp);

        builder.HasIndex(e => e.ApplicationNumber).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.ApplicationDate);

        // "Unique when present" per 's convention — a Google Form/paper submission can
        // only ever be re-keyed into one application.
        builder.HasIndex(e => e.ExternalFormReference).IsUnique().HasFilter("external_form_reference IS NOT NULL");

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
