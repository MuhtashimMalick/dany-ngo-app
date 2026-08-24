using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class DonationConfiguration : IEntityTypeConfiguration<Donation>
{
    public void Configure(EntityTypeBuilder<Donation> builder)
    {
        builder.ToTable("donations", t => t.HasCheckConstraint("ck_donations_amount_positive", "amount > 0"));

        builder.Property(e => e.DonationNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.BankName).HasMaxLength(150);
        builder.Property(e => e.InstrumentNumber).HasMaxLength(100);
        builder.Property(e => e.VoidReason).HasMaxLength(500);

        builder.HasIndex(e => e.DonationNumber).IsUnique();
        builder.HasIndex(e => e.DonationDate);

        builder.HasOne(e => e.Donor)
            .WithMany(d => d.Donations)
            .HasForeignKey(e => e.DonorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.FundCategory)
            .WithMany()
            .HasForeignKey(e => e.FundCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
