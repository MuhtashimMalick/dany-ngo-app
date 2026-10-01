using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Applications;
using NgoFund.Domain.Applications;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Exceptions;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

/// <summary>
/// Get/upsert for the three category-specific details tables plus get/replace-all for guarantors.
/// Each details table shares its primary key with <c>applications</c> (true 1:1), so "upsert" here
/// means "insert if this is the first save, otherwise update the existing row" rather than the
/// usual create-vs-update split the rest of the codebase uses for its own top-level entities.
/// </summary>
public class ApplicationDetailsService(AppDbContext dbContext, ICurrentUserService currentUser) : IApplicationDetailsService
{
    private const string HousingCategoryCode = "HOUSE_RENT";
    private const string MarriageCategoryCode = "SHAADI";
    private const string BusinessLoanCategoryCode = "ROZGAR";
    private const string EducationCategoryCode = "EDUCATION";
    private const string HealthCategoryCode = "HEALTH";

    public async Task<HousingApplicationDetailsDto?> GetHousingDetailsAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        await EnsureCategoryAsync(applicationId, HousingCategoryCode, cancellationToken);
        var entity = await dbContext.HousingApplicationDetails.AsNoTracking().SingleOrDefaultAsync(d => d.ApplicationId == applicationId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<HousingApplicationDetailsDto> UpsertHousingDetailsAsync(Guid applicationId, UpsertHousingApplicationDetailsRequest request, CancellationToken cancellationToken, bool enforceRequiredFields = true)
    {
        var application = await EnsureCategoryAsync(applicationId, HousingCategoryCode, cancellationToken);

        var entity = await dbContext.HousingApplicationDetails.SingleOrDefaultAsync(d => d.ApplicationId == applicationId, cancellationToken);
        if (entity is null)
        {
            entity = new HousingApplicationDetails { ApplicationId = applicationId };
            dbContext.HousingApplicationDetails.Add(entity);
        }

        entity.ApplicantAge = request.ApplicantAge;
        entity.CurrentHouseValue = request.CurrentHouseValue;
        entity.MonthlyRent = request.MonthlyRent;
        entity.AdvancePaid = request.AdvancePaid;
        entity.YearsAtCurrentAddress = request.YearsAtCurrentAddress;
        entity.PreviousResidentialAddress = request.PreviousResidentialAddress;
        entity.ReceivedAssistanceBefore = request.ReceivedAssistanceBefore;
        entity.PreviousAssistanceDetails = request.PreviousAssistanceDetails;
        entity.ReceivesMarriageAssistance = request.ReceivesMarriageAssistance;
        entity.ReceivesEducationAssistance = request.ReceivesEducationAssistance;
        entity.ReceivesMedicalAssistance = request.ReceivesMedicalAssistance;
        entity.ReceivesWidowAssistance = request.ReceivesWidowAssistance;

        if (enforceRequiredFields)
        {
            ThrowIfFieldsMissing(application.ApplicationNumber, ApplicationCompletenessEvaluator.MissingHousingFields(entity));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<MarriageApplicationDetailsDto?> GetMarriageDetailsAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        await EnsureCategoryAsync(applicationId, MarriageCategoryCode, cancellationToken);
        var entity = await dbContext.MarriageApplicationDetails.AsNoTracking().SingleOrDefaultAsync(d => d.ApplicationId == applicationId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<MarriageApplicationDetailsDto> UpsertMarriageDetailsAsync(Guid applicationId, UpsertMarriageApplicationDetailsRequest request, CancellationToken cancellationToken, bool enforceRequiredFields = true)
    {
        var application = await EnsureCategoryAsync(applicationId, MarriageCategoryCode, cancellationToken);

        var entity = await dbContext.MarriageApplicationDetails.SingleOrDefaultAsync(d => d.ApplicationId == applicationId, cancellationToken);
        if (entity is null)
        {
            entity = new MarriageApplicationDetails { ApplicationId = applicationId };
            dbContext.MarriageApplicationDetails.Add(entity);
        }

        entity.GuardianRelationshipToBride = request.GuardianRelationshipToBride;
        entity.BrideName = request.BrideName;
        entity.BrideFatherName = request.BrideFatherName;
        entity.BrideFamilyName = request.BrideFamilyName;
        entity.BrideCnic = request.BrideCnic;
        entity.BrideMaritalStatus = MaritalStatusMapper.MapFormLabel(request.BrideMaritalStatus);
        entity.BridePreviousHusbandName = request.BridePreviousHusbandName;
        entity.BrideJamaat = request.BrideJamaat;
        entity.BridePriorTrustAssistance = request.BridePriorTrustAssistance;
        entity.GroomName = request.GroomName;
        entity.GroomFatherName = request.GroomFatherName;
        entity.GroomGrandfatherName = request.GroomGrandfatherName;
        entity.GroomJamaat = request.GroomJamaat;
        entity.GroomMaritalStatus = MaritalStatusMapper.MapFormLabel(request.GroomMaritalStatus);
        entity.GroomPreviousWifeName = request.GroomPreviousWifeName;
        entity.GroomAddress = request.GroomAddress;
        entity.GroomMobile = request.GroomMobile;
        entity.GroomBusinessAddress = request.GroomBusinessAddress;
        entity.NikahDate = request.NikahDate;
        entity.RukhsatiDate = request.RukhsatiDate;

        if (enforceRequiredFields)
        {
            ThrowIfFieldsMissing(application.ApplicationNumber, ApplicationCompletenessEvaluator.MissingMarriageFields(entity));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<BusinessLoanApplicationDetailsDto?> GetBusinessLoanDetailsAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        await EnsureCategoryAsync(applicationId, BusinessLoanCategoryCode, cancellationToken);
        var entity = await dbContext.BusinessLoanApplicationDetails.AsNoTracking().SingleOrDefaultAsync(d => d.ApplicationId == applicationId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<BusinessLoanApplicationDetailsDto> UpsertBusinessLoanDetailsAsync(Guid applicationId, UpsertBusinessLoanApplicationDetailsRequest request, CancellationToken cancellationToken, bool enforceRequiredFields = true)
    {
        var application = await EnsureCategoryAsync(applicationId, BusinessLoanCategoryCode, cancellationToken);

        var entity = await dbContext.BusinessLoanApplicationDetails.SingleOrDefaultAsync(d => d.ApplicationId == applicationId, cancellationToken);
        if (entity is null)
        {
            entity = new BusinessLoanApplicationDetails { ApplicationId = applicationId };
            dbContext.BusinessLoanApplicationDetails.Add(entity);
        }

        entity.PaperFormNumber = request.PaperFormNumber;
        entity.BusinessPhone = request.BusinessPhone;
        entity.Education = request.Education;
        entity.Skill = request.Skill;
        entity.Experience = request.Experience;
        entity.OtherIncomeSources = request.OtherIncomeSources;
        entity.TotalMonthlyExpenses = request.TotalMonthlyExpenses;
        entity.ProposedBusinessDescription = request.ProposedBusinessDescription;
        entity.ProposedBusinessLocation = request.ProposedBusinessLocation;
        entity.CapitalRequired = request.CapitalRequired;
        entity.CapitalAlreadyAvailable = request.CapitalAlreadyAvailable;
        entity.HasPriorBusinessExperience = request.HasPriorBusinessExperience;
        entity.PriorBusinessDetails = request.PriorBusinessDetails;
        entity.EmergencyContactName = request.EmergencyContactName;
        entity.EmergencyContactCnic = request.EmergencyContactCnic;
        entity.EmergencyContactPhone = request.EmergencyContactPhone;

        if (enforceRequiredFields)
        {
            ThrowIfFieldsMissing(application.ApplicationNumber, ApplicationCompletenessEvaluator.MissingBusinessLoanFields(entity));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<EducationApplicationDetailsDto?> GetEducationDetailsAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        await EnsureCategoryAsync(applicationId, EducationCategoryCode, cancellationToken);
        var entity = await dbContext.EducationApplicationDetails.AsNoTracking().SingleOrDefaultAsync(d => d.ApplicationId == applicationId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<EducationApplicationDetailsDto> UpsertEducationDetailsAsync(Guid applicationId, UpsertEducationApplicationDetailsRequest request, CancellationToken cancellationToken, bool enforceRequiredFields = true)
    {
        var application = await EnsureCategoryAsync(applicationId, EducationCategoryCode, cancellationToken);

        var entity = await dbContext.EducationApplicationDetails.SingleOrDefaultAsync(d => d.ApplicationId == applicationId, cancellationToken);
        if (entity is null)
        {
            entity = new EducationApplicationDetails { ApplicationId = applicationId };
            dbContext.EducationApplicationDetails.Add(entity);
        }

        entity.CensusNumber = request.CensusNumber;
        entity.StudentName = request.StudentName;
        entity.WmoId = request.WmoId;
        entity.StudentMobile = request.StudentMobile;
        entity.CurrentClass = request.CurrentClass;
        entity.PreviousClass = request.PreviousClass;
        entity.LastExamTotalMarks = request.LastExamTotalMarks;
        entity.LastExamMarksObtained = request.LastExamMarksObtained;
        entity.PreviousYearAttendancePercent = request.PreviousYearAttendancePercent;
        entity.TotalAttendanceDays = request.TotalAttendanceDays;
        entity.TotalAcademicDays = request.TotalAcademicDays;
        entity.FatherJamaat = request.FatherJamaat;
        entity.MotherName = request.MotherName;
        entity.MotherFatherName = request.MotherFatherName;
        entity.MotherCaste = request.MotherCaste;
        entity.MotherJamaat = request.MotherJamaat;
        entity.MotherMembershipNumber = request.MotherMembershipNumber;
        entity.MotherCnic = request.MotherCnic;
        entity.MotherMonthlyIncome = request.MotherMonthlyIncome;
        entity.MotherMobile = request.MotherMobile;
        entity.MotherProfession = request.MotherProfession;

        if (enforceRequiredFields)
        {
            ThrowIfFieldsMissing(application.ApplicationNumber, ApplicationCompletenessEvaluator.MissingEducationFields(entity));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<HealthApplicationDetailsDto?> GetHealthDetailsAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        await EnsureCategoryAsync(applicationId, HealthCategoryCode, cancellationToken);
        var entity = await dbContext.HealthApplicationDetails.AsNoTracking().SingleOrDefaultAsync(d => d.ApplicationId == applicationId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<HealthApplicationDetailsDto> UpsertHealthDetailsAsync(Guid applicationId, UpsertHealthApplicationDetailsRequest request, CancellationToken cancellationToken, bool enforceRequiredFields = true)
    {
        var application = await EnsureCategoryAsync(applicationId, HealthCategoryCode, cancellationToken);

        var entity = await dbContext.HealthApplicationDetails.SingleOrDefaultAsync(d => d.ApplicationId == applicationId, cancellationToken);
        if (entity is null)
        {
            entity = new HealthApplicationDetails { ApplicationId = applicationId };
            dbContext.HealthApplicationDetails.Add(entity);
        }

        entity.ApplicantAge = request.ApplicantAge;

        if (enforceRequiredFields)
        {
            ThrowIfFieldsMissing(application.ApplicationNumber, ApplicationCompletenessEvaluator.MissingHealthFields(entity));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<IReadOnlyList<ApplicationGuarantorDto>> GetGuarantorsAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Applications.AnyAsync(a => a.Id == applicationId, cancellationToken))
        {
            throw new EntityNotFoundException("Application", applicationId);
        }

        var entities = await dbContext.ApplicationGuarantors
            .AsNoTracking()
            .Where(g => g.ApplicationId == applicationId)
            .OrderBy(g => g.SequenceNumber)
            .ToListAsync(cancellationToken);

        return await MapWithConflictsAsync(applicationId, entities, cancellationToken);
    }

    /// <summary>
    /// Upserts the guarantor list in place: an incoming entry whose <see cref="GuarantorEntry.Id"/>
    /// matches an existing row updates that row's fields (its Id — and therefore
    /// <c>documents.application_guarantor_id</c> — never changes); an entry with no Id, or an Id
    /// that doesn't match any existing row, inserts a new one; an existing row missing from the
    /// incoming list is soft-deleted. Replaced the old remove-all-then-recreate approach because
    /// documents hang off individual guarantor rows via FK: recreating a row for "the same
    /// guarantor, edited" orphaned its uploaded documents even though nothing was actually removed.
    /// </summary>
    public async Task<IReadOnlyList<ApplicationGuarantorDto>> ReplaceGuarantorsAsync(Guid applicationId, ReplaceApplicationGuarantorsRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Applications.AnyAsync(a => a.Id == applicationId, cancellationToken))
        {
            throw new EntityNotFoundException("Application", applicationId);
        }

        var existing = await dbContext.ApplicationGuarantors.Where(g => g.ApplicationId == applicationId).ToListAsync(cancellationToken);
        var existingById = existing.ToDictionary(g => g.Id);
        var matchedIds = request.Guarantors
            .Where(g => g.Id is not null && existingById.ContainsKey(g.Id.Value))
            .Select(g => g.Id!.Value)
            .ToHashSet();

        // C3: BeginTransactionIfNoneAsync, not a plain BeginTransactionAsync — see
        // DatabaseFacadeExtensions. Lets the Google Form intake pipeline call this nested inside
        // its own outer transaction without throwing.
        await using var transaction = await dbContext.Database.BeginTransactionIfNoneAsync(cancellationToken);

        // Soft-delete dropped guarantors first and flush, so their sequence_no slot is free (the
        // unique index on (application_id, sequence_no) is partial, WHERE is_deleted = false)
        // before a surviving row can be updated into it — e.g. removing guarantor 1 while
        // guarantor 2 stays shifts guarantor 2 from seq 2 down to seq 1 in the same save.
        var removed = existing.Where(g => !matchedIds.Contains(g.Id)).ToList();
        if (removed.Count > 0)
        {
            dbContext.ApplicationGuarantors.RemoveRange(removed);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // ponytail: doesn't handle two still-live existing guarantors swapping sequence numbers in
        // one save (e.g. 1<->2) — unreachable from the Desktop editor today (adds always append,
        // removals renumber to a contiguous prefix, edits never reorder existing rows). If a future
        // caller needs that, shift matched rows to temporary out-of-range sequence numbers in an
        // interim SaveChanges before assigning final values.
        var result = new List<ApplicationGuarantor>();
        foreach (var g in request.Guarantors)
        {
            ApplicationGuarantor entity;
            if (g.Id is not null && existingById.TryGetValue(g.Id.Value, out var matched))
            {
                entity = matched;
            }
            else
            {
                entity = new ApplicationGuarantor { ApplicationId = applicationId };
                dbContext.ApplicationGuarantors.Add(entity);
            }

            // Item 4 (2026-09 feedback): a conflict override is approved for a specific person's
            // CNIC. If staff edit the CNIC on an existing row to a different value, that override
            // must not silently carry over to whoever the new CNIC belongs to.
            if (entity.Cnic != g.Cnic)
            {
                entity.ConflictOverrideApprovedAt = null;
                entity.ConflictOverrideApprovedBy = null;
                entity.ConflictOverrideReason = null;
            }

            entity.SequenceNumber = g.SequenceNumber;
            entity.MembershipNumber = g.MembershipNumber;
            entity.FullName = g.FullName;
            entity.FatherName = g.FatherName;
            entity.GrandfatherName = g.GrandfatherName;
            entity.Surname = g.Surname;
            entity.Cnic = g.Cnic;
            entity.ResidentialAddress = g.ResidentialAddress;
            entity.BusinessAddress = g.BusinessAddress;
            entity.BusinessNature = g.BusinessNature;
            entity.PhoneHome = g.PhoneHome;
            entity.PhoneOffice = g.PhoneOffice;
            entity.PhoneMobile = g.PhoneMobile;
            entity.DeclarationAcceptedAt = g.DeclarationAcceptedAt;

            result.Add(entity);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await MapWithConflictsAsync(applicationId, result.OrderBy(e => e.SequenceNumber).ToList(), cancellationToken);
    }

    public async Task<ApplicationGuarantorDto> ApproveGuarantorConflictOverrideAsync(
        Guid applicationId, Guid guarantorId, ApproveGuarantorConflictOverrideRequest request, CancellationToken cancellationToken)
    {
        var guarantor = await dbContext.ApplicationGuarantors
            .SingleOrDefaultAsync(g => g.Id == guarantorId && g.ApplicationId == applicationId, cancellationToken)
            ?? throw new EntityNotFoundException("ApplicationGuarantor", guarantorId);

        guarantor.ConflictOverrideApprovedAt = DateTimeOffset.UtcNow;
        guarantor.ConflictOverrideApprovedBy = currentUser.UserId;
        guarantor.ConflictOverrideReason = request.Reason;

        await dbContext.SaveChangesAsync(cancellationToken);

        var mapped = await MapWithConflictsAsync(applicationId, [guarantor], cancellationToken);
        return mapped.Single();
    }

    /// <summary>Batches the conflict lookup and the override-approver name lookup across the whole
    /// list instead of one query per guarantor — same pattern as
    /// <c>FundApplicationService.GetApplicationsAsync</c>'s paymentTotals/guarantorCounts dictionaries.</summary>
    private async Task<IReadOnlyList<ApplicationGuarantorDto>> MapWithConflictsAsync(
        Guid applicationId, IReadOnlyList<ApplicationGuarantor> guarantors, CancellationToken cancellationToken)
    {
        var conflicts = await GuarantorConflictLookup.FindConflictsAsync(dbContext, applicationId, guarantors, cancellationToken);

        var approverIds = guarantors
            .Where(g => g.ConflictOverrideApprovedBy is not null)
            .Select(g => g.ConflictOverrideApprovedBy!.Value)
            .Distinct()
            .ToList();
        var approverNames = approverIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Users.Where(u => approverIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return guarantors.Select(g => Map(
            g,
            conflicts.GetValueOrDefault(g.Id, []),
            g.ConflictOverrideApprovedBy is not null ? approverNames.GetValueOrDefault(g.ConflictOverrideApprovedBy.Value) : null)).ToList();
    }

    /// <summary>Enforcement layer 2 of the completeness gate (see <c>docs/schema.md</c>): each
    /// Upsert*DetailsAsync above checks its own category-specific required fields before saving,
    /// using the exact same <see cref="ApplicationCompletenessEvaluator"/> functions the full
    /// Approved-transition gate (layer 3, in <c>FundApplicationService</c>) uses — so the two can
    /// never disagree. Deliberately does not also re-check the base (FundApplication-level) fields
    /// here: those aren't touched by this save, and get their own check at the Approved gate.</summary>
    private static void ThrowIfFieldsMissing(string applicationNumber, IReadOnlyList<RequiredField> missingFields)
    {
        if (missingFields.Count > 0)
        {
            throw new ApplicationIncompleteException(applicationNumber, missingFields.Select(f => f.Label).ToList(), []);
        }
    }

    /// <summary>Loads the application with its category and throws if it doesn't match <paramref name="expectedCategoryCode"/> — the guard every details endpoint needs (E7).</summary>
    private async Task<FundApplication> EnsureCategoryAsync(Guid applicationId, string expectedCategoryCode, CancellationToken cancellationToken)
    {
        var application = await dbContext.Applications
            .Include(a => a.ApplicationCategory)
            .SingleOrDefaultAsync(a => a.Id == applicationId, cancellationToken)
            ?? throw new EntityNotFoundException("Application", applicationId);

        if (application.ApplicationCategory.Code != expectedCategoryCode)
        {
            throw new ApplicationCategoryMismatchException(application.ApplicationNumber, expectedCategoryCode, application.ApplicationCategory.Code);
        }

        return application;
    }

    private static HousingApplicationDetailsDto Map(HousingApplicationDetails d) => new(
        d.ApplicationId, d.ApplicantAge, d.CurrentHouseValue, d.MonthlyRent, d.AdvancePaid, d.YearsAtCurrentAddress,
        d.PreviousResidentialAddress, d.ReceivedAssistanceBefore, d.PreviousAssistanceDetails,
        d.ReceivesMarriageAssistance, d.ReceivesEducationAssistance, d.ReceivesMedicalAssistance, d.ReceivesWidowAssistance);

    private static MarriageApplicationDetailsDto Map(MarriageApplicationDetails d) => new(
        d.ApplicationId, d.GuardianRelationshipToBride, d.BrideName, d.BrideFatherName, d.BrideFamilyName, d.BrideCnic,
        d.BrideMaritalStatus?.ToString(), d.BridePreviousHusbandName, d.BrideJamaat, d.BridePriorTrustAssistance,
        d.GroomName, d.GroomFatherName, d.GroomGrandfatherName, d.GroomJamaat, d.GroomMaritalStatus?.ToString(),
        d.GroomPreviousWifeName, d.GroomAddress, d.GroomMobile, d.GroomBusinessAddress, d.NikahDate, d.RukhsatiDate);

    private static EducationApplicationDetailsDto Map(EducationApplicationDetails d) => new(
        d.ApplicationId, d.CensusNumber, d.StudentName, d.WmoId, d.StudentMobile, d.CurrentClass, d.PreviousClass,
        d.LastExamTotalMarks, d.LastExamMarksObtained, d.PreviousYearAttendancePercent, d.TotalAttendanceDays,
        d.TotalAcademicDays, d.FatherJamaat, d.MotherName, d.MotherFatherName, d.MotherCaste, d.MotherJamaat,
        d.MotherMembershipNumber, d.MotherCnic, d.MotherMonthlyIncome, d.MotherMobile, d.MotherProfession);

    private static HealthApplicationDetailsDto Map(HealthApplicationDetails d) => new(d.ApplicationId, d.ApplicantAge);

    private static BusinessLoanApplicationDetailsDto Map(BusinessLoanApplicationDetails d) => new(
        d.ApplicationId, d.PaperFormNumber, d.BusinessPhone, d.Education, d.Skill, d.Experience, d.OtherIncomeSources,
        d.TotalMonthlyExpenses, d.ProposedBusinessDescription, d.ProposedBusinessLocation, d.CapitalRequired,
        d.CapitalAlreadyAvailable, d.HasPriorBusinessExperience, d.PriorBusinessDetails, d.EmergencyContactName,
        d.EmergencyContactCnic, d.EmergencyContactPhone);

    private static ApplicationGuarantorDto Map(
        ApplicationGuarantor g, IReadOnlyList<string> conflictingApplicationNumbers, string? conflictOverrideApprovedByName) => new(
        g.Id, g.ApplicationId, g.SequenceNumber, g.MembershipNumber, g.FullName, g.FatherName, g.GrandfatherName,
        g.Surname, g.Cnic, g.ResidentialAddress, g.BusinessAddress, g.BusinessNature, g.PhoneHome, g.PhoneOffice,
        g.PhoneMobile, g.DeclarationAcceptedAt,
        conflictingApplicationNumbers, g.ConflictOverrideApprovedAt, conflictOverrideApprovedByName, g.ConflictOverrideReason);
}
