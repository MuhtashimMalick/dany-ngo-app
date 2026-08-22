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
public class ApplicationDetailsService(AppDbContext dbContext) : IApplicationDetailsService
{
    private const string HousingCategoryCode = "HOUSE_RENT";
    private const string MarriageCategoryCode = "SHAADI";
    private const string BusinessLoanCategoryCode = "ROZGAR";

    public async Task<HousingApplicationDetailsDto?> GetHousingDetailsAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        await EnsureCategoryAsync(applicationId, HousingCategoryCode, cancellationToken);
        var entity = await dbContext.HousingApplicationDetails.AsNoTracking().SingleOrDefaultAsync(d => d.ApplicationId == applicationId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<HousingApplicationDetailsDto> UpsertHousingDetailsAsync(Guid applicationId, UpsertHousingApplicationDetailsRequest request, CancellationToken cancellationToken)
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

        ThrowIfFieldsMissing(application.ApplicationNumber, ApplicationCompletenessEvaluator.MissingHousingFields(entity));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<MarriageApplicationDetailsDto?> GetMarriageDetailsAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        await EnsureCategoryAsync(applicationId, MarriageCategoryCode, cancellationToken);
        var entity = await dbContext.MarriageApplicationDetails.AsNoTracking().SingleOrDefaultAsync(d => d.ApplicationId == applicationId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<MarriageApplicationDetailsDto> UpsertMarriageDetailsAsync(Guid applicationId, UpsertMarriageApplicationDetailsRequest request, CancellationToken cancellationToken)
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

        ThrowIfFieldsMissing(application.ApplicationNumber, ApplicationCompletenessEvaluator.MissingMarriageFields(entity));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<BusinessLoanApplicationDetailsDto?> GetBusinessLoanDetailsAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await EnsureCategoryAsync(applicationId, BusinessLoanCategoryCode, cancellationToken);
        var entity = await dbContext.BusinessLoanApplicationDetails.AsNoTracking().SingleOrDefaultAsync(d => d.ApplicationId == applicationId, cancellationToken);
        return entity is null ? null : Map(entity, application.RequestedAmount);
    }

    public async Task<BusinessLoanApplicationDetailsDto> UpsertBusinessLoanDetailsAsync(Guid applicationId, UpsertBusinessLoanApplicationDetailsRequest request, CancellationToken cancellationToken)
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

        ThrowIfFieldsMissing(application.ApplicationNumber, ApplicationCompletenessEvaluator.MissingBusinessLoanFields(entity));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity, application.RequestedAmount);
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

        return entities.Select(Map).ToList();
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

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

        return result.OrderBy(e => e.SequenceNumber).Select(Map).ToList();
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

    private static BusinessLoanApplicationDetailsDto Map(BusinessLoanApplicationDetails d, decimal requestedAmount) => new(
        d.ApplicationId, d.PaperFormNumber, d.BusinessPhone, d.Education, d.Skill, d.Experience, d.OtherIncomeSources,
        d.TotalMonthlyExpenses, d.ProposedBusinessDescription, d.ProposedBusinessLocation, d.CapitalRequired,
        d.CapitalAlreadyAvailable, d.HasPriorBusinessExperience, d.PriorBusinessDetails, d.EmergencyContactName,
        d.EmergencyContactCnic, d.EmergencyContactPhone,
        BusinessLoanCapitalCalculator.ComputeAmountMismatchWarning(requestedAmount, d.CapitalRequired, d.CapitalAlreadyAvailable));

    private static ApplicationGuarantorDto Map(ApplicationGuarantor g) => new(
        g.Id, g.ApplicationId, g.SequenceNumber, g.MembershipNumber, g.FullName, g.FatherName, g.GrandfatherName,
        g.Surname, g.Cnic, g.ResidentialAddress, g.BusinessAddress, g.BusinessNature, g.PhoneHome, g.PhoneOffice,
        g.PhoneMobile, g.DeclarationAcceptedAt);
}
