using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class BusinessLoanApplicationDetailsConfiguration : IEntityTypeConfiguration<BusinessLoanApplicationDetails>
{
    public void Configure(EntityTypeBuilder<BusinessLoanApplicationDetails> builder)
    {
        builder.ToTable("business_loan_application_details", t =>
        {
            t.HasCheckConstraint("ck_business_loan_details_capital_required_nonneg", "capital_required >= 0");
            t.HasCheckConstraint("ck_business_loan_details_capital_available_nonneg", "capital_already_available >= 0");
            t.HasCheckConstraint("ck_business_loan_details_expenses_nonneg", "total_monthly_expenses >= 0");
        });

        builder.HasKey(e => e.ApplicationId);

        builder.Property(e => e.PaperFormNumber).HasMaxLength(50);
        builder.Property(e => e.BusinessPhone).HasMaxLength(30);
        builder.Property(e => e.Education).HasMaxLength(200);
        builder.Property(e => e.Skill).HasMaxLength(200);
        builder.Property(e => e.Experience).HasColumnType("text");
        builder.Property(e => e.OtherIncomeSources).HasColumnType("text");
        builder.Property(e => e.ProposedBusinessDescription).HasColumnType("text").IsRequired();
        builder.Property(e => e.ProposedBusinessLocation).HasColumnType("text");
        builder.Property(e => e.PriorBusinessDetails).HasColumnType("text");
        builder.Property(e => e.EmergencyContactName).HasMaxLength(200);
        builder.Property(e => e.EmergencyContactCnic).HasMaxLength(15);
        builder.Property(e => e.EmergencyContactPhone).HasMaxLength(30);

        builder.HasOne(e => e.Application)
            .WithOne(a => a.BusinessLoanDetails)
            .HasForeignKey<BusinessLoanApplicationDetails>(e => e.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
