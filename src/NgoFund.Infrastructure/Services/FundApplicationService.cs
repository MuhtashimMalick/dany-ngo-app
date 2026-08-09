using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Common;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

public class FundApplicationService(AppDbContext dbContext, INumberGenerator numberGenerator, ICurrentUserService currentUser) : IFundApplicationService
{
    public async Task<PagedResult<ApplicationDto>> GetApplicationsAsync(
        PagedQuery query, string? status, Guid? applicantId, Guid? categoryId, DateOnly? dateFrom, DateOnly? dateTo,
        CancellationToken cancellationToken)
    {
        var applicationsQuery = dbContext.Applications
            .AsNoTracking()
            .Include(a => a.Applicant)
            .Include(a => a.ApplicationCategory)
            .Include(a => a.FundCategory)
            .AsQueryable();

        if (status is not null && Enum.TryParse<ApplicationStatus>(status, out var statusEnum))
        {
            applicationsQuery = applicationsQuery.Where(a => a.Status == statusEnum);
        }

        if (applicantId is not null)
        {
            applicationsQuery = applicationsQuery.Where(a => a.ApplicantId == applicantId);
        }

        if (categoryId is not null)
        {
            applicationsQuery = applicationsQuery.Where(a => a.ApplicationCategoryId == categoryId);
        }

        if (dateFrom is not null)
        {
            applicationsQuery = applicationsQuery.Where(a => a.ApplicationDate >= dateFrom);
        }

        if (dateTo is not null)
        {
            applicationsQuery = applicationsQuery.Where(a => a.ApplicationDate <= dateTo);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            applicationsQuery = applicationsQuery.Where(a =>
                EF.Functions.ILike(a.ApplicationNumber, pattern) ||
                EF.Functions.ILike(a.Applicant.FullName, pattern) ||
                EF.Functions.ILike(a.Applicant.Cnic, pattern) ||
                (a.Applicant.MembershipNumber != null && EF.Functions.ILike(a.Applicant.MembershipNumber, pattern)));
        }

        var totalCount = await applicationsQuery.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var entities = await applicationsQuery
            .OrderByDescending(a => a.ApplicationDate)
            .ThenByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // One grouped query for the whole page instead of one payments-sum query per row.
        var applicationIds = entities.Select(a => a.Id).ToList();
        var paymentTotals = await dbContext.Payments
            .Where(p => applicationIds.Contains(p.ApplicationId) && p.Status == PaymentStatus.Completed)
            .GroupBy(p => p.ApplicationId)
            .Select(g => new { ApplicationId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.ApplicationId, x => x.Total, cancellationToken);

        return new PagedResult<ApplicationDto>(
            entities.Select(a => Map(a, paymentTotals.GetValueOrDefault(a.Id))).ToList(), totalCount, page, pageSize);
    }

    public async Task<ApplicationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Applications
            .AsNoTracking()
            .Include(a => a.Applicant)
            .Include(a => a.ApplicationCategory)
            .Include(a => a.FundCategory)
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("Application", id);

        var totalCompletedPaid = await GetTotalCompletedPaidAsync(id, cancellationToken);
        return Map(entity, totalCompletedPaid);
    }

    /// <summary>Shared by <see cref="GetByIdAsync"/>, <see cref="UpdateAsync"/> and <see cref="ChangeStatusAsync"/> so the sum-of-completed-payments query isn't duplicated three times.</summary>
    private async Task<decimal> GetTotalCompletedPaidAsync(Guid applicationId, CancellationToken cancellationToken) =>
        await dbContext.Payments
            .Where(p => p.ApplicationId == applicationId && p.Status == PaymentStatus.Completed)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

    public async Task<ApplicationDto> CreateAsync(CreateApplicationRequest request, CancellationToken cancellationToken)
    {
        var applicant = await dbContext.Applicants.FindAsync([request.ApplicantId], cancellationToken)
            ?? throw new EntityNotFoundException("Applicant", request.ApplicantId);

        var category = await dbContext.ApplicationCategories.FindAsync([request.ApplicationCategoryId], cancellationToken)
            ?? throw new EntityNotFoundException("ApplicationCategory", request.ApplicationCategoryId);

        var fund = await dbContext.FundCategories.FindAsync([request.FundCategoryId], cancellationToken)
            ?? throw new EntityNotFoundException("FundCategory", request.FundCategoryId);

        FundApplication.EnsureFundIsCompatible(category, fund);

        var applicationNumber = await numberGenerator.NextAsync("Application", cancellationToken);

        var entity = new FundApplication
        {
            ApplicationNumber = applicationNumber,
            ApplicantId = applicant.Id,
            ApplicationCategoryId = category.Id,
            FundCategoryId = fund.Id,
            RequestedAmount = request.RequestedAmount,
            Priority = Enum.Parse<ApplicationPriority>(request.Priority),
            ApplicationDate = request.ApplicationDate,
            Purpose = request.Purpose,
        };

        dbContext.Applications.Add(entity);

        dbContext.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            ApplicationId = entity.Id,
            FromStatus = null,
            ToStatus = ApplicationStatus.Pending,
            Remarks = "Application created.",
            ChangedAt = DateTimeOffset.UtcNow,
            ChangedBy = currentUser.UserId,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        entity.Applicant = applicant;
        entity.ApplicationCategory = category;
        entity.FundCategory = fund;
        return Map(entity, 0m); // freshly created: no payments can exist yet
    }

    public async Task<ApplicationDto> UpdateAsync(Guid id, UpdateApplicationRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Applications
            .Include(a => a.Applicant)
            .Include(a => a.ApplicationCategory)
            .Include(a => a.FundCategory)
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("Application", id);

        var category = request.ApplicationCategoryId == entity.ApplicationCategoryId
            ? entity.ApplicationCategory
            : await dbContext.ApplicationCategories.FindAsync([request.ApplicationCategoryId], cancellationToken)
                ?? throw new EntityNotFoundException("ApplicationCategory", request.ApplicationCategoryId);

        var fund = request.FundCategoryId == entity.FundCategoryId
            ? entity.FundCategory
            : await dbContext.FundCategories.FindAsync([request.FundCategoryId], cancellationToken)
                ?? throw new EntityNotFoundException("FundCategory", request.FundCategoryId);

        FundApplication.EnsureFundIsCompatible(category, fund);

        entity.ApplicationCategoryId = category.Id;
        entity.ApplicationCategory = category;
        entity.FundCategoryId = fund.Id;
        entity.FundCategory = fund;
        entity.RequestedAmount = request.RequestedAmount;

        // A completed payment already reflects money actually disbursed against the current
        // ApprovedAmount — rewriting it out from under the ledger (clearing it or setting it below
        // what's already paid) would let this endpoint reintroduce the exact corruption D3 closes
        // in PaymentService. Only applies once money has actually moved ( rule 4).
        var completedPaid = await GetTotalCompletedPaidAsync(entity.Id, cancellationToken);
        if (completedPaid > 0 && (request.ApprovedAmount is null || request.ApprovedAmount.Value < completedPaid))
        {
            throw new ApprovedAmountBelowCompletedPaymentsException(request.ApprovedAmount, completedPaid);
        }

        entity.ApprovedAmount = request.ApprovedAmount;
        entity.Priority = Enum.Parse<ApplicationPriority>(request.Priority);
        entity.Purpose = request.Purpose;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity, completedPaid);
    }

    public async Task ChangeStatusAsync(Guid id, ChangeApplicationStatusRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Applications.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("Application", id);

        var newStatus = Enum.Parse<ApplicationStatus>(request.NewStatus);
        var fromStatus = entity.Status;

        var totalCompletedPaid = await GetTotalCompletedPaidAsync(entity.Id, cancellationToken);
        entity.TransitionTo(newStatus, totalCompletedPaid);

        var now = DateTimeOffset.UtcNow;
        switch (newStatus)
        {
            case ApplicationStatus.UnderReview:
                entity.ReviewedAt = now;
                entity.ReviewedBy = currentUser.UserId;
                break;
            case ApplicationStatus.Approved:
                entity.ApprovedAt = now;
                entity.ApprovedBy = currentUser.UserId;
                entity.ApprovedAmount ??= entity.RequestedAmount;
                break;
            case ApplicationStatus.Rejected:
                entity.RejectionReason = request.RejectionReason;
                entity.ClosedAt = now;
                break;
        }

        dbContext.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            ApplicationId = entity.Id,
            FromStatus = fromStatus,
            ToStatus = newStatus,
            Remarks = request.Remarks,
            ChangedAt = now,
            ChangedBy = currentUser.UserId,
        });

        // OnHold -> Approved is now reachable even when payments already exist (D1's new arrow).
        // Immediately re-settle to ledger truth (N1) instead of leaving it incorrectly sitting at
        // Approved, and journal that second, system-attributed transition too.
        if (newStatus == ApplicationStatus.Approved)
        {
            var settledFrom = entity.RecomputeStatusFromPayments(totalCompletedPaid);
            if (settledFrom is not null)
            {
                dbContext.ApplicationStatusHistories.Add(AutoStatusHistoryFactory.Create(
                    entity.Id, settledFrom.Value, entity.Status, currentUser.UserId,
                    $"Auto: application re-settled from actual completed payments ({totalCompletedPaid:N2} of {entity.ApprovedAmount:N2} paid)."));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationStatusHistoryDto>> GetStatusHistoryAsync(Guid id, CancellationToken cancellationToken)
    {
        var history = await dbContext.ApplicationStatusHistories
            .AsNoTracking()
            .Where(h => h.ApplicationId == id)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync(cancellationToken);

        return history.Select(h => new ApplicationStatusHistoryDto(h.Id, h.FromStatus?.ToString(), h.ToStatus.ToString(), h.Remarks, h.ChangedAt)).ToList();
    }

    public async Task<IReadOnlyList<ApplicationRemarkDto>> GetRemarksAsync(Guid id, CancellationToken cancellationToken)
    {
        var remarks = await dbContext.ApplicationRemarks
            .AsNoTracking()
            .Where(r => r.ApplicationId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return remarks.Select(r => new ApplicationRemarkDto(r.Id, r.Remark, r.IsInternal, r.CreatedAt)).ToList();
    }

    public async Task<ApplicationRemarkDto> AddRemarkAsync(Guid id, AddRemarkRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Applications.AnyAsync(a => a.Id == id, cancellationToken))
        {
            throw new EntityNotFoundException("Application", id);
        }

        var remark = new ApplicationRemark
        {
            ApplicationId = id,
            Remark = request.Remark,
            IsInternal = request.IsInternal,
        };

        dbContext.ApplicationRemarks.Add(remark);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApplicationRemarkDto(remark.Id, remark.Remark, remark.IsInternal, remark.CreatedAt);
    }

    private static ApplicationDto Map(FundApplication a, decimal totalCompletedPaid) => new(
        a.Id, a.ApplicationNumber, a.ApplicantId, a.Applicant.FullName, a.Applicant.Cnic,
        a.ApplicationCategoryId, a.ApplicationCategory.Name, a.FundCategoryId, a.FundCategory.Name,
        a.RequestedAmount, a.ApprovedAmount, a.Status.ToString(), a.Priority.ToString(), a.ApplicationDate,
        a.Purpose, a.ReviewedAt, a.ApprovedAt, a.RejectionReason,
        FundApplication.GetAllowedNextStatuses(a.Status, totalCompletedPaid).Select(s => s.ToString()).ToList());
}
