using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class LoanInstallmentConfiguration : IEntityTypeConfiguration<LoanInstallment>
{
    public void Configure(EntityTypeBuilder<LoanInstallment> builder)
    {
        builder.ToTable("loan_installments", t =>
        {
            t.HasCheckConstraint("ck_loan_installments_amount_positive", "amount_due > 0");
            t.HasCheckConstraint("ck_loan_installments_sequence_positive", "sequence_no > 0");
        });

        builder.Property(e => e.SequenceNumber).HasColumnName("sequence_no");

        builder.HasIndex(e => new { e.LoanAgreementId, e.SequenceNumber }).IsUnique();

        builder.HasOne(e => e.LoanAgreement)
            .WithMany(a => a.Installments)
            .HasForeignKey(e => e.LoanAgreementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
