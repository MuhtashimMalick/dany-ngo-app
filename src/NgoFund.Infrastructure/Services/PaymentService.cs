using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Payments;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

/// <summary>
/// Owns the only two ways money moves out of the fund ledger against an application: recording a
/// payment (Debit) and voiding one (Credit reversal). Both run inside one DB transaction together
/// with a row lock on the owning application (and, for creation, the fund it draws from) so
/// concurrent payments can never jointly overshoot the approved amount or the fund balance.
/// </summary>
public class PaymentService(
    AppDbContext dbContext,
    INumberGenerator numberGenerator,
    IFundCategoryService fundCategoryService,
    ICurrentUserService currentUser) : IPaymentService
{
    public async Task<PagedResult<PaymentDto>> GetPaymentsAsync(
        PagedQuery query, Guid? applicationId, string? status, string? paymentMethod, Guid? fundCategoryId,
        DateOnly? dateFrom, DateOnly? dateTo, CancellationToken cancellationToken)
    {
        var paymentsQuery = dbContext.Payments
            .AsNoTracking()
            .Include(p => p.Application).ThenInclude(a => a.Applicant)
            .Include(p => p.FundCategory)
            .AsQueryable();

        if (applicationId is not null)
        {
            paymentsQuery = paymentsQuery.Where(p => p.ApplicationId == applicationId);
        }

        if (status is not null && Enum.TryParse<PaymentStatus>(status, out var statusEnum))
        {
            paymentsQuery = paymentsQuery.Where(p => p.Status == statusEnum);
        }

        if (paymentMethod is not null && Enum.TryParse<PaymentMethod>(paymentMethod, out var methodEnum))
        {
            paymentsQuery = paymentsQuery.Where(p => p.PaymentMethod == methodEnum);
        }

        if (fundCategoryId is not null)
        {
            paymentsQuery = paymentsQuery.Where(p => p.FundCategoryId == fundCategoryId.Value);
        }

        if (dateFrom is not null)
        {
            paymentsQuery = paymentsQuery.Where(p => p.PaymentDate >= dateFrom.Value);
        }

        if (dateTo is not null)
        {
            paymentsQuery = paymentsQuery.Where(p => p.PaymentDate <= dateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            paymentsQuery = paymentsQuery.Where(p =>
                EF.Functions.ILike(p.PaymentNumber, pattern) ||
                EF.Functions.ILike(p.Application.ApplicationNumber, pattern) ||
                EF.Functions.ILike(p.Application.Applicant.FullName, pattern) ||
                EF.Functions.ILike(p.Application.Applicant.Cnic, pattern));
        }

        var totalCount = await paymentsQuery.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var entities = await paymentsQuery
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PaymentDto>(entities.Select(Map).ToList(), totalCount, page, pageSize);
    }

    public async Task<PaymentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Payments
            .AsNoTracking()
            .Include(p => p.Application).ThenInclude(a => a.Applicant)
            .Include(p => p.FundCategory)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("Payment", id);

        return Map(entity);
    }

    public async Task<PaymentDto> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Row lock on the application: two concurrent payments against the same application must
        // be serialized, or both could read the same "already paid" total and jointly overshoot
        // the approved amount.
        var application = await dbContext.Applications
            .FromSqlInterpolated($"SELECT * FROM applications WHERE id = {request.ApplicationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Application", request.ApplicationId);

        var applicant = await dbContext.Applicants.FindAsync([application.ApplicantId], cancellationToken)
            ?? throw new EntityNotFoundException("Applicant", application.ApplicantId);
        application.Applicant = applicant;

        if (application.Status is not (ApplicationStatus.Approved or ApplicationStatus.PartiallyPaid))
        {
            throw new InvalidStatusTransitionException(application.Status, ApplicationStatus.PartiallyPaid);
        }

        var fundCategory = await dbContext.FundCategories.FindAsync([application.FundCategoryId], cancellationToken)
            ?? throw new EntityNotFoundException("FundCategory", application.FundCategoryId);

        // D2/D7#5: a non-Zakat-fund application is a qard al-hasan loan and must have its
        // installment plan authored (POST api/loans) before any money can be disbursed against
        // it. Mirrored unconditionally by the fn_enforce_loan_plan_before_disbursement DB trigger.
        if (!fundCategory.IsZakat)
        {
            var hasActiveLoanAgreement = await dbContext.LoanAgreements
                .AnyAsync(l => l.ApplicationId == application.Id && l.Status == LoanAgreementStatus.Active, cancellationToken);

            if (!hasActiveLoanAgreement)
            {
                throw new LoanPlanRequiredException(application.ApplicationNumber);
            }
        }

        // Row lock on the fund too: two concurrent payments against different applications
        // drawing from the same fund must also be serialized, or both could read the same
        // balance and jointly overdraw it.
        await dbContext.Database.SqlQuery<int>(
            $"SELECT 1 FROM fund_categories WHERE id = {fundCategory.Id} FOR UPDATE")
            .ToListAsync(cancellationToken);

        var alreadyPaid = await dbContext.Payments
            .Where(p => p.ApplicationId == application.Id && p.Status == PaymentStatus.Completed)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        // Explicit guard: ApprovedAmount is decimal?, so the lifted comparison below silently
        // evaluates to false for every amount if it's ever null, defeating the cap entirely.
        if (application.ApprovedAmount is null)
        {
            throw new ApprovedAmountNotSetException();
        }

        if (alreadyPaid + request.Amount > application.ApprovedAmount)
        {
            throw new PaymentExceedsApprovedAmountException(application.ApprovedAmount.Value, alreadyPaid, request.Amount);
        }

        var balances = await fundCategoryService.GetBalancesAsync(cancellationToken);
        var fundBalance = balances.Single(b => b.FundCategoryId == fundCategory.Id);

        if (fundBalance.Balance < request.Amount)
        {
            throw new InsufficientFundBalanceException(fundCategory.Name, fundBalance.Balance, request.Amount);
        }

        var paymentNumber = await numberGenerator.NextAsync("Payment", cancellationToken);

        var payment = new Payment
        {
            PaymentNumber = paymentNumber,
            ApplicationId = application.Id,
            FundCategoryId = fundCategory.Id,
            Amount = request.Amount,
            PaymentDate = request.PaymentDate,
            PaymentMethod = Enum.Parse<PaymentMethod>(request.PaymentMethod),
            InstrumentNumber = request.InstrumentNumber,
            BankName = request.BankName,
            PaidToName = request.PaidToName,
            PaidToCnic = request.PaidToCnic,
            PaidToRelation = request.PaidToRelation,
            Remarks = request.Remarks,
        };
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.FundTransactions.Add(new FundTransaction
        {
            FundCategoryId = fundCategory.Id,
            Direction = TransactionDirection.Debit,
            Amount = payment.Amount,
            TransactionDate = payment.PaymentDate,
            ReferenceType = TransactionReferenceType.Payment,
            ReferenceId = payment.Id,
            Description = $"Payment {payment.PaymentNumber} for application {application.ApplicationNumber}",
        });

        var totalPaid = alreadyPaid + request.Amount;
        var previousStatus = application.RecomputeStatusFromPayments(totalPaid);
        if (previousStatus is not null)
        {
            AppendAutoStatusHistory(application.Id, previousStatus.Value, application.Status,
                $"Auto: payment {payment.PaymentNumber} recorded ({totalPaid:N2} of {application.ApprovedAmount:N2} paid).");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        payment.Application = application;
        payment.FundCategory = fundCategory;
        return Map(payment);
    }

    public async Task VoidAsync(Guid id, VoidPaymentRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var payment = await dbContext.Payments.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("Payment", id);

        if (payment.Status == PaymentStatus.Voided)
        {
            throw new AlreadyVoidedException("payment");
        }

        // Row lock on the owning application — the recomputed status below must not race with a
        // concurrent payment being created against the same application.
        var application = await dbContext.Applications
            .FromSqlInterpolated($"SELECT * FROM applications WHERE id = {payment.ApplicationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Application", payment.ApplicationId);

        payment.Status = PaymentStatus.Voided;
        payment.VoidedAt = DateTimeOffset.UtcNow;
        payment.VoidedBy = currentUser.UserId;
        payment.VoidReason = request.Reason;

        dbContext.FundTransactions.Add(new FundTransaction
        {
            FundCategoryId = payment.FundCategoryId,
            Direction = TransactionDirection.Credit,
            Amount = payment.Amount,
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ReferenceType = TransactionReferenceType.PaymentReversal,
            ReferenceId = payment.Id,
            Description = $"Void of payment {payment.PaymentNumber}: {request.Reason}",
        });

        // The DB still reflects this payment as Completed until SaveChangesAsync below, so it
        // must be excluded explicitly rather than relying on the in-memory status flip above.
        var totalCompletedPaid = await dbContext.Payments
            .Where(p => p.ApplicationId == application.Id && p.Status == PaymentStatus.Completed && p.Id != payment.Id)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var previousStatus = application.RecomputeStatusFromPayments(totalCompletedPaid);
        if (previousStatus is not null)
        {
            AppendAutoStatusHistory(application.Id, previousStatus.Value, application.Status,
                $"Auto: payment {payment.PaymentNumber} voided ({totalCompletedPaid:N2} of {application.ApprovedAmount:N2} paid).");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Journals a system-driven status change (see N1) — shared by <see cref="CreateAsync"/> and <see cref="VoidAsync"/>.</summary>
    private void AppendAutoStatusHistory(Guid applicationId, ApplicationStatus from, ApplicationStatus to, string remarks) =>
        dbContext.ApplicationStatusHistories.Add(AutoStatusHistoryFactory.Create(applicationId, from, to, currentUser.UserId, remarks));

    private static PaymentDto Map(Payment p) => new(
        p.Id, p.PaymentNumber, p.ApplicationId, p.Application.ApplicationNumber, p.Application.Applicant.FullName,
        p.FundCategoryId, p.FundCategory.Name, p.Amount, p.PaymentDate, p.PaymentMethod.ToString(),
        p.InstrumentNumber, p.BankName, p.PaidToName, p.PaidToCnic, p.PaidToRelation, p.Remarks,
        p.Status.ToString(), p.VoidedAt, p.VoidReason);
}
