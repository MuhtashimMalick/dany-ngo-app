using Microsoft.EntityFrameworkCore;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Seed;

/// <summary>The two fund categories named in the client scope document.</summary>
internal static class FundCategorySeed
{
    public static readonly Guid ZakatId = DeterministicGuid.From("fund-category:ZAKAT");
    public static readonly Guid GeneralId = DeterministicGuid.From("fund-category:GENERAL");

    public static void Apply(ModelBuilder builder)
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        builder.Entity<FundCategory>().HasData(
            new FundCategory
            {
                Id = ZakatId, Code = "ZAKAT", Name = "Zakat Fund",
                Description = "Zakat-eligible donations, disbursed only to Zakat-eligible application categories.",
                IsZakat = true, IsActive = true, DisplayOrder = 1, CreatedAt = now,
            },
            new FundCategory
            {
                Id = GeneralId, Code = "GENERAL", Name = "General Fund",
                Description = "General donations, may fund any application category.",
                IsZakat = false, IsActive = true, DisplayOrder = 2, CreatedAt = now,
            });
    }
}
