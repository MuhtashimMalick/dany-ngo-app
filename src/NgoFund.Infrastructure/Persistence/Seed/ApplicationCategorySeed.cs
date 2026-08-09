using Microsoft.EntityFrameworkCore;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Seed;

/// <summary>
/// The application categories from the client scope document. "- Z" marked categories are
/// Zakat-eligible; Rozgar/Business Help and Other are not, per the client's explicit ruling on
/// the Zakat rule.
/// </summary>
internal static class ApplicationCategorySeed
{
    public static readonly Guid ShaadiId = DeterministicGuid.From("application-category:SHAADI");
    public static readonly Guid HealthId = DeterministicGuid.From("application-category:HEALTH");
    public static readonly Guid EducationId = DeterministicGuid.From("application-category:EDUCATION");
    public static readonly Guid HouseRentId = DeterministicGuid.From("application-category:HOUSE_RENT");
    public static readonly Guid EmergencyId = DeterministicGuid.From("application-category:EMERGENCY");
    public static readonly Guid RozgarId = DeterministicGuid.From("application-category:ROZGAR");
    public static readonly Guid OtherId = DeterministicGuid.From("application-category:OTHER");

    public static void Apply(ModelBuilder builder)
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        (Guid Id, string Code, string Name, bool IsZakatEligible, int Order)[] rows =
        [
            (ShaadiId, "SHAADI", "Shaadi Fund Request", true, 1),
            (HealthId, "HEALTH", "Health Fund Request", true, 2),
            (EducationId, "EDUCATION", "Education Support", true, 3),
            (HouseRentId, "HOUSE_RENT", "House Rent/Help", true, 4),
            (EmergencyId, "EMERGENCY", "Emergency Support", true, 5),
            (RozgarId, "ROZGAR", "Rozgar/Business Help", false, 6),
            (OtherId, "OTHER", "Other", false, 7),
        ];

        builder.Entity<ApplicationCategory>().HasData(rows.Select(r => new ApplicationCategory
        {
            Id = r.Id,
            Code = r.Code,
            Name = r.Name,
            IsZakatEligible = r.IsZakatEligible,
            IsActive = true,
            DisplayOrder = r.Order,
            CreatedAt = now,
        }));
    }
}
