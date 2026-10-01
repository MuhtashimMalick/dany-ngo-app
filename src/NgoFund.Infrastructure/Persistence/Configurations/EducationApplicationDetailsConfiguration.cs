using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Contracts.Common;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class EducationApplicationDetailsConfiguration : IEntityTypeConfiguration<EducationApplicationDetails>
{
    public void Configure(EntityTypeBuilder<EducationApplicationDetails> builder)
    {
        builder.ToTable("education_application_details", t =>
        {
            t.HasCheckConstraint("ck_education_details_attendance_pct_range",
                "previous_year_attendance_percent IS NULL OR (previous_year_attendance_percent >= 0 AND previous_year_attendance_percent <= 100)");
            t.HasCheckConstraint("ck_education_details_attendance_days_nonneg", "total_attendance_days IS NULL OR total_attendance_days >= 0");
            t.HasCheckConstraint("ck_education_details_academic_days_positive", "total_academic_days IS NULL OR total_academic_days > 0");
            t.HasCheckConstraint("ck_education_details_attendance_le_academic_days",
                "total_attendance_days IS NULL OR total_academic_days IS NULL OR total_attendance_days <= total_academic_days");
            t.HasCheckConstraint("ck_education_details_mother_income_nonneg", "mother_monthly_income IS NULL OR mother_monthly_income >= 0");
        });

        // Shared PK, true 1:1 with applications — same pattern as HousingApplicationDetailsConfiguration.
        builder.HasKey(e => e.ApplicationId);

        builder.Property(e => e.CensusNumber).HasMaxLength(50);
        builder.Property(e => e.StudentName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.WmoId).HasMaxLength(50);
        builder.Property(e => e.StudentMobile).HasMaxLength(12);
        builder.Property(e => e.CurrentClass).HasMaxLength(50).IsRequired();
        builder.Property(e => e.PreviousClass).HasMaxLength(50);
        builder.Property(e => e.LastExamTotalMarks).HasMaxLength(50);
        builder.Property(e => e.LastExamMarksObtained).HasMaxLength(50);
        builder.Property(e => e.FatherJamaat).HasMaxLength(100);
        builder.Property(e => e.MotherName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.MotherFatherName).HasMaxLength(200);
        builder.Property(e => e.MotherCaste).HasMaxLength(100);
        builder.Property(e => e.MotherJamaat).HasMaxLength(100);
        builder.Property(e => e.MotherMembershipNumber).HasMaxLength(PakistaniFormats.JamaatMembershipMaxLength);
        builder.Property(e => e.MotherCnic).HasMaxLength(15);
        builder.Property(e => e.MotherMobile).HasMaxLength(12);
        builder.Property(e => e.MotherProfession).HasMaxLength(100);

        builder.HasOne(e => e.Application)
            .WithOne(a => a.EducationDetails)
            .HasForeignKey<EducationApplicationDetails>(e => e.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
