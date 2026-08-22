using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class LoanAgreementConfiguration : IEntityTypeConfiguration<LoanAgreement>
{
    public void Configure(EntityTypeBuilder<LoanAgreement> builder)
    {
        builder.ToTable("loan_agreements", t => t.HasCheckConstraint("ck_loan_agreements_principal_positive", "principal_amount > 0"));

        builder.Property(e => e.LoanNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.CancelReason).HasMaxLength(500);
        builder.Property(e => e.WrittenOffReason).HasMaxLength(500);

        builder.HasIndex(e => e.LoanNumber).IsUnique();

        // One Active agreement per application at a time — historical Cancelled/WrittenOff ones
        // are allowed to accumulate. Filter uses the final snake_case column/enum-string values.
        builder.HasIndex(e => e.ApplicationId)
            .IsUnique()
            .HasFilter($"status = '{nameof(LoanAgreementStatus.Active)}'");

        builder.HasOne(e => e.Application)
            .WithMany()
            .HasForeignKey(e => e.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.FundCategory)
            .WithMany()
            .HasForeignKey(e => e.FundCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
