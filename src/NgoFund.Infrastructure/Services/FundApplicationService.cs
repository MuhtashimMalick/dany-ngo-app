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

        return new PagedResult<ApplicationDto>(
            entities.Select(a => Map(
                a, paymentTotals.GetValueOrDefault(a.Id), activeLoanAgreementIds.GetValueOrDefault(a.Id),
                guarantorCounts.GetValueOrDefault(a.Id))).ToList(),
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
        return Map(entity, totalCompletedPaid, activeLoanAgreementId, guarantorCount);
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
        return Map(entity, 0m, null, 0); // freshly created: no payments, loan agreement, or guarantors can exist yet
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

        entity.ApprovedAmount = request.ApprovedAmount;
        entity.Priority = Enum.Parse<ApplicationPriority>(request.Priority);
        entity.Purpose = request.Purpose;
        entity.DeclaredMonthlyIncome = request.DeclaredMonthlyIncome;
        entity.DeclaredHouseholdSize = request.DeclaredHouseholdSize;
        entity.DeclaredEarningMembers = request.DeclaredEarningMembers;
        entity.DeclaredResidentialAddress = request.DeclaredResidentialAddress;
        entity.DeclaredBusinessAddress = request.DeclaredBusinessAddress;
        entity.DeclaredHouseStatus = request.DeclaredHouseStatus is null ? null : Enum.Parse<HouseStatus>(request.DeclaredHouseStatus);

        await EnsureExternalFormReferenceUniqueAsync(request.ExternalFormReference, existingApplicationId: entity.Id, cancellationToken);
        entity.ExternalFormReference = request.ExternalFormReference;
        entity.SubmittedAt = request.SubmittedAt;
        entity.DeclarationAcceptedAt = request.DeclarationAcceptedAt;
        entity.TermsAcceptedAt = request.TermsAcceptedAt;
        entity.TermsVersion = request.TermsVersion;

        await dbContext.SaveChangesAsync(cancellationToken);

        var guarantorCount = await dbContext.ApplicationGuarantors.CountAsync(g => g.ApplicationId == entity.Id, cancellationToken);
        return Map(entity, completedPaid, activeLoanAgreement?.Id, guarantorCount);
    }

    public async Task ChangeStatusAsync(Guid id, ChangeApplicationStatusRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Applications
            .Include(a => a.ApplicationCategory)
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("Application", id);

        var newStatus = Enum.Parse<ApplicationStatus>(request.NewStatus);
        var fromStatus = entity.Status;

        // The guarantor gate (E2): a category with RequiresGuarantors > 0 (2 for ROZGAR) needs at
        // least that many ApplicationGuarantor rows on file before Approved. Checked before
        // TransitionTo so a category that fails this never even reaches the state-machine check.
        if (newStatus == ApplicationStatus.Approved)
        {
            var guarantorCount = await dbContext.ApplicationGuarantors.CountAsync(g => g.ApplicationId == entity.Id, cancellationToken);
            FundApplication.EnsureGuarantorsSatisfied(entity.ApplicationNumber, entity.ApplicationCategory, guarantorCount);

            // The completeness gate: the fix for the client's bug report (applications reaching
            // Approved with zero category-specific details and zero required documents).
            var completeness = await LoadCompletenessAsync(entity, cancellationToken);
            FundApplication.EnsureApplicationIsComplete(entity.ApplicationNumber, completeness);
        }

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

    private static ApplicationDto Map(FundApplication a, decimal totalCompletedPaid, Guid? activeLoanAgreementId, int guarantorCount) => new(
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
        DualEligibleCategoryOnGeneralFund: a.ApplicationCategory.FundEligibility == FundEligibility.Either && !a.FundCategory.IsZakat);
}
