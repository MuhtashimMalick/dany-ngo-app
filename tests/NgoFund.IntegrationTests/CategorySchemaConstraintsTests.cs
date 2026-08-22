using Microsoft.EntityFrameworkCore;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the raw DB constraints added for category application details, against a real, freshly
/// migrated Postgres 18 — the widened <c>documents</c> exactly-one-owner CHECK (now 5 arms), the
/// guarantor <c>(application_id, sequence_no)</c> unique index, the partial unique index on
/// <c>applications.external_form_reference</c>, and the marriage rukhsati/nikah CHECK. Modelled on
/// <see cref="ZakatTriggerTests"/>'s structure — direct EF inserts, bypassing the API/services.
/// </summary>
public class CategorySchemaConstraintsTests : IAsyncLifetime
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

    private async Task<Applicant> SeedApplicantAsync(string cnic)
    {
        var applicant = new Applicant { Cnic = cnic, FullName = "Schema Test Applicant", Gender = Gender.Male };
        _db.Applicants.Add(applicant);
        await _db.SaveChangesAsync();
        return applicant;
    }

    private async Task<FundApplication> SeedApplicationAsync(string cnic, string categoryCode, string applicationNumber)
    {
        var applicant = await SeedApplicantAsync(cnic);
        var categoryId = await _db.ApplicationCategories.Where(c => c.Code == categoryCode).Select(c => c.Id).SingleAsync();
        var generalFundId = await _db.FundCategories.Where(f => f.Code == "GENERAL").Select(f => f.Id).SingleAsync();

        var application = new FundApplication
        {
            ApplicationNumber = applicationNumber,
            ApplicantId = applicant.Id,
            ApplicationCategoryId = categoryId,
            FundCategoryId = generalFundId,
            RequestedAmount = 1000m,
            ApplicationDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        _db.Applications.Add(application);
        await _db.SaveChangesAsync();
        return application;
    }

    // --- documents: 5-arm exactly-one-owner CHECK ---

    [Fact]
    public async Task Document_WithZeroOwners_IsRejectedByCheckConstraint()
    {
        _db.Documents.Add(new Document
        {
            FileName = "test.jpg",
            StorageKey = Guid.NewGuid().ToString(),
            ContentType = "image/jpeg",
            SizeBytes = 10,
            Sha256 = new string('a', 64),
            DocumentType = DocumentType.Other,
            UploadedAt = DateTimeOffset.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Document_WithTwoOwners_IsRejectedByCheckConstraint()
    {
        var application = await SeedApplicationAsync("50001-5000001-1", "HOUSE_RENT", "SCHEMA-0001");

        _db.Documents.Add(new Document
        {
            FileName = "test.jpg",
            StorageKey = Guid.NewGuid().ToString(),
            ContentType = "image/jpeg",
            SizeBytes = 10,
            Sha256 = new string('a', 64),
            DocumentType = DocumentType.Other,
            ApplicantId = application.ApplicantId,
            ApplicationId = application.Id,
            UploadedAt = DateTimeOffset.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Document_OwnedByGuarantorOnly_Succeeds()
    {
        var application = await SeedApplicationAsync("50002-5000002-2", "ROZGAR", "SCHEMA-0002");
        var guarantor = new ApplicationGuarantor { ApplicationId = application.Id, SequenceNumber = 1, FullName = "Guarantor" };
        _db.ApplicationGuarantors.Add(guarantor);
        await _db.SaveChangesAsync();

        _db.Documents.Add(new Document
        {
            FileName = "cnic.jpg",
            StorageKey = Guid.NewGuid().ToString(),
            ContentType = "image/jpeg",
            SizeBytes = 10,
            Sha256 = new string('b', 64),
            DocumentType = DocumentType.CnicFront,
            ApplicationGuarantorId = guarantor.Id,
            UploadedAt = DateTimeOffset.UtcNow,
        });

        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.Documents.CountAsync());
    }

    // --- application_guarantors: (application_id, sequence_no) unique ---

    [Fact]
    public async Task Guarantors_DuplicateSequenceNumberForSameApplication_IsRejected()
    {
        var application = await SeedApplicationAsync("50003-5000003-3", "ROZGAR", "SCHEMA-0003");

        _db.ApplicationGuarantors.Add(new ApplicationGuarantor { ApplicationId = application.Id, SequenceNumber = 1, FullName = "First" });
        await _db.SaveChangesAsync();

        _db.ApplicationGuarantors.Add(new ApplicationGuarantor { ApplicationId = application.Id, SequenceNumber = 1, FullName = "Duplicate sequence" });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    // --- applications.external_form_reference: partial unique index ---

    [Fact]
    public async Task ExternalFormReference_MultipleNulls_AreAllowed()
    {
        await SeedApplicationAsync("50004-5000004-4", "HEALTH", "SCHEMA-0004A");
        await SeedApplicationAsync("50005-5000005-5", "HEALTH", "SCHEMA-0004B");

        Assert.Equal(2, await _db.Applications.CountAsync());
    }

    [Fact]
    public async Task ExternalFormReference_DuplicateNonNullValue_IsRejected()
    {
        var first = await SeedApplicationAsync("50006-5000006-6", "HEALTH", "SCHEMA-0005A");
        first.ExternalFormReference = "gform-response-123";
        await _db.SaveChangesAsync();

        var second = await SeedApplicationAsync("50007-5000007-7", "HEALTH", "SCHEMA-0005B");
        second.ExternalFormReference = "gform-response-123";

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    // --- marriage_application_details: rukhsati_date >= nikah_date CHECK ---

    [Fact]
    public async Task MarriageDetails_RukhsatiBeforeNikah_IsRejectedByCheckConstraint()
    {
        var application = await SeedApplicationAsync("50008-5000008-8", "SHAADI", "SCHEMA-0006");

        _db.MarriageApplicationDetails.Add(new MarriageApplicationDetails
        {
            ApplicationId = application.Id,
            BrideName = "Bride",
            GroomName = "Groom",
            NikahDate = new DateOnly(2026, 6, 10),
            RukhsatiDate = new DateOnly(2026, 6, 1),
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task MarriageDetails_RukhsatiOnOrAfterNikah_Succeeds()
    {
        var application = await SeedApplicationAsync("50009-5000009-9", "SHAADI", "SCHEMA-0007");

        _db.MarriageApplicationDetails.Add(new MarriageApplicationDetails
        {
            ApplicationId = application.Id,
            BrideName = "Bride",
            GroomName = "Groom",
            NikahDate = new DateOnly(2026, 6, 10),
            RukhsatiDate = new DateOnly(2026, 6, 10),
        });

        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.MarriageApplicationDetails.CountAsync());
    }
}
