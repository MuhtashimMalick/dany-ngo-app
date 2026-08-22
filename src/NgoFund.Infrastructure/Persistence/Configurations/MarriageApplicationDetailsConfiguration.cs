using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class MarriageApplicationDetailsConfiguration : IEntityTypeConfiguration<MarriageApplicationDetails>
{
    public void Configure(EntityTypeBuilder<MarriageApplicationDetails> builder)
    {
        builder.ToTable("marriage_application_details", t => t.HasCheckConstraint(
            "ck_marriage_application_details_rukhsati_after_nikah",
            "rukhsati_date IS NULL OR nikah_date IS NULL OR rukhsati_date >= nikah_date"));

        builder.HasKey(e => e.ApplicationId);

        builder.Property(e => e.GuardianRelationshipToBride).HasMaxLength(100);
        builder.Property(e => e.BrideName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.BrideFatherName).HasMaxLength(200);
        builder.Property(e => e.BrideFamilyName).HasMaxLength(200);
        builder.Property(e => e.BrideCnic).HasMaxLength(15);
        builder.Property(e => e.BridePreviousHusbandName).HasMaxLength(200);
        builder.Property(e => e.BrideJamaat).HasMaxLength(150);
        builder.Property(e => e.BridePriorTrustAssistance).HasColumnType("text");

        builder.Property(e => e.GroomName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.GroomFatherName).HasMaxLength(200);
        builder.Property(e => e.GroomGrandfatherName).HasMaxLength(200);
        builder.Property(e => e.GroomJamaat).HasMaxLength(150);
        builder.Property(e => e.GroomPreviousWifeName).HasMaxLength(200);
        builder.Property(e => e.GroomAddress).HasColumnType("text");
        builder.Property(e => e.GroomMobile).HasMaxLength(12);
        builder.Property(e => e.GroomBusinessAddress).HasColumnType("text");

        builder.HasIndex(e => e.BrideCnic);

        builder.HasOne(e => e.Application)
            .WithOne(a => a.MarriageDetails)
            .HasForeignKey<MarriageApplicationDetails>(e => e.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
