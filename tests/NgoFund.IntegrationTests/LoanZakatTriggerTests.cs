using Microsoft.EntityFrameworkCore;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the `fn_enforce_loan_agreement_fund_eligibility`/`fn_enforce_loan_repayment_fund_eligibility`
/// DB triggers (D7#1/#2) against a real, freshly migrated PostgreSQL 18 instance — the
/// defence-in-depth half of <see cref="LoanAgreement.EnsureFundIsRepayable"/> (covered by unit
/// tests), modelled on <see cref="ZakatTriggerTests"/>'s structure. Inserts directly via EF,
/// bypassing <c>LoanService</c> entirely.
/// </summary>
public class LoanZakatTriggerTests : IAsyncLifetime
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

    private async Task<(FundApplication Application, Guid ZakatFundId, Guid GeneralFundId)> SeedApprovedApplicationAsync(decimal approvedAmount, bool zakatFund)
    {
        // OTHER, not HEALTH: this fixture needs a category valid against BOTH fund types (this
        // method is called with zakatFund true and false across the tests below), and since the
        // v1.5 FundEligibility amendment, HEALTH is ZakatOnly — OTHER is the sole dual-eligible
        // (Either) category. These tests are about the loan-agreement/repayment fund-eligibility
        // triggers, not the application-category Zakat rule, so which dual-eligible category is
        // used here is otherwise arbitrary.
        var categoryId = await _db.ApplicationCategories.Where(c => c.Code == "OTHER").Select(c => c.Id).SingleAsync();
        var zakatFundId = await _db.FundCategories.Where(f => f.Code == "ZAKAT").Select(f => f.Id).SingleAsync();
        var generalFundId = await _db.FundCategories.Where(f => f.Code == "GENERAL").Select(f => f.Id).SingleAsync();

        var applicant = new Applicant { Cnic = "60001-6000001-1", FullName = "Loan Trigger Applicant", Gender = Gender.Male };
        _db.Applicants.Add(applicant);

        var application = new FundApplication
        {
            ApplicationNumber = $"LOANTRG-{Guid.NewGuid():N}"[..20],
            ApplicantId = applicant.Id,
            ApplicationCategoryId = categoryId,
            FundCategoryId = zakatFund ? zakatFundId : generalFundId,
            RequestedAmount = approvedAmount,
            ApprovedAmount = approvedAmount,
            Status = ApplicationStatus.Approved,
            ApplicationDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        _db.Applications.Add(application);
        await _db.SaveChangesAsync();

        return (application, zakatFundId, generalFundId);
    }

    [Fact]
    public async Task DirectInsert_LoanAgreementAgainstZakatFund_IsRejectedByTrigger()
    {
        var (application, zakatFundId, _) = await SeedApprovedApplicationAsync(1000m, zakatFund: true);

        _db.LoanAgreements.Add(new LoanAgreement
        {
            LoanNumber = "LOAN-TEST-0001",
            ApplicationId = application.Id,
            FundCategoryId = zakatFundId,
            PrincipalAmount = 1000m,
            InstallmentCount = 1,
            FirstDueDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task DirectInsert_LoanAgreementAgainstGeneralFund_Succeeds()
    {
        var (application, _, generalFundId) = await SeedApprovedApplicationAsync(1000m, zakatFund: false);

        var agreement = new LoanAgreement
        {
            LoanNumber = "LOAN-TEST-0002",
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

        Assert.Equal(1, await _db.LoanAgreements.CountAsync());
    }

    [Fact]
    public async Task DirectInsert_RepaymentAgainstZakatFundAgreement_IsRejectedByTrigger()
    {
        var (application, zakatFundId, _) = await SeedApprovedApplicationAsync(1000m, zakatFund: true);

        // A Completed payment first, so the (unrelated) repayment-cap trigger doesn't reject the
        // repayment below for its own reason before we ever reach the fund-eligibility trigger
        // this test targets.
        _db.Payments.Add(new Payment
        {
            PaymentNumber = $"LOANTRG-PAY-{Guid.NewGuid():N}"[..20],
            ApplicationId = application.Id,
            FundCategoryId = zakatFundId,
            Amount = 1000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentMethod = PaymentMethod.Cash,
            Status = PaymentStatus.Completed,
        });
        await _db.SaveChangesAsync();

        // The agreement-level trigger (previous tests) would normally prevent a Zakat-fund
        // agreement from ever existing — disabled here only to reach the repayment-level trigger
        // directly, proving its defence-in-depth independently of the agreement-level check.
        await _db.Database.ExecuteSqlRawAsync("ALTER TABLE loan_agreements DISABLE TRIGGER trg_loan_agreements_enforce_fund_eligibility;");
        try
        {
            var agreement = new LoanAgreement
            {
                LoanNumber = "LOAN-TEST-0003",
                ApplicationId = application.Id,
                FundCategoryId = zakatFundId,
                PrincipalAmount = 1000m,
                InstallmentCount = 1,
                FirstDueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            };
            _db.LoanAgreements.Add(agreement);
            await _db.SaveChangesAsync();

            _db.LoanRepayments.Add(new LoanRepayment
            {
                RepaymentNumber = "REP-TEST-0001",
                LoanAgreementId = agreement.Id,
                Amount = 100m,
                RepaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
                PaymentMethod = PaymentMethod.Cash,
                ReceivedFromName = "Test Repayer",
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
        }
        finally
        {
            await _db.Database.ExecuteSqlRawAsync("ALTER TABLE loan_agreements ENABLE TRIGGER trg_loan_agreements_enforce_fund_eligibility;");
        }
    }
}
