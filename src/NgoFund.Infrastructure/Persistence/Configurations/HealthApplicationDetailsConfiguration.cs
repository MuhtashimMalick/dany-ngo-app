using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class HealthApplicationDetailsConfiguration : IEntityTypeConfiguration<HealthApplicationDetails>
{
    public void Configure(EntityTypeBuilder<HealthApplicationDetails> builder)
    {
        builder.ToTable("health_application_details", t =>
            t.HasCheckConstraint("ck_health_details_applicant_age_range", "applicant_age IS NULL OR (applicant_age >= 0 AND applicant_age <= 150)"));

        // Shared PK, true 1:1 with applications — same pattern as HousingApplicationDetailsConfiguration.
        builder.HasKey(e => e.ApplicationId);

        builder.HasOne(e => e.Application)
            .WithOne(a => a.HealthDetails)
            .HasForeignKey<HealthApplicationDetails>(e => e.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
