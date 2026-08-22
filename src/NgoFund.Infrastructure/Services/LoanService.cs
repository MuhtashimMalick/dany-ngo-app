using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Loans;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;
using NgoFund.Domain.Loans;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

/// <summary>
/// Owns the qard al-hasan lifecycle end to end. Mirrors <see cref="PaymentService"/>'s locking
/// order (agreement/application row first, then the fund) and <see cref="DonationService"/>'s
/// "post the ledger entry in the same transaction as the source row" pattern — no new patterns
/// invented here.
/// </summary>
public class LoanService(AppDbContext dbContext, INumberGenerator numberGenerator, ICurrentUserService currentUser) : ILoanService
{
    public async Task<PagedResult<LoanAgreementDto>> GetLoansAsync(
        PagedQuery query, string? status, bool? overdueOnly, Guid? fundCategoryId, CancellationToken cancellationToken)
    {
        var loansQuery = dbContext.LoanAgreements
            .AsNoTracking()
            .Include(l => l.Application).ThenInclude(a => a.Applicant)
            .Include(l => l.FundCategory)
            .Include(l => l.Installments)
            .AsQueryable();

        if (status is not null && Enum.TryParse<LoanAgreementStatus>(status, out var statusEnum))
        {
            loansQuery = loansQuery.Where(l => l.Status == statusEnum);
        }

        if (fundCategoryId is not null)
        {
            loansQuery = loansQuery.Where(l => l.FundCategoryId == fundCategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            loansQuery = loansQuery.Where(l =>
                EF.Functions.ILike(l.LoanNumber, pattern) ||
                EF.Functions.ILike(l.Application.ApplicationNumber, pattern) ||
                EF.Functions.ILike(l.Application.Applicant.FullName, pattern));
        }

        if (overdueOnly == true)
        {
            var overdueIds = await dbContext.Database
                .SqlQuery<Guid>($"SELECT loan_agreement_id FROM vw_loan_balances WHERE overdue_amount > 0")
                .ToListAsync(cancellationToken);
            loansQuery = loansQuery.Where(l => overdueIds.Contains(l.Id));
        }

        var totalCount = await loansQuery.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var entities = await loansQuery
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<LoanAgreementDto>(entities.Select(MapAgreement).ToList(), totalCount, page, pageSize);
    }

    public async Task<LoanAgreementDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.LoanAgreements
            .AsNoTracking()
            .Include(l => l.Application).ThenInclude(a => a.Applicant)
            .Include(l => l.FundCategory)
            .Include(l => l.Installments)
            .SingleOrDefaultAsync(l => l.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("LoanAgreement", id);

        return MapAgreement(entity);
    }

    public async Task<LoanScheduleDto> GetScheduleByApplicationAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Applications.AnyAsync(a => a.Id == applicationId, cancellationToken))
        {
            throw new EntityNotFoundException("Application", applicationId);
        }

        // The most recent agreement for this application — Zakat-fund applications never have
        // one, in which case this is null and we return an empty schedule rather than a 404.
        var agreement = await dbContext.LoanAgreements
            .Include(l => l.Application).ThenInclude(a => a.Applicant)
            .Include(l => l.FundCategory)
            .Include(l => l.Installments)
            .Where(l => l.ApplicationId == applicationId)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (agreement is null)
        {
            return new LoanScheduleDto(null, [], [], 0m, 0m, 0m, 0m, false, null, 0m);
        }

        var installments = agreement.Installments.OrderBy(i => i.SequenceNumber).ToList();

        var repayments = await dbContext.LoanRepayments
            .AsNoTracking()
            .Where(r => r.LoanAgreementId == agreement.Id)
            .OrderByDescending(r => r.RepaymentDate).ThenByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var balance = await GetLoanBalanceAsync(agreement.Id, cancellationToken);

        var lines = installments
            .Select(i => new LoanScheduleCalculator.InstallmentPlanLine(i.SequenceNumber, i.AmountDue, i.DueDate))
            .ToList();
        var allocated = LoanScheduleCalculator.Allocate(lines, balance.TotalRepaid, balance.PrincipalDisbursed, DateOnly.FromDateTime(DateTime.UtcNow));
        var idBySequence = installments.ToDictionary(i => i.SequenceNumber, i => i.Id);

        var installmentDtos = allocated
            .Select(a => new LoanInstallmentDto(idBySequence[a.SequenceNumber], a.SequenceNumber, a.AmountDue, a.DueDate, a.AmountAllocated, a.AmountRemaining, a.Status.ToString()))
            .ToList();

        // Deliberately NOT just OutstandingBalance <= 0 — a brand-new, undisbursed agreement must
        // never read as settled.
        var isSettled = balance.OutstandingBalance <= 0m && balance.PrincipalDisbursed > 0m;

        return new LoanScheduleDto(
            MapAgreement(agreement), installmentDtos, repayments.Select(MapRepayment).ToList(),
            balance.PrincipalScheduled, balance.PrincipalDisbursed, balance.TotalRepaid, balance.OutstandingBalance,
            isSettled, balance.NextDueDate, balance.OverdueAmount);
    }

    public async Task<IReadOnlyList<LoanInstallmentDto>> PreviewScheduleAsync(PreviewLoanScheduleRequest request, CancellationToken cancellationToken)
    {
        var application = await dbContext.Applications
            .Include(a => a.FundCategory)
            .SingleOrDefaultAsync(a => a.Id == request.ApplicationId, cancellationToken)
            ?? throw new EntityNotFoundException("Application", request.ApplicationId);

        LoanAgreement.EnsureFundIsRepayable(application.FundCategory);

        var principal = application.ApprovedAmount ?? throw new ApprovedAmountNotSetException();
        var frequency = Enum.Parse<LoanInstallmentFrequency>(request.Frequency);

        var lines = LoanScheduleCalculator.GenerateInstallments(principal, request.InstallmentCount, request.FirstDueDate, frequency);

        // Nothing is disbursed/repaid yet for a preview — run it through the same allocator so the
        // returned statuses ("Undisbursed" for every line) come from the one place that logic lives.
        var allocated = LoanScheduleCalculator.Allocate(lines, totalRepaid: 0m, principalDisbursed: 0m, DateOnly.FromDateTime(DateTime.UtcNow));

        return allocated
            .Select(a => new LoanInstallmentDto(Guid.Empty, a.SequenceNumber, a.AmountDue, a.DueDate, a.AmountAllocated, a.AmountRemaining, a.Status.ToString()))
            .ToList();
    }

    public async Task<LoanAgreementDto> CreateAgreementAsync(CreateLoanAgreementRequest request, CancellationToken cancellationToken)
    {
        var application = await dbContext.Applications
            .Include(a => a.Applicant)
            .Include(a => a.FundCategory)
            .SingleOrDefaultAsync(a => a.Id == request.ApplicationId, cancellationToken)
            ?? throw new EntityNotFoundException("Application", request.ApplicationId);

        if (application.Status != ApplicationStatus.Approved)
        {
            throw new InvalidStatusTransitionException(application.Status, ApplicationStatus.Approved);
        }

        LoanAgreement.EnsureFundIsRepayable(application.FundCategory);

        var principal = application.ApprovedAmount ?? throw new ApprovedAmountNotSetException();

        if (await dbContext.LoanAgreements.AnyAsync(l => l.ApplicationId == application.Id && l.Status == LoanAgreementStatus.Active, cancellationToken))
        {
            throw new LoanAgreementLockedException($"Application {application.ApplicationNumber} already has an Active loan agreement.");
        }

        var frequency = Enum.Parse<LoanInstallmentFrequency>(request.Frequency);
        var lines = LoanScheduleCalculator.GenerateInstallments(principal, request.InstallmentCount, request.FirstDueDate, frequency);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var loanNumber = await numberGenerator.NextAsync("LoanAgreement", cancellationToken);

        var agreement = new LoanAgreement
        {
            LoanNumber = loanNumber,
            ApplicationId = application.Id,
            FundCategoryId = application.FundCategoryId,
            PrincipalAmount = principal,
            InstallmentCount = request.InstallmentCount,
            Frequency = frequency,
            FirstDueDate = request.FirstDueDate,
            Notes = request.Notes,
        };
        dbContext.LoanAgreements.Add(agreement);

        var installments = lines.Select(l => new LoanInstallment
        {
            LoanAgreementId = agreement.Id,
            SequenceNumber = l.SequenceNumber,
            AmountDue = l.AmountDue,
            DueDate = l.DueDate,
        }).ToList();
        dbContext.LoanInstallments.AddRange(installments);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        agreement.Application = application;
        agreement.FundCategory = application.FundCategory;
        agreement.Installments = installments;
        return MapAgreement(agreement);
    }

    public async Task CancelAgreementAsync(Guid id, CancelLoanAgreementRequest request, CancellationToken cancellationToken)
    {
        var agreement = await dbContext.LoanAgreements.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("LoanAgreement", id);

        if (agreement.Status != LoanAgreementStatus.Active)
        {
            throw new LoanAgreementLockedException($"Loan agreement {agreement.LoanNumber} is not Active and cannot be cancelled.");
        }

        var totalRepaid = await dbContext.LoanRepayments
            .Where(r => r.LoanAgreementId == agreement.Id && r.Status == LoanRepaymentStatus.Completed)
            .SumAsync(r => (decimal?)r.Amount, cancellationToken) ?? 0m;

        agreement.EnsureCancellable(totalRepaid);

        agreement.Status = LoanAgreementStatus.Cancelled;
        agreement.CancelledAt = DateTimeOffset.UtcNow;
        agreement.CancelledBy = currentUser.UserId;
        agreement.CancelReason = request.Reason;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task WriteOffAsync(Guid id, WriteOffLoanRequest request, CancellationToken cancellationToken)
    {
        var agreement = await dbContext.LoanAgreements.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("LoanAgreement", id);

        if (agreement.Status != LoanAgreementStatus.Active)
        {
            throw new LoanAgreementLockedException($"Loan agreement {agreement.LoanNumber} is not Active and cannot be written off.");
        }

        agreement.Status = LoanAgreementStatus.WrittenOff;
        agreement.WrittenOffAt = DateTimeOffset.UtcNow;
        agreement.WrittenOffBy = currentUser.UserId;
        agreement.WrittenOffReason = request.Reason;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<LoanRepaymentDto> RecordRepaymentAsync(RecordLoanRepaymentRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Row lock on the agreement first, then the fund — same order PaymentService uses for
        // application-then-fund, so the two money-movement paths can never deadlock against
        // each other.
        var agreement = await dbContext.LoanAgreements
            .FromSqlInterpolated($"SELECT * FROM loan_agreements WHERE id = {request.LoanAgreementId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("LoanAgreement", request.LoanAgreementId);

        if (agreement.Status != LoanAgreementStatus.Active)
        {
            throw new LoanAgreementLockedException($"Loan agreement {agreement.LoanNumber} is not Active and cannot accept repayments.");
        }

        var fundCategory = await dbContext.FundCategories.FindAsync([agreement.FundCategoryId], cancellationToken)
            ?? throw new EntityNotFoundException("FundCategory", agreement.FundCategoryId);

        await dbContext.Database.SqlQuery<int>(
            $"SELECT 1 FROM fund_categories WHERE id = {fundCategory.Id} FOR UPDATE")
            .ToListAsync(cancellationToken);

        var principalDisbursed = await dbContext.Payments
            .Where(p => p.ApplicationId == agreement.ApplicationId && p.FundCategoryId == agreement.FundCategoryId && p.Status == PaymentStatus.Completed)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var totalRepaid = await dbContext.LoanRepayments
            .Where(r => r.LoanAgreementId == agreement.Id && r.Status == LoanRepaymentStatus.Completed)
            .SumAsync(r => (decimal?)r.Amount, cancellationToken) ?? 0m;

        if (totalRepaid + request.Amount > principalDisbursed)
        {
            throw new LoanRepaymentExceedsOutstandingException(principalDisbursed - totalRepaid, request.Amount);
        }

        var repaymentNumber = await numberGenerator.NextAsync("LoanRepayment", cancellationToken);

        var repayment = new LoanRepayment
        {
            RepaymentNumber = repaymentNumber,
            LoanAgreementId = agreement.Id,
            Amount = request.Amount,
            RepaymentDate = request.RepaymentDate,
            PaymentMethod = Enum.Parse<PaymentMethod>(request.PaymentMethod),
            InstrumentNumber = request.InstrumentNumber,
            BankName = request.BankName,
            ReceivedFromName = request.ReceivedFromName,
            ReceivedFromCnic = request.ReceivedFromCnic,
            Notes = request.Notes,
        };
        dbContext.LoanRepayments.Add(repayment);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.FundTransactions.Add(new FundTransaction
        {
            FundCategoryId = fundCategory.Id,
            Direction = TransactionDirection.Credit,
            Amount = repayment.Amount,
            TransactionDate = repayment.RepaymentDate,
            ReferenceType = TransactionReferenceType.LoanRepayment,
            ReferenceId = repayment.Id,
            Description = $"Loan repayment {repayment.RepaymentNumber} for loan agreement {agreement.LoanNumber}",
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return MapRepayment(repayment);
    }

    public async Task VoidRepaymentAsync(Guid id, VoidLoanRepaymentRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var repayment = await dbContext.LoanRepayments.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("LoanRepayment", id);

        if (repayment.Status == LoanRepaymentStatus.Voided)
        {
            throw new AlreadyVoidedException("loan repayment");
        }

        var agreement = await dbContext.LoanAgreements.FindAsync([repayment.LoanAgreementId], cancellationToken)
            ?? throw new EntityNotFoundException("LoanAgreement", repayment.LoanAgreementId);

        repayment.Status = LoanRepaymentStatus.Voided;
        repayment.VoidedAt = DateTimeOffset.UtcNow;
        repayment.VoidedBy = currentUser.UserId;
        repayment.VoidReason = request.Reason;

        dbContext.FundTransactions.Add(new FundTransaction
        {
            FundCategoryId = agreement.FundCategoryId,
            Direction = TransactionDirection.Debit,
            Amount = repayment.Amount,
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ReferenceType = TransactionReferenceType.LoanRepaymentReversal,
            ReferenceId = repayment.Id,
            Description = $"Void of loan repayment {repayment.RepaymentNumber}: {request.Reason}",
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Maps 1:1 to <c>vw_loan_balances</c>, the same way <see cref="FundCategoryService"/> derives fund balances from <c>vw_fund_balances</c> — never a stored column.</summary>
    private record LoanBalanceRow(
        Guid LoanAgreementId, Guid ApplicationId, Guid FundCategoryId, decimal PrincipalScheduled,
        decimal PrincipalDisbursed, decimal TotalRepaid, decimal OutstandingBalance, DateOnly? NextDueDate, decimal OverdueAmount);

    private async Task<LoanBalanceRow> GetLoanBalanceAsync(Guid loanAgreementId, CancellationToken cancellationToken) =>
        await dbContext.Database.SqlQuery<LoanBalanceRow>(
            $"""
            SELECT loan_agreement_id, application_id, fund_category_id, principal_scheduled, principal_disbursed,
                   total_repaid, outstanding_balance, next_due_date, overdue_amount
            FROM vw_loan_balances
            WHERE loan_agreement_id = {loanAgreementId}
            """).SingleAsync(cancellationToken);

    private static LoanAgreementDto MapAgreement(LoanAgreement l) => new(
        l.Id, l.LoanNumber, l.ApplicationId, l.Application.ApplicationNumber, l.Application.Applicant.FullName,
        l.FundCategoryId, l.FundCategory.Name, l.PrincipalAmount, l.Status.ToString(), l.InstallmentCount,
        // The base installment amount, read from the already-generated schedule rather than
        // re-deriving D5's rounding rule a second time here.
        l.Installments.OrderBy(i => i.SequenceNumber).First().AmountDue,
        l.Frequency.ToString(), l.FirstDueDate, l.CreatedAt, l.CancelledAt, l.CancelReason, l.WrittenOffAt, l.WrittenOffReason, l.Notes);

    private static LoanRepaymentDto MapRepayment(LoanRepayment r) => new(
        r.Id, r.RepaymentNumber, r.LoanAgreementId, r.Amount, r.RepaymentDate, r.PaymentMethod.ToString(),
        r.InstrumentNumber, r.BankName, r.Status.ToString(), r.ReceivedFromName, r.ReceivedFromCnic, r.Notes,
        r.VoidedAt, r.VoidReason);
}
