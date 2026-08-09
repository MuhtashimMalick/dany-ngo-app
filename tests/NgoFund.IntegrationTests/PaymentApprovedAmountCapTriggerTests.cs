using Microsoft.EntityFrameworkCore;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the `trg_payments_enforce_approved_amount_cap` DB trigger (D3.3 — the database-level
/// half of  rule 4's payment cap; <see cref="NgoFund.Infrastructure.Services.PaymentService"/>
/// is the application-level half, covered by <see cref="PaymentWorkflowTests"/>) against a real,
/// freshly migrated PostgreSQL 18 instance. Inserts directly via EF bypassing
/// <c>PaymentService</c> entirely — a service-level test alone would never reach the trigger,
/// since the service already blocks an overshoot before any SQL is issued.
/// </summary>
public class PaymentApprovedAmountCapTriggerTests : IAsyncLifetime
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

    private async Task<FundApplication> SeedApprovedApplicationAsync(decimal approvedAmount)
    {
        var healthId = await _db.ApplicationCategories.Where(c => c.Code == "HEALTH").Select(c => c.Id).SingleAsync();
        var zakatFundId = await _db.FundCategories.Where(f => f.Code == "ZAKAT").Select(f => f.Id).SingleAsync();

        var applicant = new Applicant { Cnic = "55555-5555555-9", FullName = "Trigger Test Applicant", Gender = Gender.Male };
        _db.Applicants.Add(applicant);

        var application = new FundApplication
        {
            ApplicationNumber = "TRG-0001",
            ApplicantId = applicant.Id,
            ApplicationCategoryId = healthId,
            FundCategoryId = zakatFundId,
            RequestedAmount = approvedAmount,
            ApprovedAmount = approvedAmount,
            Status = ApplicationStatus.Approved,
            ApplicationDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        _db.Applications.Add(application);
        await _db.SaveChangesAsync();

        return application;
    }

    private static Payment MakePayment(Guid applicationId, Guid fundCategoryId, decimal amount) => new()
    {
        PaymentNumber = $"TRG-{Guid.NewGuid():N}"[..20],
        ApplicationId = applicationId,
        FundCategoryId = fundCategoryId,
        Amount = amount,
        PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
        PaymentMethod = PaymentMethod.Cash,
        Status = PaymentStatus.Completed,
    };

    [Fact]
    public async Task DirectInsert_WithinApprovedAmount_Succeeds()
    {
        var application = await SeedApprovedApplicationAsync(1000m);

        _db.Payments.Add(MakePayment(application.Id, application.FundCategoryId, 800m));
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.Payments.CountAsync());
    }

    [Fact]
    public async Task DirectInsert_ThatWouldBreachApprovedAmount_IsRejectedByTrigger()
    {
        var application = await SeedApprovedApplicationAsync(1000m);

        _db.Payments.Add(MakePayment(application.Id, application.FundCategoryId, 800m));
        await _db.SaveChangesAsync();

        // 800 already completed; a further 300 would bring the total to 1100, over the 1000 cap.
        // PaymentService would never let this SQL be issued — this insert bypasses it entirely.
        _db.Payments.Add(MakePayment(application.Id, application.FundCategoryId, 300m));

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }
}
