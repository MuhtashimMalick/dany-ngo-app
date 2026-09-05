using Microsoft.EntityFrameworkCore;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the `fn_enforce_installment_schedule_totals` DEFERRABLE constraint trigger (D7#4/D5 —
/// the database-level half of the "no interest" guarantee that
/// <see cref="NgoFund.Domain.Loans.LoanScheduleCalculator"/> already enforces in code, machine-checked
/// by <c>LoanScheduleCalculatorTests.GenerateInstallments_SumAlwaysEqualsPrincipal</c>) against a
/// real, freshly migrated PostgreSQL 18 instance. Inserts directly via EF, bypassing
/// <c>LoanService</c>/the calculator entirely, so a schedule that doesn't sum to its agreement's
/// principal is still rejected at commit time even if some future caller skips the calculator.
/// </summary>
public class InstallmentScheduleTotalsTriggerTests : IAsyncLifetime
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

    private async Task<LoanAgreement> SeedApprovedAgreementShellAsync(decimal principal)
    {
        // OTHER, not HEALTH: this test targets the loan-schedule-totals trigger, unrelated to the
        // application category's Zakat rule, and needs a category valid against General — HEALTH
        // became ZakatOnly under the v1.5 FundEligibility amendment, so it no longer qualifies.
        var categoryId = await _db.ApplicationCategories.Where(c => c.Code == "OTHER").Select(c => c.Id).SingleAsync();
        var generalFundId = await _db.FundCategories.Where(f => f.Code == "GENERAL").Select(f => f.Id).SingleAsync();

        var applicant = new Applicant { Cnic = "90001-9000001-1", FullName = "Schedule Totals Trigger Applicant", Gender = Gender.Male };
        _db.Applicants.Add(applicant);

        var application = new FundApplication
        {
            ApplicationNumber = $"SCHTRG-{Guid.NewGuid():N}"[..20],
            ApplicantId = applicant.Id,
            ApplicationCategoryId = categoryId,
            FundCategoryId = generalFundId,
            RequestedAmount = principal,
            ApprovedAmount = principal,
            Status = ApplicationStatus.Approved,
            ApplicationDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        _db.Applications.Add(application);

        var agreement = new LoanAgreement
        {
            LoanNumber = $"LOAN-{Guid.NewGuid():N}"[..20],
            ApplicationId = application.Id,
            FundCategoryId = generalFundId,
            PrincipalAmount = principal,
            InstallmentCount = 2,
            FirstDueDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        _db.LoanAgreements.Add(agreement);

        await _db.SaveChangesAsync();

        return agreement;
    }

    [Fact]
    public async Task DirectInsert_ScheduleSummingToPrincipal_Succeeds()
    {
        var agreement = await SeedApprovedAgreementShellAsync(1000m);

        _db.LoanInstallments.AddRange(
            new LoanInstallment { LoanAgreementId = agreement.Id, SequenceNumber = 1, AmountDue = 500m, DueDate = DateOnly.FromDateTime(DateTime.UtcNow) },
            new LoanInstallment { LoanAgreementId = agreement.Id, SequenceNumber = 2, AmountDue = 500m, DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1) });

        await _db.SaveChangesAsync();

        Assert.Equal(2, await _db.LoanInstallments.CountAsync());
    }

    [Fact]
    public async Task DirectInsert_ScheduleNotSummingToPrincipal_IsRejectedByDeferredTrigger()
    {
        var agreement = await SeedApprovedAgreementShellAsync(1000m);

        // 500 + 400 = 900, not the agreement's 1000 principal.
        _db.LoanInstallments.AddRange(
            new LoanInstallment { LoanAgreementId = agreement.Id, SequenceNumber = 1, AmountDue = 500m, DueDate = DateOnly.FromDateTime(DateTime.UtcNow) },
            new LoanInstallment { LoanAgreementId = agreement.Id, SequenceNumber = 2, AmountDue = 400m, DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1) });

        // Deferred to end-of-transaction, so the failure surfaces from the implicit commit inside
        // SaveChangesAsync rather than from an individual INSERT statement — assert broadly rather
        // than pin down whether Npgsql/EF wraps it as DbUpdateException or lets the raw
        // PostgresException through, since that wrapping is an EF/Npgsql implementation detail.
        var exception = await Record.ExceptionAsync(() => _db.SaveChangesAsync());

        Assert.NotNull(exception);
    }
}
