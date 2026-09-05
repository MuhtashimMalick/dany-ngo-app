using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.UnitTests.Services;

/// <summary>
/// Guards the client-confirmed final per-category FundEligibility mapping (v1.5 amendment, see
/// docs/scope.md) against an accidental data flip in
/// <see cref="NgoFund.Infrastructure.Persistence.Seed.ApplicationCategorySeed"/>. Reads the seed
/// data straight off the real <see cref="AppDbContext"/> model (built, never connected) so the
/// assertion exercises exactly what `dotnet ef database update`/the migrator would insert.
/// </summary>
public class ApplicationCategorySeedTests
{
    [Theory]
    [InlineData("SHAADI", FundEligibility.ZakatOnly)]
    [InlineData("HEALTH", FundEligibility.ZakatOnly)]
    [InlineData("EDUCATION", FundEligibility.ZakatOnly)]
    [InlineData("HOUSE_RENT", FundEligibility.ZakatOnly)]
    [InlineData("EMERGENCY", FundEligibility.ZakatOnly)]
    [InlineData("ROZGAR", FundEligibility.GeneralOnly)]
    [InlineData("OTHER", FundEligibility.Either)]
    public void Apply_SeedsExpectedFundEligibility(string code, FundEligibility expected)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=ngofund_model_probe")
            .Options;
        using var db = new AppDbContext(options);

        var designTimeModel = db.GetService<IDesignTimeModel>().Model;
        var seedData = designTimeModel.FindEntityType(typeof(ApplicationCategory))!.GetSeedData();
        var row = Assert.Single(seedData, r => (string)r["Code"]! == code);

        Assert.Equal(expected, (FundEligibility)row["FundEligibility"]!);
    }
}
