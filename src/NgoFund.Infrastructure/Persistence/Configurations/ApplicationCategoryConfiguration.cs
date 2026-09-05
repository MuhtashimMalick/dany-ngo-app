using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Configurations;

public class ApplicationCategoryConfiguration : IEntityTypeConfiguration<ApplicationCategory>
{
    public void Configure(EntityTypeBuilder<ApplicationCategory> builder)
    {
        builder.ToTable("application_categories", t => t.HasCheckConstraint(
            "ck_application_categories_fund_eligibility",
            "fund_eligibility IN ('ZakatOnly','GeneralOnly','Either')"));

        builder.Property(e => e.Code).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.TermsText).HasColumnType("text");
        builder.Property(e => e.TermsVersion).HasMaxLength(20);

        builder.HasIndex(e => e.Code).IsUnique();
    }
}
