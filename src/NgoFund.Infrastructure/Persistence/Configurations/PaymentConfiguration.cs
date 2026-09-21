using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", t => t.HasCheckConstraint("ck_payments_amount_positive", "amount > 0"));

        builder.Property(e => e.PaymentNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.InstrumentNumber).HasMaxLength(100);
        builder.Property(e => e.BankName).HasMaxLength(150);
        builder.Property(e => e.VoidReason).HasMaxLength(500);

        builder.HasIndex(e => e.PaymentNumber).IsUnique();
        builder.HasIndex(e => e.PaymentDate);
        builder.HasIndex(e => e.ApplicationId);

        builder.HasOne(e => e.Application)
            .WithMany(a => a.Payments)
            .HasForeignKey(e => e.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.FundCategory)
            .WithMany()
            .HasForeignKey(e => e.FundCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
