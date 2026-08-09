using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class FundTransactionConfiguration : IEntityTypeConfiguration<FundTransaction>
{
    public void Configure(EntityTypeBuilder<FundTransaction> builder)
    {
        builder.ToTable("fund_transactions", t => t.HasCheckConstraint("ck_fund_transactions_amount_positive", "amount > 0"));

        builder.Property(e => e.Description).HasMaxLength(500);

        // Guarantees exactly-once posting: the same donation/payment can never be posted twice.
        builder.HasIndex(e => new { e.ReferenceType, e.ReferenceId }).IsUnique();
        builder.HasIndex(e => e.FundCategoryId);
        builder.HasIndex(e => e.TransactionDate);

        builder.HasOne(e => e.FundCategory)
            .WithMany()
            .HasForeignKey(e => e.FundCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
