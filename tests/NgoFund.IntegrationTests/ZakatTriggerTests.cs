using Microsoft.EntityFrameworkCore;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the `fn_enforce_zakat_eligibility` DB trigger (the defence-in-depth half of the Zakat
/// rule — <see cref="NgoFund.Domain.Entities.FundApplication.EnsureFundIsCompatible"/> is the
/// other half, covered by unit tests) against a real, freshly migrated PostgreSQL 18 instance —
/// the same image and migrations docker-compose uses.
/// </summary>
public class ZakatTriggerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();

    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString(), npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        _db = new AppDbContext(options);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _container.DisposeAsync();
    }

    private async Task<Guid> SeedApplicantAsync()
    {
        var applicant = new Applicant
        {
            Cnic = "12345-1234567-1",
            FullName = "Test Applicant",
            Gender = Gender.Male,
        };
        _db.Applicants.Add(applicant);
        await _db.SaveChangesAsync();
        return applicant.Id;
    }

    private async Task<(Guid RozgarCategoryId, Guid ZakatFundId, Guid GeneralFundId)> LoadSeededIdsAsync()
    {
        var rozgarId = await _db.ApplicationCategories.Where(c => c.Code == "ROZGAR").Select(c => c.Id).SingleAsync();
        var zakatId = await _db.FundCategories.Where(f => f.Code == "ZAKAT").Select(f => f.Id).SingleAsync();
        var generalId = await _db.FundCategories.Where(f => f.Code == "GENERAL").Select(f => f.Id).SingleAsync();
        return (rozgarId, zakatId, generalId);
    }

    [Fact]
    public async Task NonZakatEligibleCategory_AgainstZakatFund_IsRejectedByTrigger()
    {
        var applicantId = await SeedApplicantAsync();
        var (rozgarId, zakatId, _) = await LoadSeededIdsAsync();

        _db.Applications.Add(new FundApplication
        {
            ApplicationNumber = "TEST-0001",
            ApplicantId = applicantId,
            ApplicationCategoryId = rozgarId,
            FundCategoryId = zakatId,
            RequestedAmount = 1000m,
            ApplicationDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task NonZakatEligibleCategory_AgainstGeneralFund_Succeeds()
    {
        var applicantId = await SeedApplicantAsync();
        var (rozgarId, _, generalId) = await LoadSeededIdsAsync();

        _db.Applications.Add(new FundApplication
        {
            ApplicationNumber = "TEST-0002",
            ApplicantId = applicantId,
            ApplicationCategoryId = rozgarId,
            FundCategoryId = generalId,
            RequestedAmount = 1000m,
            ApplicationDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });

        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.Applications.CountAsync());
    }
}
