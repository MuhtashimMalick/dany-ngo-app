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

    private async Task<Guid> LoadCategoryIdAsync(string code) =>
        await _db.ApplicationCategories.Where(c => c.Code == code).Select(c => c.Id).SingleAsync();

    private async Task InsertApplicationAsync(string applicationNumber, Guid applicantId, Guid categoryId, Guid fundId) =>
        _db.Applications.Add(new FundApplication
        {
            ApplicationNumber = applicationNumber,
            ApplicantId = applicantId,
            ApplicationCategoryId = categoryId,
            FundCategoryId = fundId,
            RequestedAmount = 1000m,
            ApplicationDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });

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

    // --- Regression guards for the three-state FundEligibility rule (v1.5 amendment): a
    // ZakatOnly category (HEALTH here, representative of SHAADI/EDUCATION/HOUSE_RENT/EMERGENCY
    // too) must now be rejected on General, not just accepted on Zakat. OTHER is the new
    // dual-eligible (Either) category and must succeed on both. ---

    [Fact]
    public async Task ZakatOnlyCategory_AgainstZakatFund_Succeeds()
    {
        var applicantId = await SeedApplicantAsync();
        var (_, zakatId, _) = await LoadSeededIdsAsync();
        var healthId = await LoadCategoryIdAsync("HEALTH");

        await InsertApplicationAsync("TEST-HZ", applicantId, healthId, zakatId);
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.Applications.CountAsync());
    }

    [Fact]
    public async Task ZakatOnlyCategory_AgainstGeneralFund_IsRejectedByTrigger()
    {
        var applicantId = await SeedApplicantAsync();
        var (_, _, generalId) = await LoadSeededIdsAsync();
        var healthId = await LoadCategoryIdAsync("HEALTH");

        await InsertApplicationAsync("TEST-HG", applicantId, healthId, generalId);

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task EitherCategory_AgainstZakatFund_Succeeds()
    {
        var applicantId = await SeedApplicantAsync();
        var (_, zakatId, _) = await LoadSeededIdsAsync();
        var otherId = await LoadCategoryIdAsync("OTHER");

        await InsertApplicationAsync("TEST-OZ", applicantId, otherId, zakatId);
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.Applications.CountAsync());
    }

    [Fact]
    public async Task EitherCategory_AgainstGeneralFund_Succeeds()
    {
        var applicantId = await SeedApplicantAsync();
        var (_, _, generalId) = await LoadSeededIdsAsync();
        var otherId = await LoadCategoryIdAsync("OTHER");

        await InsertApplicationAsync("TEST-OG", applicantId, otherId, generalId);
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.Applications.CountAsync());
    }
}
