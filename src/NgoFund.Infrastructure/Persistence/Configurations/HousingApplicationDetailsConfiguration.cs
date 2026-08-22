using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class HousingApplicationDetailsConfiguration : IEntityTypeConfiguration<HousingApplicationDetails>
{
    public void Configure(EntityTypeBuilder<HousingApplicationDetails> builder)
    {
        builder.ToTable("housing_application_details");

        // Shared PK, true 1:1 with applications — no separate uuid identity column.
        builder.HasKey(e => e.ApplicationId);

        builder.Property(e => e.PreviousResidentialAddress).HasColumnType("text");
        builder.Property(e => e.PreviousAssistanceDetails).HasColumnType("text");

        builder.HasOne(e => e.Application)
            .WithOne(a => a.HousingDetails)
            .HasForeignKey<HousingApplicationDetails>(e => e.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
