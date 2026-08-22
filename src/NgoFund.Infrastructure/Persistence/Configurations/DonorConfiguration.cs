using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Contracts.Common;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class DonorConfiguration : IEntityTypeConfiguration<Donor>
{
    public void Configure(EntityTypeBuilder<Donor> builder)
    {
        builder.ToTable("donors");

        builder.Property(e => e.DonorCode).HasMaxLength(30).IsRequired();
        builder.Property(e => e.FullName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Cnic).HasMaxLength(15);
        builder.Property(e => e.Ntn).HasMaxLength(30);
        builder.Property(e => e.MembershipNumber).HasMaxLength(PakistaniFormats.JamaatMembershipMaxLength);
        builder.Property(e => e.Phone).HasMaxLength(12);
        builder.Property(e => e.AlternatePhone).HasMaxLength(12);
        builder.Property(e => e.Email).HasMaxLength(200);
        builder.Property(e => e.City).HasMaxLength(100);
        builder.Property(e => e.Country).HasMaxLength(100);

        // NOTE: filter predicates use the final snake_case column name (set by
        // EFCore.NamingConventions), not the C# property name — HasFilter is a raw SQL string
        // the naming-convention plugin does not rewrite.
        builder.HasIndex(e => e.DonorCode).IsUnique();
        builder.HasIndex(e => e.Cnic).IsUnique().HasFilter("cnic IS NOT NULL");
        builder.HasIndex(e => e.MembershipNumber).IsUnique().HasFilter("membership_number IS NOT NULL");
        builder.HasIndex(e => e.Phone);
    }
}
