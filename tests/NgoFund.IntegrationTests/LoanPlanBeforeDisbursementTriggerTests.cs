using Microsoft.EntityFrameworkCore;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the `fn_enforce_loan_plan_before_disbursement` DB trigger (D7#5 — unconditional, no
/// grandfathering clause; <see cref="NgoFund.Infrastructure.Services.PaymentService"/>'s
/// <c>LoanPlanRequiredException</c> gate is the application-level half, covered by
/// <see cref="LoanAgreementWorkflowTests"/>) against a real, freshly migrated PostgreSQL 18
/// instance, modelled on <see cref="PaymentApprovedAmountCapTriggerTests"/>. Inserts directly via
/// EF, bypassing <c>PaymentService</c> entirely.
/// </summary>
public class LoanPlanBeforeDisbursementTriggerTests : IAsyncLifetime
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

    private async Task<FundApplication> SeedApprovedApplicationAsync(decimal amount, Guid fundCategoryId)
    {
        // OTHER, not HEALTH: this fixture is called with both the Zakat and the General fund
        // across the tests below, and needs a category valid against both — HEALTH became
        // ZakatOnly under the v1.5 FundEligibility amendment, so OTHER (the sole Either category)
        // is used instead. Unrelated to the loan-plan-before-disbursement trigger under test.
        var categoryId = await _db.ApplicationCategories.Where(c => c.Code == "OTHER").Select(c => c.Id).SingleAsync();

        var applicant = new Applicant { Cnic = "80001-8000001-1", FullName = "Loan Plan Trigger Applicant", Gender = Gender.Male };
        _db.Applicants.Add(applicant);

        var application = new FundApplication
        {
            ApplicationNumber = $"PLANTRG-{Guid.NewGuid():N}"[..20],
            ApplicantId = applicant.Id,
            ApplicationCategoryId = categoryId,
            FundCategoryId = fundCategoryId,
            RequestedAmount = amount,
            ApprovedAmount = amount,
            Status = ApplicationStatus.Approved,
            ApplicationDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        _db.Applications.Add(application);
        await _db.SaveChangesAsync();

        return application;
    }

    private static Payment MakePayment(Guid applicationId, Guid fundCategoryId, decimal amount) => new()
    {
        PaymentNumber = $"PLANTRG-PAY-{Guid.NewGuid():N}"[..20],
        ApplicationId = applicationId,
        FundCategoryId = fundCategoryId,
        Amount = amount,
        PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
        PaymentMethod = PaymentMethod.Cash,
        Status = PaymentStatus.Completed,
    };

    [Fact]
    public async Task DirectInsert_GeneralFundPayment_WithNoLoanAgreement_IsRejectedByTrigger()
    {
        var generalFundId = await _db.FundCategories.Where(f => f.Code == "GENERAL").Select(f => f.Id).SingleAsync();
        var application = await SeedApprovedApplicationAsync(1000m, generalFundId);

        _db.Payments.Add(MakePayment(application.Id, generalFundId, 500m));

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task DirectInsert_GeneralFundPayment_WithActiveLoanAgreement_Succeeds()
    {
        var generalFundId = await _db.FundCategories.Where(f => f.Code == "GENERAL").Select(f => f.Id).SingleAsync();
        var application = await SeedApprovedApplicationAsync(1000m, generalFundId);

        var agreement = new LoanAgreement
        {
            LoanNumber = $"LOAN-{Guid.NewGuid():N}"[..20],
            ApplicationId = application.Id,
            FundCategoryId = generalFundId,
            PrincipalAmount = 1000m,
            InstallmentCount = 1,
            FirstDueDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        _db.LoanAgreements.Add(agreement);
        _db.LoanInstallments.Add(new LoanInstallment
        {
            LoanAgreementId = agreement.Id,
            SequenceNumber = 1,
            AmountDue = 1000m,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        await _db.SaveChangesAsync();

        _db.Payments.Add(MakePayment(application.Id, generalFundId, 500m));
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.Payments.CountAsync());
    }

    [Fact]
    public async Task DirectInsert_ZakatFundPayment_WithNoLoanAgreement_Succeeds()
    {
        var zakatFundId = await _db.FundCategories.Where(f => f.Code == "ZAKAT").Select(f => f.Id).SingleAsync();
        var application = await SeedApprovedApplicationAsync(1000m, zakatFundId);

        _db.Payments.Add(MakePayment(application.Id, zakatFundId, 500m));
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.Payments.CountAsync());
    }
}
