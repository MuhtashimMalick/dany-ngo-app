using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Documents;
using NgoFund.Domain.Applications;
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
                (a.Applicant.MembershipNumber != null && EF.Functions.ILike(a.Applicant.MembershipNumber, pattern)) ||
                (a.Applicant.Surname != null && EF.Functions.ILike(a.Applicant.Surname, pattern)));
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

        // Same batching approach for the Active loan agreement (at most one per application, per
        // the partial unique index) that backs RequiresLoanPlan/LoanAgreementId.
        var activeLoanAgreementIds = await dbContext.LoanAgreements
            .Where(l => applicationIds.Contains(l.ApplicationId) && l.Status == LoanAgreementStatus.Active)
            .ToDictionaryAsync(l => l.ApplicationId, l => (Guid?)l.Id, cancellationToken);

        // Same batching approach for the guarantor counts that back RequiresGuarantors/GuarantorCount.
        var guarantorCounts = await dbContext.ApplicationGuarantors
            .Where(g => applicationIds.Contains(g.ApplicationId))
            .GroupBy(g => g.ApplicationId)
            .Select(g => new { ApplicationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ApplicationId, x => x.Count, cancellationToken);

        // Item 3 (2026-09 feedback), the duplicate-active-application badge: one grouped query for
        // the whole page's distinct applicants, not one per row. Counts across ALL of the
        // applicant's applications (not just this page), so it's correct even when only one of an
        // applicant's several applications happens to land on the current page.
        var applicantIds = entities.Select(a => a.ApplicantId).Distinct().ToList();
        var activeStatuses = ApplicationStatusRules.ActiveStatuses;
        var activeApplicationCounts = await dbContext.Applications
            .Where(a => applicantIds.Contains(a.ApplicantId) && activeStatuses.Contains(a.Status))
            .GroupBy(a => a.ApplicantId)
            .Select(g => new { ApplicantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ApplicantId, x => x.Count, cancellationToken);

        // Mirror direction of item 4: one batched lookup for the whole page instead of one per row.
        var (applicantGuarantorConflicts, applicantGuarantorConflictApprovers) = await LoadApplicantGuarantorConflictsAsync(entities, cancellationToken);

        return new PagedResult<ApplicationDto>(
            entities.Select(a => Map(
                a, paymentTotals.GetValueOrDefault(a.Id), activeLoanAgreementIds.GetValueOrDefault(a.Id),
                guarantorCounts.GetValueOrDefault(a.Id), activeApplicationCounts.GetValueOrDefault(a.ApplicantId),
                applicantGuarantorConflicts.GetValueOrDefault(a.Id, []),
                a.ApplicantGuarantorConflictOverrideApprovedBy is not null
                    ? applicantGuarantorConflictApprovers.GetValueOrDefault(a.ApplicantGuarantorConflictOverrideApprovedBy.Value)
                    : null)).ToList(),
            totalCount, page, pageSize);
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
        var activeLoanAgreementId = await GetActiveLoanAgreementIdAsync(id, cancellationToken);
        var guarantorCount = await dbContext.ApplicationGuarantors.CountAsync(g => g.ApplicationId == id, cancellationToken);
        var activeApplicationCount = await GetActiveApplicationCountAsync(entity.ApplicantId, cancellationToken);
        var (conflictNumbers, conflictApproverName) = await LoadApplicantGuarantorConflictAsync(entity, cancellationToken);
        return Map(entity, totalCompletedPaid, activeLoanAgreementId, guarantorCount, activeApplicationCount, conflictNumbers, conflictApproverName);
    }

    /// <summary>Shared by <see cref="GetApplicationsAsync"/>, <see cref="GetByIdAsync"/>,
    /// <see cref="CreateAsync"/> and <see cref="UpdateAsync"/> — see <c>ApplicationDto.ActiveApplicationCount</c>.</summary>
    private async Task<int> GetActiveApplicationCountAsync(Guid applicantId, CancellationToken cancellationToken)
    {
        var activeStatuses = ApplicationStatusRules.ActiveStatuses;
        return await dbContext.Applications.CountAsync(a => a.ApplicantId == applicantId && activeStatuses.Contains(a.Status), cancellationToken);
    }

    /// <summary>Mirror direction of item 4: batches <see cref="ApplicantGuarantorConflictLookup"/> and
    /// the override-approver name lookup across a whole page of already-loaded applications (each
    /// with <c>.Applicant.Cnic</c> loaded) — same pattern as
    /// <see cref="ApplicationDetailsService.MapWithConflictsAsync"/>, with the same
    /// <c>approverIds.Count == 0</c> early-out so a page with zero overrides costs nothing extra.
    /// Shared by <see cref="GetApplicationsAsync"/> (via <see cref="LoadApplicantGuarantorConflictAsync"/>
    /// too, for the single-entity call sites).</summary>
    private async Task<(Dictionary<Guid, List<string>> Conflicts, Dictionary<Guid, string> ApproverNames)> LoadApplicantGuarantorConflictsAsync(
        IReadOnlyList<FundApplication> entities, CancellationToken cancellationToken)
    {
        var conflicts = await ApplicantGuarantorConflictLookup.FindConflictsAsync(
            dbContext, entities.Select(a => (a.Id, a.Applicant.Cnic)).ToList(), cancellationToken);

        var approverIds = entities
            .Where(a => a.ApplicantGuarantorConflictOverrideApprovedBy is not null)
            .Select(a => a.ApplicantGuarantorConflictOverrideApprovedBy!.Value)
            .Distinct()
            .ToList();
        var approverNames = approverIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Users.Where(u => approverIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return (conflicts, approverNames);
    }

    /// <summary>Single-entity convenience over <see cref="LoadApplicantGuarantorConflictsAsync"/> for
    /// <see cref="GetByIdAsync"/>, <see cref="CreateAsync"/>, and <see cref="UpdateAsync"/> — still one
    /// query each, just against a one-element batch.</summary>
    private async Task<(IReadOnlyList<string> ConflictNumbers, string? ApproverName)> LoadApplicantGuarantorConflictAsync(
        FundApplication entity, CancellationToken cancellationToken)
    {
        var (conflicts, approverNames) = await LoadApplicantGuarantorConflictsAsync([entity], cancellationToken);
        var numbers = conflicts.GetValueOrDefault(entity.Id, []);
        var approverName = entity.ApplicantGuarantorConflictOverrideApprovedBy is not null
            ? approverNames.GetValueOrDefault(entity.ApplicantGuarantorConflictOverrideApprovedBy.Value)
            : null;
        return (numbers, approverName);
    }

    /// <summary>Shared by <see cref="GetByIdAsync"/>, <see cref="UpdateAsync"/> and <see cref="ChangeStatusAsync"/> so the sum-of-completed-payments query isn't duplicated three times.</summary>
    private async Task<decimal> GetTotalCompletedPaidAsync(Guid applicationId, CancellationToken cancellationToken) =>
        await dbContext.Payments
            .Where(p => p.ApplicationId == applicationId && p.Status == PaymentStatus.Completed)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

    /// <summary>The application's current Active loan agreement, if any — at most one per the
    /// partial unique index on <c>loan_agreements.application_id</c>.</summary>
    private async Task<LoanAgreement?> GetActiveLoanAgreementAsync(Guid applicationId, CancellationToken cancellationToken) =>
        await dbContext.LoanAgreements
            .SingleOrDefaultAsync(l => l.ApplicationId == applicationId && l.Status == LoanAgreementStatus.Active, cancellationToken);

    private async Task<Guid?> GetActiveLoanAgreementIdAsync(Guid applicationId, CancellationToken cancellationToken) =>
        await dbContext.LoanAgreements
            .Where(l => l.ApplicationId == applicationId && l.Status == LoanAgreementStatus.Active)
            .Select(l => (Guid?)l.Id)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Whether the application's CURRENT category already has category-scoped data on
    /// file: a details row (housing/marriage/business-loan, whichever matches
    /// <c>entity.ApplicationCategory.Code</c>), any guarantor rows, or any document saved into a
    /// slot. Backs <see cref="FundApplication.EnsureCategoryChangeAllowed"/> — see that method for
    /// why a category change is blocked once any of this exists. Requires
    /// <paramref name="entity"/>.ApplicationCategory already loaded.</summary>
    private async Task<bool> HasCategoryScopedDataAsync(FundApplication entity, CancellationToken cancellationToken)
    {
        var hasDetails = entity.ApplicationCategory.Code switch
        {
            "HOUSE_RENT" => await dbContext.HousingApplicationDetails.AnyAsync(d => d.ApplicationId == entity.Id, cancellationToken),
            "SHAADI" => await dbContext.MarriageApplicationDetails.AnyAsync(d => d.ApplicationId == entity.Id, cancellationToken),
            "ROZGAR" => await dbContext.BusinessLoanApplicationDetails.AnyAsync(d => d.ApplicationId == entity.Id, cancellationToken),
            _ => false,
        };
        if (hasDetails)
        {
            return true;
        }

        if (await dbContext.ApplicationGuarantors.AnyAsync(g => g.ApplicationId == entity.Id, cancellationToken))
        {
            return true;
        }

        return await dbContext.Documents.AnyAsync(d => d.ApplicationId == entity.Id && d.SlotKey != null, cancellationToken);
    }

    /// <summary>Pre-check for the partial unique index on <c>applications.external_form_reference</c>
    /// (staff re-keying the same Google Form response twice) — mirrors <c>ApplicantService</c>'s
    /// CNIC/membership-number pre-check so the duplicate maps to a clean 422 instead of falling
    /// through to a raw 500 from the DB constraint.</summary>
    private async Task EnsureExternalFormReferenceUniqueAsync(string? externalFormReference, Guid? existingApplicationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(externalFormReference))
        {
            return;
        }

        if (await dbContext.Applications.AnyAsync(
            a => a.ExternalFormReference == externalFormReference && a.Id != existingApplicationId, cancellationToken))
        {
            throw new DuplicateFieldException("application", "external form reference", externalFormReference);
        }
    }

    public async Task<ApplicationDto> CreateAsync(CreateApplicationRequest request, CancellationToken cancellationToken)
    {
        var applicant = await dbContext.Applicants.FindAsync([request.ApplicantId], cancellationToken)
            ?? throw new EntityNotFoundException("Applicant", request.ApplicantId);

        var category = await dbContext.ApplicationCategories.FindAsync([request.ApplicationCategoryId], cancellationToken)
            ?? throw new EntityNotFoundException("ApplicationCategory", request.ApplicationCategoryId);

        var fund = await dbContext.FundCategories.FindAsync([request.FundCategoryId], cancellationToken)
            ?? throw new EntityNotFoundException("FundCategory", request.FundCategoryId);

        FundApplication.EnsureFundIsCompatible(category, fund);

        await EnsureExternalFormReferenceUniqueAsync(request.ExternalFormReference, existingApplicationId: null, cancellationToken);

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
            DeclaredMonthlyIncome = request.DeclaredMonthlyIncome,
            DeclaredHouseholdSize = request.DeclaredHouseholdSize,
            DeclaredEarningMembers = request.DeclaredEarningMembers,
            DeclaredResidentialAddress = request.DeclaredResidentialAddress,
            DeclaredBusinessAddress = request.DeclaredBusinessAddress,
            DeclaredHouseStatus = request.DeclaredHouseStatus is null ? null : Enum.Parse<HouseStatus>(request.DeclaredHouseStatus),
            IntakeChannel = Enum.Parse<ApplicationIntakeChannel>(request.IntakeChannel),
            ExternalFormReference = request.ExternalFormReference,
            SubmittedAt = request.SubmittedAt,
            DeclarationAcceptedAt = request.DeclarationAcceptedAt,
            TermsAcceptedAt = request.TermsAcceptedAt,
            TermsVersion = request.TermsVersion,
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
        // Item 3 (2026-09 feedback): unlike payments/loan agreement/guarantors, the active-count
        // must be a real query, not a literal 0 — this application (Pending, itself active) can
        // already be the applicant's second (or later) active application.
        var activeApplicationCount = await GetActiveApplicationCountAsync(applicant.Id, cancellationToken);
        // Mirror direction of item 4: also a real query, not a literal empty list — the applicant
        // could already be an active guarantor elsewhere the instant this application is created.
        var (conflictNumbers, conflictApproverName) = await LoadApplicantGuarantorConflictAsync(entity, cancellationToken);
        return Map(entity, 0m, null, 0, activeApplicationCount, conflictNumbers, conflictApproverName);
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

        // Category-scoped data (E7's flip side): the details row / guarantors / slotted documents
        // already on file are all keyed to the CURRENT category. Reassigning ApplicationCategoryId
        // out from under them would leave them permanently unreachable behind
        // ApplicationDetailsService.EnsureCategoryAsync's mismatch guard instead of migrating or
        // deleting them, so block the change once any of that data exists. A fresh application with
        // nothing saved yet may still have its category corrected.
        if (category.Id != entity.ApplicationCategoryId)
        {
            var hasCategoryScopedData = await HasCategoryScopedDataAsync(entity, cancellationToken);
            FundApplication.EnsureCategoryChangeAllowed(entity.ApplicationNumber, entity.ApplicationCategory.Name, hasCategoryScopedData);
        }

        // D3 corollary: an Active loan agreement has frozen this application's approved_amount
        // and fund onto its own principal_amount/fund_category_id — changing either out from
        // under it here would silently desync the two. Checked against the PRE-mutation values,
        // mirroring ApprovedAmountBelowCompletedPaymentsException's guard pattern.
        var activeLoanAgreement = await GetActiveLoanAgreementAsync(entity.Id, cancellationToken);
        if (activeLoanAgreement is not null)
        {
            if (request.ApprovedAmount != entity.ApprovedAmount)
            {
                throw new LoanAgreementLockedException(
                    $"Cannot change the approved amount while loan agreement {activeLoanAgreement.LoanNumber} is Active for this application.");
            }

            if (request.FundCategoryId != entity.FundCategoryId)
            {
                throw new LoanAgreementLockedException(
                    $"Cannot change the fund category while loan agreement {activeLoanAgreement.LoanNumber} is Active for this application.");
            }
        }

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

        // Item 1 (2026-09 feedback) corollary: even before any payment exists yet, an application
        // that has already moved into the payment lifecycle (Approved/PartiallyPaid/Paid) must never
        // have its approved amount nulled out here — that would silently undo the Approved-transition
        // gate's requirement that an approved amount be set (ChangeStatusAsync).
        if (request.ApprovedAmount is null && entity.Status is ApplicationStatus.Approved or ApplicationStatus.PartiallyPaid or ApplicationStatus.Paid)
        {
            throw new ApprovedAmountCannotBeClearedException();
        }

        entity.ApprovedAmount = request.ApprovedAmount;
        entity.Priority = Enum.Parse<ApplicationPriority>(request.Priority);
        entity.Purpose = request.Purpose;
        entity.DeclaredMonthlyIncome = request.DeclaredMonthlyIncome;
        entity.DeclaredHouseholdSize = request.DeclaredHouseholdSize;
        entity.DeclaredEarningMembers = request.DeclaredEarningMembers;
        entity.DeclaredResidentialAddress = request.DeclaredResidentialAddress;
        entity.DeclaredBusinessAddress = request.DeclaredBusinessAddress;
        entity.DeclaredHouseStatus = request.DeclaredHouseStatus is null ? null : Enum.Parse<HouseStatus>(request.DeclaredHouseStatus);

        entity.DeclarationAcceptedAt = request.DeclarationAcceptedAt;
        entity.TermsAcceptedAt = request.TermsAcceptedAt;
        entity.TermsVersion = request.TermsVersion;

        await dbContext.SaveChangesAsync(cancellationToken);

        var guarantorCount = await dbContext.ApplicationGuarantors.CountAsync(g => g.ApplicationId == entity.Id, cancellationToken);
        var activeApplicationCount = await GetActiveApplicationCountAsync(entity.ApplicantId, cancellationToken);
        var (conflictNumbers, conflictApproverName) = await LoadApplicantGuarantorConflictAsync(entity, cancellationToken);
        return Map(entity, completedPaid, activeLoanAgreement?.Id, guarantorCount, activeApplicationCount, conflictNumbers, conflictApproverName);
    }

    public async Task ChangeStatusAsync(Guid id, ChangeApplicationStatusRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Applications
            .Include(a => a.ApplicationCategory)
            .Include(a => a.Applicant)
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("Application", id);

        var newStatus = Enum.Parse<ApplicationStatus>(request.NewStatus);
        var fromStatus = entity.Status;
        var totalCompletedPaid = await GetTotalCompletedPaidAsync(entity.Id, cancellationToken);

        // The guarantor gate (E2): a category with RequiresGuarantors > 0 (2 for ROZGAR) needs at
        // least that many ApplicationGuarantor rows on file before Approved. Checked before
        // TransitionTo so a category that fails this never even reaches the state-machine check.
        if (newStatus == ApplicationStatus.Approved)
        {
            var guarantors = await dbContext.ApplicationGuarantors.Where(g => g.ApplicationId == entity.Id).ToListAsync(cancellationToken);
            FundApplication.EnsureGuarantorsSatisfied(entity.ApplicationNumber, entity.ApplicationCategory, guarantors.Count);

            // The completeness gate: the fix for the client's bug report (applications reaching
            // Approved with zero category-specific details and zero required documents).
            var completeness = await LoadCompletenessAsync(entity, cancellationToken);
            FundApplication.EnsureApplicationIsComplete(entity.ApplicationNumber, completeness);

            // Item 4 (2026-09 feedback): a guarantor whose CNIC also appears on another currently
            // active application blocks Approved unless staff have approved a conflict override.
            var conflicts = await GuarantorConflictLookup.FindConflictsAsync(dbContext, entity.Id, guarantors, cancellationToken);
            var unresolvedGuarantorIds = guarantors
                .Where(g => conflicts.ContainsKey(g.Id) && g.ConflictOverrideApprovedAt is null)
                .Select(g => g.Id)
                .ToList();
            FundApplication.EnsureGuarantorConflictsResolved(entity.ApplicationNumber, unresolvedGuarantorIds);

            // Mirror direction of item 4: this application's own applicant being an active guarantor
            // elsewhere blocks Approved unless staff have approved an applicant-guarantor conflict
            // override for THIS application.
            var applicantConflicts = await ApplicantGuarantorConflictLookup.FindConflictsAsync(
                dbContext, [(entity.Id, entity.Applicant.Cnic)], cancellationToken);
            FundApplication.EnsureApplicantNotActiveGuarantorElsewhere(
                entity.ApplicationNumber,
                applicantConflicts.GetValueOrDefault(entity.Id, []),
                entity.HasValidApplicantGuarantorConflictOverride(entity.Applicant.Cnic));

            // Item 1 (2026-09 feedback): the approved amount is now an explicit reviewer decision,
            // never a silent default to RequestedAmount. Omitting it is only legal when re-approving
            // (OnHold -> Approved) an application that already has one on file, in which case the
            // existing value is kept as-is. It may exceed RequestedAmount (v1.8 correction — a
            // committee of elders may decide the requested amount was too low).
            if (request.ApprovedAmount is not null)
            {
                if (request.ApprovedAmount.Value < totalCompletedPaid)
                {
                    throw new ApprovedAmountBelowCompletedPaymentsException(request.ApprovedAmount, totalCompletedPaid);
                }

                entity.ApprovedAmount = request.ApprovedAmount;
            }
            else if (entity.ApprovedAmount is null)
            {
                throw new ApprovedAmountNotSetException();
            }
        }

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

    /// <summary>Mirror direction of item 4's guarantor-row override endpoint
    /// (<c>ApplicationDetailsService.ApproveGuarantorConflictOverrideAsync</c>), but application-level:
    /// stamps the applicant's CURRENT CNIC alongside the approval so the override self-heals if the
    /// CNIC is later edited via ApplicantService — see <c>FundApplication.HasValidApplicantGuarantorConflictOverride</c>.</summary>
    public async Task<ApplicationDto> ApproveApplicantGuarantorConflictOverrideAsync(
        Guid id, ApproveApplicantGuarantorConflictOverrideRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Applications
            .Include(a => a.Applicant)
            .Include(a => a.ApplicationCategory)
            .Include(a => a.FundCategory)
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("Application", id);

        entity.ApplicantGuarantorConflictOverrideApprovedAt = DateTimeOffset.UtcNow;
        entity.ApplicantGuarantorConflictOverrideApprovedBy = currentUser.UserId;
        entity.ApplicantGuarantorConflictOverrideReason = request.Reason;
        entity.ApplicantGuarantorConflictOverrideCnic = entity.Applicant.Cnic;

        await dbContext.SaveChangesAsync(cancellationToken);

        var totalCompletedPaid = await GetTotalCompletedPaidAsync(entity.Id, cancellationToken);
        var activeLoanAgreementId = await GetActiveLoanAgreementIdAsync(entity.Id, cancellationToken);
        var guarantorCount = await dbContext.ApplicationGuarantors.CountAsync(g => g.ApplicationId == entity.Id, cancellationToken);
        var activeApplicationCount = await GetActiveApplicationCountAsync(entity.ApplicantId, cancellationToken);
        var (conflictNumbers, conflictApproverName) = await LoadApplicantGuarantorConflictAsync(entity, cancellationToken);
        return Map(entity, totalCompletedPaid, activeLoanAgreementId, guarantorCount, activeApplicationCount, conflictNumbers, conflictApproverName);
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

    public async Task<ApplicationCompletenessDto> GetCompletenessAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Applications
            .AsNoTracking()
            .Include(a => a.ApplicationCategory)
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("Application", id);

        var result = await LoadCompletenessAsync(entity, cancellationToken);
        return MapCompleteness(entity.Id, result);
    }

    /// <summary>Loads everything <see cref="ApplicationCompletenessEvaluator.Evaluate"/> needs — the
    /// category-specific details row (whichever one applies), the guarantor list, and every document
    /// on file for the application, one of its guarantors, or (v1.4, B5) the applicant themselves —
    /// and runs the evaluation. Shared by the Approved-transition gate and
    /// <see cref="GetCompletenessAsync"/> so they can never disagree about what "complete" means.
    /// Requires <paramref name="application"/>.ApplicationCategory already loaded;
    /// <paramref name="application"/>.ApplicantId is a scalar FK, so it's always available without
    /// an extra Include.</summary>
    private async Task<ApplicationCompletenessResult> LoadCompletenessAsync(FundApplication application, CancellationToken cancellationToken)
    {
        var category = application.ApplicationCategory;

        var housing = category.Code == "HOUSE_RENT"
            ? await dbContext.HousingApplicationDetails.AsNoTracking().SingleOrDefaultAsync(d => d.ApplicationId == application.Id, cancellationToken)
            : null;
        var marriage = category.Code == "SHAADI"
            ? await dbContext.MarriageApplicationDetails.AsNoTracking().SingleOrDefaultAsync(d => d.ApplicationId == application.Id, cancellationToken)
            : null;
        var businessLoan = category.Code == "ROZGAR"
            ? await dbContext.BusinessLoanApplicationDetails.AsNoTracking().SingleOrDefaultAsync(d => d.ApplicationId == application.Id, cancellationToken)
            : null;

        var guarantors = await dbContext.ApplicationGuarantors.AsNoTracking()
            .Where(g => g.ApplicationId == application.Id)
            .OrderBy(g => g.SequenceNumber)
            .ToListAsync(cancellationToken);

        var guarantorIds = guarantors.Select(g => g.Id).ToList();
        var documents = await dbContext.Documents.AsNoTracking()
            .Where(d => d.ApplicationId == application.Id
                || (d.ApplicationGuarantorId != null && guarantorIds.Contains(d.ApplicationGuarantorId.Value))
                || d.ApplicantId == application.ApplicantId)
            .ToListAsync(cancellationToken);

        return ApplicationCompletenessEvaluator.Evaluate(application, category, housing, marriage, businessLoan, documents, guarantors);
    }

    private static ApplicationCompletenessDto MapCompleteness(Guid applicationId, ApplicationCompletenessResult result) => new(
        applicationId,
        result.IsComplete,
        result.MissingFields.Select(f => new MissingFieldDto(f.Key, f.Label, f.SectionLabel)).ToList(),
        result.Slots.Select(s => new DocumentSlotStatusDto(
            s.Slot.SlotKey,
            s.Slot.Label,
            s.Slot.IsRequired,
            s.Slot.MinCount,
            s.Slot.OwnerScope.ToString(),
            s.Slot.AcceptedTypes.Select(t => t.ToString()).ToList(),
            s.Slot.AcceptedTypes[0].ToString(),
            s.GuarantorId,
            s.GuarantorSequenceNumber,
            s.GuarantorName,
            s.Documents.Select(MapDocument).ToList(),
            s.IsSatisfied,
            s.SatisfiedByApplicantProfile)).ToList());

    private static DocumentDto MapDocument(Document d) => new(d.Id, d.FileName, d.ContentType, d.SizeBytes, d.DocumentType.ToString(), d.Description, d.UploadedAt, d.SlotKey);

    private static ApplicationDto Map(
        FundApplication a, decimal totalCompletedPaid, Guid? activeLoanAgreementId, int guarantorCount, int activeApplicationCount,
        IReadOnlyList<string> applicantGuarantorConflictNumbers, string? applicantGuarantorConflictOverrideApprovedByName)
    {
        // The read side and the write-side gate (FundApplication.EnsureApplicantNotActiveGuarantorElsewhere)
        // must never disagree about whether an override still counts, hence routing through the same
        // instance method here rather than re-deriving the condition inline.
        var hasValidOverride = a.HasValidApplicantGuarantorConflictOverride(a.Applicant.Cnic);
        return MapCore(a, totalCompletedPaid, activeLoanAgreementId, guarantorCount, activeApplicationCount,
            applicantGuarantorConflictNumbers, hasValidOverride, applicantGuarantorConflictOverrideApprovedByName);
    }

    private static ApplicationDto MapCore(
        FundApplication a, decimal totalCompletedPaid, Guid? activeLoanAgreementId, int guarantorCount, int activeApplicationCount,
        IReadOnlyList<string> applicantGuarantorConflictNumbers, bool hasValidApplicantGuarantorConflictOverride,
        string? applicantGuarantorConflictOverrideApprovedByName) => new(
        a.Id, a.ApplicationNumber, a.ApplicantId, a.Applicant.FullName, a.Applicant.Cnic,
        a.ApplicationCategoryId, a.ApplicationCategory.Name, a.ApplicationCategory.Code, a.FundCategoryId, a.FundCategory.Name,
        a.RequestedAmount, a.ApprovedAmount, a.Status.ToString(), a.Priority.ToString(), a.ApplicationDate,
        a.Purpose, a.ReviewedAt, a.ApprovedAt, a.RejectionReason,
        FundApplication.GetAllowedNextStatuses(a.Status, totalCompletedPaid).Select(s => s.ToString()).ToList(),
        // A non-Zakat fund needs a loan plan before it can accept payments (D2); once an Active
        // agreement exists, the application already has one.
        RequiresLoanPlan: !a.FundCategory.IsZakat && activeLoanAgreementId is null,
        LoanAgreementId: activeLoanAgreementId,
        DeclaredMonthlyIncome: a.DeclaredMonthlyIncome,
        DeclaredHouseholdSize: a.DeclaredHouseholdSize,
        DeclaredEarningMembers: a.DeclaredEarningMembers,
        DeclaredResidentialAddress: a.DeclaredResidentialAddress,
        DeclaredBusinessAddress: a.DeclaredBusinessAddress,
        DeclaredHouseStatus: a.DeclaredHouseStatus?.ToString(),
        IntakeChannel: a.IntakeChannel.ToString(),
        ExternalFormReference: a.ExternalFormReference,
        SubmittedAt: a.SubmittedAt,
        DeclarationAcceptedAt: a.DeclarationAcceptedAt,
        TermsAcceptedAt: a.TermsAcceptedAt,
        TermsVersion: a.TermsVersion,
        RequiresGuarantors: a.ApplicationCategory.RequiresGuarantors,
        GuarantorCount: guarantorCount,
        // The blocking-confirmation signal (E1): a dual-eligible (FundEligibility.Either) category
        // currently pointed at the General (non-Zakat) fund — under the final mapping this only
        // ever fires for OTHER, the sole dual-eligible category; ROZGAR (GeneralOnly) is forced
        // onto General and correctly never flags this. Distinct from RequiresLoanPlan, which is
        // about whether payments need a loan agreement first, not about whether this fund choice
        // is the unusual one.
        DualEligibleCategoryOnGeneralFund: a.ApplicationCategory.FundEligibility == FundEligibility.Either && !a.FundCategory.IsZakat,
        ActiveApplicationCount: activeApplicationCount,
        ApplicantGuarantorConflictApplicationNumbers: applicantGuarantorConflictNumbers,
        ApplicantGuarantorConflictOverrideApprovedAt: hasValidApplicantGuarantorConflictOverride ? a.ApplicantGuarantorConflictOverrideApprovedAt : null,
        ApplicantGuarantorConflictOverrideApprovedByName: hasValidApplicantGuarantorConflictOverride ? applicantGuarantorConflictOverrideApprovedByName : null,
        ApplicantGuarantorConflictOverrideReason: hasValidApplicantGuarantorConflictOverride ? a.ApplicantGuarantorConflictOverrideReason : null);
}
