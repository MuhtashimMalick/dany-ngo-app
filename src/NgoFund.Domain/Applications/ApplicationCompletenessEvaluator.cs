using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Applications;

/// <summary>One document slot's status for a given application — reported for every slot in the
/// category's manifest (satisfied or not, required or optional), so the wizard and the manage-view
/// checklist render from a single evaluation. <see cref="GuarantorId"/>/<see cref="GuarantorSequenceNumber"/>/
/// <see cref="GuarantorName"/> are set only for <see cref="DocumentSlotOwnerScope.Guarantor"/>-scoped
/// slots, one entry per existing guarantor row. <see cref="SatisfiedByApplicantProfile"/> is true
/// when an <see cref="DocumentSlotOwnerScope.Applicant"/>-scoped slot was satisfied by a document on
/// the applicant's own profile (rather than the legacy application-owned fallback) — the frontend
/// uses it to render "already on file" instead of an upload prompt.</summary>
public sealed record DocumentSlotStatus(
    RequiredDocumentSlot Slot,
    Guid? GuarantorId,
    int? GuarantorSequenceNumber,
    string? GuarantorName,
    IReadOnlyList<Document> Documents,
    bool IsSatisfied,
    bool SatisfiedByApplicantProfile = false)
{
    /// <summary>The slot's label, disambiguated with the guarantor sequence number for guarantor-scoped slots (e.g. "Guarantor's CNIC (Guarantor 2)").</summary>
    public string DisplayLabel => GuarantorSequenceNumber is null ? Slot.Label : $"{Slot.Label} (Guarantor {GuarantorSequenceNumber})";
}

public sealed record ApplicationCompletenessResult(
    bool IsComplete,
    IReadOnlyList<RequiredField> MissingFields,
    IReadOnlyList<DocumentSlotStatus> Slots);

/// <summary>
/// Evaluates whether an application has everything <see cref="ApplicationRequirements"/> demands
/// for its category — fields, then documents/guarantor slots. The single evaluator shared by all
/// three enforcement layers (per-page wizard validation on the frontend, the per-upsert field
/// check in <c>ApplicationDetailsService</c>, and the full Approved-transition gate) so they can
/// never disagree about what "complete" means.
/// </summary>
public static class ApplicationCompletenessEvaluator
{
    public static IReadOnlyList<RequiredField> MissingHousingFields(HousingApplicationDetails? details)
    {
        var missing = new List<RequiredField>();

        if (details?.ApplicantAge is null) missing.Add(ApplicationRequirements.ApplicantAge);
        if (details?.MonthlyRent is null) missing.Add(ApplicationRequirements.MonthlyRent);
        if (details?.AdvancePaid is null) missing.Add(ApplicationRequirements.AdvancePaid);
        if (details?.YearsAtCurrentAddress is null) missing.Add(ApplicationRequirements.YearsAtCurrentAddress);

        if (details?.ReceivedAssistanceBefore == true && string.IsNullOrWhiteSpace(details.PreviousAssistanceDetails))
        {
            missing.Add(ApplicationRequirements.PreviousAssistanceDetails);
        }

        return missing;
    }

    public static IReadOnlyList<RequiredField> MissingMarriageFields(MarriageApplicationDetails? details)
    {
        var missing = new List<RequiredField>();

        if (string.IsNullOrWhiteSpace(details?.GuardianRelationshipToBride)) missing.Add(ApplicationRequirements.GuardianRelationshipToBride);
        if (string.IsNullOrWhiteSpace(details?.BrideName)) missing.Add(ApplicationRequirements.BrideName);
        if (string.IsNullOrWhiteSpace(details?.BrideFatherName)) missing.Add(ApplicationRequirements.BrideFatherName);
        if (string.IsNullOrWhiteSpace(details?.BrideCnic)) missing.Add(ApplicationRequirements.BrideCnic);
        if (details?.BrideMaritalStatus is null) missing.Add(ApplicationRequirements.BrideMaritalStatus);
        if (string.IsNullOrWhiteSpace(details?.BrideJamaat)) missing.Add(ApplicationRequirements.BrideJamaat);
        if (string.IsNullOrWhiteSpace(details?.GroomName)) missing.Add(ApplicationRequirements.GroomName);
        if (string.IsNullOrWhiteSpace(details?.GroomFatherName)) missing.Add(ApplicationRequirements.GroomFatherName);
        if (string.IsNullOrWhiteSpace(details?.GroomJamaat)) missing.Add(ApplicationRequirements.GroomJamaat);
        if (details?.GroomMaritalStatus is null) missing.Add(ApplicationRequirements.GroomMaritalStatus);
        // Item 5 (2026-09 feedback, groom-only): GroomAddress/GroomMobile are no longer blockers —
        // the client eased only the groom's side (see ApplicationRequirements.GroomCnic's doc
        // comment). The basic groom fields above (name/father's name/Jamaat/marital status) stay
        // required; RequiredField definitions themselves are left in place since they're still valid
        // labels for display when the fields happen to be filled in.
        if (details?.NikahDate is null) missing.Add(ApplicationRequirements.NikahDate);

        if (details?.BrideMaritalStatus is MaritalStatus.Divorced or MaritalStatus.Widowed
            && string.IsNullOrWhiteSpace(details.BridePreviousHusbandName))
        {
            missing.Add(ApplicationRequirements.BridePreviousHusbandName);
        }

        if (details?.GroomMaritalStatus is MaritalStatus.Divorced or MaritalStatus.Widowed
            && string.IsNullOrWhiteSpace(details.GroomPreviousWifeName))
        {
            missing.Add(ApplicationRequirements.GroomPreviousWifeName);
        }

        return missing;
    }

    public static IReadOnlyList<RequiredField> MissingBusinessLoanFields(BusinessLoanApplicationDetails? details)
    {
        var missing = new List<RequiredField>();

        if (string.IsNullOrWhiteSpace(details?.Skill)) missing.Add(ApplicationRequirements.Skill);
        if (string.IsNullOrWhiteSpace(details?.Experience)) missing.Add(ApplicationRequirements.Experience);
        if (details?.TotalMonthlyExpenses is null) missing.Add(ApplicationRequirements.TotalMonthlyExpenses);
        if (string.IsNullOrWhiteSpace(details?.ProposedBusinessDescription)) missing.Add(ApplicationRequirements.ProposedBusinessDescription);
        if (string.IsNullOrWhiteSpace(details?.ProposedBusinessLocation)) missing.Add(ApplicationRequirements.ProposedBusinessLocation);
        if (details?.CapitalRequired is null) missing.Add(ApplicationRequirements.CapitalRequired);
        if (details?.CapitalAlreadyAvailable is null) missing.Add(ApplicationRequirements.CapitalAlreadyAvailable);
        if (string.IsNullOrWhiteSpace(details?.EmergencyContactName)) missing.Add(ApplicationRequirements.EmergencyContactName);
        if (string.IsNullOrWhiteSpace(details?.EmergencyContactPhone)) missing.Add(ApplicationRequirements.EmergencyContactPhone);

        if (details?.HasPriorBusinessExperience == true && string.IsNullOrWhiteSpace(details.PriorBusinessDetails))
        {
            missing.Add(ApplicationRequirements.PriorBusinessDetails);
        }

        return missing;
    }

    /// <summary>Fields living on <see cref="FundApplication"/> itself rather than a category-specific details table: the declared-* fields (which categories need them varies) and the terms-signing block (required for every category once it has T&amp;C text — see <see cref="ApplicationCategory.TermsText"/>).</summary>
    public static IReadOnlyList<RequiredField> MissingBaseFields(FundApplication application, ApplicationCategory category)
    {
        var missing = new List<RequiredField>();

        switch (category.Code)
        {
            case "HOUSE_RENT":
                if (application.DeclaredMonthlyIncome is null) missing.Add(ApplicationRequirements.DeclaredMonthlyIncome);
                if (application.DeclaredHouseholdSize is null) missing.Add(ApplicationRequirements.DeclaredHouseholdSize);
                if (string.IsNullOrWhiteSpace(application.DeclaredResidentialAddress)) missing.Add(ApplicationRequirements.DeclaredResidentialAddress);
                if (application.DeclaredHouseStatus is null) missing.Add(ApplicationRequirements.DeclaredHouseStatus);
                break;
            case "ROZGAR":
                if (string.IsNullOrWhiteSpace(application.DeclaredBusinessAddress)) missing.Add(ApplicationRequirements.DeclaredBusinessAddress);
                break;
        }

        if (category.TermsText is not null)
        {
            if (application.DeclarationAcceptedAt is null) missing.Add(ApplicationRequirements.DeclarationAcceptedAt);
            if (application.TermsAcceptedAt is null) missing.Add(ApplicationRequirements.TermsAcceptedAt);
        }

        return missing;
    }

    /// <summary>Evaluates every document slot in the category's manifest against the documents actually on file. Guarantor-scoped slots are emitted once per existing <paramref name="guarantors"/> row (zero if there are none).</summary>
    public static IReadOnlyList<DocumentSlotStatus> EvaluateSlots(
        Guid applicationId, Guid applicantId, string categoryCode, IReadOnlyList<Document> documents, IReadOnlyList<ApplicationGuarantor> guarantors)
    {
        var statuses = new List<DocumentSlotStatus>();

        foreach (var slot in ApplicationRequirements.DocumentSlotsFor(categoryCode))
        {
            if (slot.OwnerScope == DocumentSlotOwnerScope.Applicant)
            {
                // v1.4 (B3): an Applicant-scoped slot is satisfied by EITHER (a) a document the
                // APPLICANT owns of an accepted type, matched on DocumentType alone and ignoring
                // SlotKey — applicant profile documents are uploaded with slotKey: null (they
                // aren't application-scoped), so this is a deliberate, narrow exception to
                // docs/schema.md's "slot_key IS NULL satisfies zero requirements" fail-closed rule.
                // It stays safe because it only applies to the Applicant arm, for slots explicitly
                // marked Applicant-scoped — every other slot still requires an exact SlotKey match.
                // OR (b) the legacy fallback: an APPLICATION-owned document under this exact slot
                // key, the pre-v1.4 rule. (b) is mandatory, not optional — applications created
                // before this change already have their CNIC/membership card uploaded as an
                // application-owned document under the old slot key, and without this fallback every
                // one of them would silently become incomplete and unable to reach Approved.
                var fromProfile = documents
                    .Where(d => d.ApplicantId == applicantId && slot.AcceptedTypes.Contains(d.DocumentType))
                    .ToList();
                var fromLegacyApplication = documents
                    .Where(d => d.ApplicationId == applicationId && d.SlotKey == slot.SlotKey && slot.AcceptedTypes.Contains(d.DocumentType))
                    .ToList();
                var matched = fromProfile.Concat(fromLegacyApplication).DistinctBy(d => d.Id).ToList();
                statuses.Add(new DocumentSlotStatus(slot, null, null, null, matched, matched.Count >= slot.MinCount, SatisfiedByApplicantProfile: fromProfile.Count > 0));
            }
            else if (slot.OwnerScope == DocumentSlotOwnerScope.Application)
            {
                var matched = documents
                    .Where(d => d.ApplicationId == applicationId && d.SlotKey == slot.SlotKey && slot.AcceptedTypes.Contains(d.DocumentType))
                    .ToList();
                statuses.Add(new DocumentSlotStatus(slot, null, null, null, matched, matched.Count >= slot.MinCount));
            }
            else
            {
                foreach (var guarantor in guarantors)
                {
                    var matched = documents
                        .Where(d => d.ApplicationGuarantorId == guarantor.Id && d.SlotKey == slot.SlotKey && slot.AcceptedTypes.Contains(d.DocumentType))
                        .ToList();
                    statuses.Add(new DocumentSlotStatus(slot, guarantor.Id, guarantor.SequenceNumber, guarantor.FullName, matched, matched.Count >= slot.MinCount));
                }
            }
        }

        return statuses;
    }

    /// <summary>The single dispatcher every enforcement layer calls: combines base fields, category-specific fields, and document/guarantor slots into one verdict.</summary>
    public static ApplicationCompletenessResult Evaluate(
        FundApplication application,
        ApplicationCategory category,
        HousingApplicationDetails? housingDetails,
        MarriageApplicationDetails? marriageDetails,
        BusinessLoanApplicationDetails? businessLoanDetails,
        IReadOnlyList<Document> documents,
        IReadOnlyList<ApplicationGuarantor> guarantors)
    {
        var missingFields = new List<RequiredField>(MissingBaseFields(application, category));
        missingFields.AddRange(category.Code switch
        {
            "HOUSE_RENT" => MissingHousingFields(housingDetails),
            "SHAADI" => MissingMarriageFields(marriageDetails),
            "ROZGAR" => MissingBusinessLoanFields(businessLoanDetails),
            _ => [],
        });

        var slots = EvaluateSlots(application.Id, application.ApplicantId, category.Code, documents, guarantors);
        var isComplete = missingFields.Count == 0 && slots.Where(s => s.Slot.IsRequired).All(s => s.IsSatisfied);

        return new ApplicationCompletenessResult(isComplete, missingFields, slots);
    }
}
