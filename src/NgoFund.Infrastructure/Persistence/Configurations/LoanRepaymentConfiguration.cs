using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class LoanRepaymentConfiguration : IEntityTypeConfiguration<LoanRepayment>
{
    public void Configure(EntityTypeBuilder<LoanRepayment> builder)
    {
        builder.ToTable("loan_repayments", t => t.HasCheckConstraint("ck_loan_repayments_amount_positive", "amount > 0"));

        builder.Property(e => e.RepaymentNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.InstrumentNumber).HasMaxLength(100);
        builder.Property(e => e.BankName).HasMaxLength(150);
        builder.Property(e => e.ReceivedFromName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ReceivedFromCnic).HasMaxLength(15);
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.VoidReason).HasMaxLength(500);

        builder.HasIndex(e => e.RepaymentNumber).IsUnique();
        builder.HasIndex(e => e.LoanAgreementId);

        builder.HasOne(e => e.LoanAgreement)
            .WithMany(a => a.Repayments)
            .HasForeignKey(e => e.LoanAgreementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
