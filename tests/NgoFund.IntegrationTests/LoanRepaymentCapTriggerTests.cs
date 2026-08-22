using Microsoft.EntityFrameworkCore;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the `fn_enforce_loan_repayment_cap` DB trigger (D7#3 — the database-level half of
/// "total completed repayments never exceed disbursed principal"; <see cref="NgoFund.Infrastructure.Services.LoanService"/>
/// enforces this in application code, covered by <see cref="LoanRepaymentLedgerTests"/>) against a
/// real, freshly migrated PostgreSQL 18 instance, modelled on
/// <see cref="PaymentApprovedAmountCapTriggerTests"/>. Inserts directly via EF, bypassing
/// <c>LoanService</c> entirely.
/// </summary>
public class LoanRepaymentCapTriggerTests : IAsyncLifetime
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

    /// <summary>Seeds a General-fund application, Approved with the given amount, disbursed in full via one Completed payment, plus a matching (1-installment) loan agreement.</summary>
    private async Task<LoanAgreement> SeedDisbursedLoanAsync(decimal amount)
    {
        var healthId = await _db.ApplicationCategories.Where(c => c.Code == "HEALTH").Select(c => c.Id).SingleAsync();
        var generalFundId = await _db.FundCategories.Where(f => f.Code == "GENERAL").Select(f => f.Id).SingleAsync();

        var applicant = new Applicant { Cnic = "70001-7000001-1", FullName = "Repayment Cap Trigger Applicant", Gender = Gender.Male };
        _db.Applicants.Add(applicant);

        var application = new FundApplication
        {
            ApplicationNumber = $"CAPTRG-{Guid.NewGuid():N}"[..20],
            ApplicantId = applicant.Id,
            ApplicationCategoryId = healthId,
            FundCategoryId = generalFundId,
            RequestedAmount = amount,
            ApprovedAmount = amount,
            Status = ApplicationStatus.Approved,
            ApplicationDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        _db.Applications.Add(application);

        var agreement = new LoanAgreement
        {
            LoanNumber = $"LOAN-{Guid.NewGuid():N}"[..20],
            ApplicationId = application.Id,
            FundCategoryId = generalFundId,
            PrincipalAmount = amount,
            InstallmentCount = 1,
            FirstDueDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        _db.LoanAgreements.Add(agreement);
        _db.LoanInstallments.Add(new LoanInstallment
        {
            LoanAgreementId = agreement.Id,
            SequenceNumber = 1,
            AmountDue = amount,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        _db.Payments.Add(new Payment
        {
            PaymentNumber = $"CAPTRG-PAY-{Guid.NewGuid():N}"[..20],
            ApplicationId = application.Id,
            FundCategoryId = generalFundId,
            Amount = amount,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentMethod = PaymentMethod.Cash,
            Status = PaymentStatus.Completed,
        });

        await _db.SaveChangesAsync();

        return agreement;
    }

    private static LoanRepayment MakeRepayment(Guid agreementId, decimal amount) => new()
    {
        RepaymentNumber = $"CAPTRG-REP-{Guid.NewGuid():N}"[..20],
        LoanAgreementId = agreementId,
        Amount = amount,
        RepaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
        PaymentMethod = PaymentMethod.Cash,
        ReceivedFromName = "Test Repayer",
    };

    [Fact]
    public async Task DirectInsert_WithinDisbursedPrincipal_Succeeds()
    {
        var agreement = await SeedDisbursedLoanAsync(1000m);

        _db.LoanRepayments.Add(MakeRepayment(agreement.Id, 800m));
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.LoanRepayments.CountAsync());
    }

    [Fact]
    public async Task DirectInsert_ThatWouldExceedDisbursedPrincipal_IsRejectedByTrigger()
    {
        var agreement = await SeedDisbursedLoanAsync(1000m);

        _db.LoanRepayments.Add(MakeRepayment(agreement.Id, 800m));
        await _db.SaveChangesAsync();

        // 800 already completed; a further 300 would bring the total to 1100, over the 1000
        // disbursed principal. LoanService would never let this SQL be issued — this bypasses it.
        _db.LoanRepayments.Add(MakeRepayment(agreement.Id, 300m));

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }
}
