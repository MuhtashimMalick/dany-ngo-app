using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Applications;

/// <summary>One required (or optional) document slot for a category — e.g. "Bride's CNIC / B-Form"
/// on SHAADI. <see cref="SlotKey"/> is a persisted string (stored on <c>documents.slot_key</c>) —
/// renaming one orphans existing uploads, so treat it like a database column name.</summary>
public sealed record RequiredDocumentSlot(
    string SlotKey,
    string Label,
    bool IsRequired,
    int MinCount,
    DocumentSlotOwnerScope OwnerScope,
    IReadOnlyList<DocumentType> AcceptedTypes);

/// <summary>One required field on the application or its category-specific details table, for
/// display in the missing-fields list and the wizard/checklist UI.</summary>
public sealed record RequiredField(string Key, string Label, string SectionLabel);

/// <summary>
/// The client-approved completeness manifest: which documents and fields a
/// <c>FundApplication</c> needs before it can be moved to <see cref="Enums.ApplicationStatus.Approved"/>,
/// per <see cref="Entities.ApplicationCategory.Code"/>.
///
/// Deliberately code, not a database table: it must be editable without a migration, Desktop can't
/// reference Domain directly anyway so it has to cross the API as a DTO regardless of where it's
/// stored, and this codebase already keeps workflow rules in code (see
/// <c>FundApplication.AllowedTransitions</c>, <c>FundApplication.EnsureFundIsCompatible</c>) rather
/// than in tables. If this ever needs to become admin-editable, the migration path is: seed this
/// same data into a table plus an admin screen, and change <see cref="DocumentSlotsFor"/>/
/// <see cref="RequiredFieldsFor"/> to read from it instead — the call sites (the completeness
/// evaluator, the endpoint) don't need to change.
/// </summary>
public static class ApplicationRequirements
{
    private const string HouseRent = "HOUSE_RENT";
    private const string Shaadi = "SHAADI";
    private const string Rozgar = "ROZGAR";
    private const string Education = "EDUCATION";
    private const string Health = "HEALTH";
    private const string Other = "OTHER";

    // v1.4: APPLICANT_CNIC/MEMBERSHIP_CARD slots below are Applicant-scoped, not
    // Application-scoped — the applicant's own CNIC and Jamaat membership card, already mandatory
    // on the applicant record itself (see ApplicantService.CreateAsync), satisfy these slots so
    // staff aren't asked to upload the same two documents again on every application. See
    // ApplicationCompletenessEvaluator.EvaluateSlots for how an Applicant-scoped slot is matched.
    private static readonly IReadOnlyList<RequiredDocumentSlot> HouseRentSlots =
    [
        new("HOUSE_RENT.APPLICANT_CNIC", "Applicant's CNIC", true, 1, DocumentSlotOwnerScope.Applicant, [DocumentType.CnicFront, DocumentType.CnicBack]),
        new("HOUSE_RENT.MEMBERSHIP_CARD", "Applicant's Jamaat Membership Card", true, 1, DocumentSlotOwnerScope.Applicant, [DocumentType.MembershipCard]),
        // Item 6 (2026-09 feedback): utility bills are now fully optional — an applicant "may
        // provide None". MinCount is 0, not left at the old 3, so matched.Count >= slot.MinCount is
        // always true: the slot never reads as an incomplete/partial-upload state in the checklist.
        new("HOUSE_RENT.UTILITY_BILLS", "3 Months' Utility Bills", false, 0, DocumentSlotOwnerScope.Application, [DocumentType.UtilityBill]),
        new("HOUSE_RENT.RENT_RECEIPTS", "Rent Receipts", false, 1, DocumentSlotOwnerScope.Application, [DocumentType.RentReceipt]),
    ];

    private static readonly IReadOnlyList<RequiredDocumentSlot> ShaadiSlots =
    [
        new("SHAADI.APPLICANT_CNIC", "Applicant's CNIC", true, 1, DocumentSlotOwnerScope.Applicant, [DocumentType.CnicFront, DocumentType.CnicBack]),
        new("SHAADI.APPLICANT_MEMBERSHIP_CARD", "Applicant's Jamaat Membership Card", true, 1, DocumentSlotOwnerScope.Applicant, [DocumentType.MembershipCard]),
        new("SHAADI.WEDDING_CARD", "Wedding Card", false, 1, DocumentSlotOwnerScope.Application, [DocumentType.WeddingCard]),
        // Deliberately still Application-scoped, even though they share DocumentTypes with the
        // applicant-scoped slots above: these are the BRIDE's and GROOM's own documents, not the
        // applicant's. Widening them to Applicant scope would let the applicant's own CNIC/
        // membership card silently satisfy a requirement for someone else's document — an
        // incomplete application could then reach Approved. See
        // ApplicationCompletenessEvaluatorTests for the regression guard.
        new("SHAADI.BRIDE_CNIC_OR_BFORM", "Bride's CNIC / B-Form", true, 1, DocumentSlotOwnerScope.Application, [DocumentType.CnicFront, DocumentType.FormB]),
        // Item 5 (2026-09 feedback, groom-only): the client explicitly kept every bride requirement
        // as-is (NGO provides help to the bride's side of the family, so bride documents stay
        // mandatory) and only eased the groom's side. Groom's CNIC is now optional; his name/
        // father's name/Jamaat/marital status stay required (see MissingMarriageFields below).
        new("SHAADI.GROOM_CNIC", "Groom's CNIC", false, 1, DocumentSlotOwnerScope.Application, [DocumentType.CnicFront]),
        new("SHAADI.BRIDE_MEMBERSHIP_CARD", "Bride's Jamaat Membership Card", true, 1, DocumentSlotOwnerScope.Application, [DocumentType.MembershipCard]),
    ];

    private static readonly IReadOnlyList<RequiredDocumentSlot> RozgarSlots =
    [
        new("ROZGAR.LOAN_APPLICATION", "Loan Application", true, 1, DocumentSlotOwnerScope.Application, [DocumentType.SignedApplicationForm]),
        new("ROZGAR.BUSINESS_DETAILS", "Nature and Details of the Business", true, 1, DocumentSlotOwnerScope.Application, [DocumentType.BusinessPlan]),
        new("ROZGAR.APPLICANT_CNIC", "Photocopy of CNIC", true, 1, DocumentSlotOwnerScope.Applicant, [DocumentType.CnicFront, DocumentType.CnicBack]),
        // Deliberately still Application-scoped — the B-Form is not one of the applicant's
        // mandatory profile documents, so there is nothing on the applicant record to satisfy it.
        new("ROZGAR.FORM_B", "Photocopy of B-Form", true, 1, DocumentSlotOwnerScope.Application, [DocumentType.FormB]),
        new("ROZGAR.MEMBERSHIP_CARD", "Copy of Jamaat Membership Card", true, 1, DocumentSlotOwnerScope.Applicant, [DocumentType.MembershipCard]),
        new("ROZGAR.PASSPORT_PHOTOS", "Two Recent Passport-Size Colour Photographs", true, 2, DocumentSlotOwnerScope.Application, [DocumentType.PassportPhoto]),
        // Item 6 (2026-09 feedback): optional, same reasoning as HOUSE_RENT.UTILITY_BILLS above.
        new("ROZGAR.UTILITY_BILLS", "Photocopies of Household Utility Bills", false, 0, DocumentSlotOwnerScope.Application, [DocumentType.UtilityBill]),
        // Guarantor slots stay Guarantor-scoped on purpose — they are the GUARANTOR's own CNIC/
        // membership card, a different person from the applicant.
        new("ROZGAR.GUARANTOR_CNIC", "Guarantor's CNIC", true, 1, DocumentSlotOwnerScope.Guarantor, [DocumentType.CnicFront, DocumentType.CnicBack]),
        new("ROZGAR.GUARANTOR_MEMBERSHIP_CARD", "Guarantor's Jamaat Membership Card", true, 1, DocumentSlotOwnerScope.Guarantor, [DocumentType.MembershipCard]),
    ];

    // A9: EDUCATION/HEALTH/OTHER manifests added by the Google Form intake integration. Applicant-
    // scoped CNIC/membership-card slots follow the same v1.4 pattern as the three original
    // categories — see the comment above HouseRentSlots.
    private static readonly IReadOnlyList<RequiredDocumentSlot> EducationSlots =
    [
        new("EDUCATION.MEMBERSHIP_CARD", "Applicant's Jamaat Membership Card", true, 1, DocumentSlotOwnerScope.Applicant, [DocumentType.MembershipCard]),
        new("EDUCATION.STUDENT_BFORM_OR_CNIC", "Student's B-Form / CNIC", true, 1, DocumentSlotOwnerScope.Application, [DocumentType.FormB, DocumentType.CnicFront]),
        new("EDUCATION.STUDENT_PHOTO_AND_RESULT", "Student's Photo & Last Academic Result", true, 1, DocumentSlotOwnerScope.Application,
            [DocumentType.PassportPhoto, DocumentType.AcademicResult, DocumentType.SupportingDocument]),
    ];

    private static readonly IReadOnlyList<RequiredDocumentSlot> HealthSlots =
    [
        new("HEALTH.APPLICANT_CNIC", "Applicant's CNIC", true, 1, DocumentSlotOwnerScope.Applicant, [DocumentType.CnicFront, DocumentType.CnicBack]),
        new("HEALTH.MEMBERSHIP_CARD", "Applicant's Jamaat Membership Card", true, 1, DocumentSlotOwnerScope.Applicant, [DocumentType.MembershipCard]),
        new("HEALTH.MEDICAL_DOCUMENTS", "Supporting Medical Documents", true, 1, DocumentSlotOwnerScope.Application, [DocumentType.MedicalReport, DocumentType.SupportingDocument]),
    ];

    private static readonly IReadOnlyList<RequiredDocumentSlot> OtherSlots =
    [
        new("OTHER.APPLICANT_CNIC", "Applicant's CNIC", true, 1, DocumentSlotOwnerScope.Applicant, [DocumentType.CnicFront, DocumentType.CnicBack]),
        new("OTHER.MEMBERSHIP_CARD", "Applicant's Jamaat Membership Card", true, 1, DocumentSlotOwnerScope.Applicant, [DocumentType.MembershipCard]),
        new("OTHER.SUPPORTING_DOCUMENTS", "Supporting Documents", true, 1, DocumentSlotOwnerScope.Application, [DocumentType.SupportingDocument]),
    ];

    private static readonly Dictionary<string, IReadOnlyList<RequiredDocumentSlot>> SlotsByCategory = new()
    {
        [HouseRent] = HouseRentSlots,
        [Shaadi] = ShaadiSlots,
        [Rozgar] = RozgarSlots,
        [Education] = EducationSlots,
        [Health] = HealthSlots,
        [Other] = OtherSlots,
    };

    /// <summary>Terms-signing fields required, when a category has T&amp;C text, regardless of category — see <see cref="ApplicationCompletenessEvaluator.MissingBaseFields"/>.</summary>
    public static readonly RequiredField DeclarationAcceptedAt = new(nameof(DeclarationAcceptedAt), "Declaration Accepted", "Terms & Declaration");
    public static readonly RequiredField TermsAcceptedAt = new(nameof(TermsAcceptedAt), "Terms Accepted", "Terms & Declaration");

    public static readonly RequiredField DeclaredMonthlyIncome = new(nameof(DeclaredMonthlyIncome), "Declared Monthly Income", "Household");
    public static readonly RequiredField DeclaredHouseholdSize = new(nameof(DeclaredHouseholdSize), "Declared Household Size", "Household");
    public static readonly RequiredField DeclaredResidentialAddress = new(nameof(DeclaredResidentialAddress), "Declared Residential Address", "Household");
    public static readonly RequiredField DeclaredHouseStatus = new(nameof(DeclaredHouseStatus), "Declared House Status", "Household");
    public static readonly RequiredField DeclaredBusinessAddress = new(nameof(DeclaredBusinessAddress), "Declared Business Address", "Household");

    public static readonly RequiredField ApplicantAge = new(nameof(ApplicantAge), "Applicant's Age", "Housing Details");
    public static readonly RequiredField MonthlyRent = new(nameof(MonthlyRent), "Monthly Rent", "Housing Details");
    public static readonly RequiredField AdvancePaid = new(nameof(AdvancePaid), "Advance Paid", "Housing Details");
    public static readonly RequiredField YearsAtCurrentAddress = new(nameof(YearsAtCurrentAddress), "Years at Current Address", "Housing Details");
    public static readonly RequiredField PreviousAssistanceDetails = new(nameof(PreviousAssistanceDetails), "Previous Assistance Details", "Housing Details");

    public static readonly RequiredField GuardianRelationshipToBride = new(nameof(GuardianRelationshipToBride), "Guardian's Relationship to Bride", "Marriage Details");
    public static readonly RequiredField BrideName = new(nameof(BrideName), "Bride's Name", "Marriage Details");
    public static readonly RequiredField BrideFatherName = new(nameof(BrideFatherName), "Bride's Father Name", "Marriage Details");
    public static readonly RequiredField BrideCnic = new(nameof(BrideCnic), "Bride's CNIC", "Marriage Details");
    public static readonly RequiredField BrideMaritalStatus = new(nameof(BrideMaritalStatus), "Bride's Marital Status", "Marriage Details");
    public static readonly RequiredField BrideJamaat = new(nameof(BrideJamaat), "Bride's Jamaat", "Marriage Details");
    public static readonly RequiredField BridePreviousHusbandName = new(nameof(BridePreviousHusbandName), "Bride's Previous Husband Name", "Marriage Details");
    public static readonly RequiredField GroomName = new(nameof(GroomName), "Groom's Name", "Marriage Details");
    public static readonly RequiredField GroomFatherName = new(nameof(GroomFatherName), "Groom's Father Name", "Marriage Details");
    public static readonly RequiredField GroomJamaat = new(nameof(GroomJamaat), "Groom's Jamaat", "Marriage Details");
    public static readonly RequiredField GroomMaritalStatus = new(nameof(GroomMaritalStatus), "Groom's Marital Status", "Marriage Details");
    public static readonly RequiredField GroomAddress = new(nameof(GroomAddress), "Groom's Address", "Marriage Details");
    public static readonly RequiredField GroomMobile = new(nameof(GroomMobile), "Groom's Mobile", "Marriage Details");
    public static readonly RequiredField GroomPreviousWifeName = new(nameof(GroomPreviousWifeName), "Groom's Previous Wife Name", "Marriage Details");
    public static readonly RequiredField NikahDate = new(nameof(NikahDate), "Nikah Date", "Marriage Details");

    public static readonly RequiredField Skill = new(nameof(Skill), "Skill", "Business Details");
    public static readonly RequiredField Experience = new(nameof(Experience), "Experience", "Business Details");
    public static readonly RequiredField TotalMonthlyExpenses = new(nameof(TotalMonthlyExpenses), "Total Monthly Expenses", "Business Details");
    public static readonly RequiredField ProposedBusinessDescription = new(nameof(ProposedBusinessDescription), "Proposed Business Description", "Business Details");
    public static readonly RequiredField ProposedBusinessLocation = new(nameof(ProposedBusinessLocation), "Proposed Business Location", "Business Details");
    public static readonly RequiredField CapitalRequired = new(nameof(CapitalRequired), "Capital Required", "Business Details");
    public static readonly RequiredField CapitalAlreadyAvailable = new(nameof(CapitalAlreadyAvailable), "Capital Already Available", "Business Details");
    public static readonly RequiredField EmergencyContactName = new(nameof(EmergencyContactName), "Emergency Contact Name", "Business Details");
    public static readonly RequiredField EmergencyContactPhone = new(nameof(EmergencyContactPhone), "Emergency Contact Phone", "Business Details");
    public static readonly RequiredField PriorBusinessDetails = new(nameof(PriorBusinessDetails), "Prior Business Details", "Business Details");

    /// <summary>A4: required for EVERY category (not just staff-entered ones) once Google Form
    /// intake made it possible for an application to exist with no amount at all — blocks Approved
    /// until staff fill it in. See <see cref="ApplicationCompletenessEvaluator.MissingBaseFields"/>.</summary>
    public static readonly RequiredField RequestedAmount = new(nameof(RequestedAmount), "Requested Amount", "Request");

    public static readonly RequiredField StudentName = new(nameof(StudentName), "Student's Name", "Education Details");
    public static readonly RequiredField CurrentClass = new(nameof(CurrentClass), "Current Class", "Education Details");
    public static readonly RequiredField MotherName = new(nameof(MotherName), "Mother's Name", "Education Details");

    public static readonly RequiredField HealthApplicantAge = new(nameof(HealthApplicantAge), "Applicant's Age", "Health Details");

    private static readonly Dictionary<string, IReadOnlyList<RequiredField>> FieldsByCategory = new()
    {
        [HouseRent] =
        [
            ApplicantAge, MonthlyRent, AdvancePaid, YearsAtCurrentAddress, PreviousAssistanceDetails,
            DeclaredMonthlyIncome, DeclaredHouseholdSize, DeclaredResidentialAddress, DeclaredHouseStatus,
            DeclarationAcceptedAt, TermsAcceptedAt,
        ],
        [Shaadi] =
        [
            GuardianRelationshipToBride, BrideName, BrideFatherName, BrideCnic, BrideMaritalStatus, BrideJamaat,
            BridePreviousHusbandName, GroomName, GroomFatherName, GroomJamaat, GroomMaritalStatus,
            // Item 5 (2026-09 feedback): GroomAddress/GroomMobile stay in this list for label lookup
            // only — MissingMarriageFields below no longer treats them as blockers, so they will
            // never actually appear as a missing field. Kept here (not removed) because
            // RequiredFieldsFor is documented as "every field this category CAN require," and this
            // list has no production consumer to break by leaving them in (see the type's own
            // remarks); removing them would just be churn.
            GroomAddress, GroomMobile, GroomPreviousWifeName, NikahDate,
            DeclarationAcceptedAt, TermsAcceptedAt,
        ],
        [Rozgar] =
        [
            Skill, Experience, TotalMonthlyExpenses, ProposedBusinessDescription, ProposedBusinessLocation,
            CapitalRequired, CapitalAlreadyAvailable, EmergencyContactName, EmergencyContactPhone, PriorBusinessDetails,
            DeclaredBusinessAddress,
            DeclarationAcceptedAt, TermsAcceptedAt,
        ],
        [Education] = [StudentName, CurrentClass, MotherName],
        [Health] = [HealthApplicantAge],
    };

    /// <summary>The document slots (satisfied/unsatisfied) for a category — empty for categories with no manifest (HEALTH/EDUCATION/EMERGENCY/OTHER).</summary>
    public static IReadOnlyList<RequiredDocumentSlot> DocumentSlotsFor(string categoryCode) =>
        SlotsByCategory.GetValueOrDefault(categoryCode, []);

    /// <summary>Every field this category can require — the source evaluators consult for labels; whether a given field is ACTUALLY missing (incl. conditionals) is decided by <c>ApplicationCompletenessEvaluator</c>, not here.</summary>
    public static IReadOnlyList<RequiredField> RequiredFieldsFor(string categoryCode) =>
        FieldsByCategory.GetValueOrDefault(categoryCode, []);

    public static RequiredDocumentSlot? FindSlot(string categoryCode, string slotKey) =>
        DocumentSlotsFor(categoryCode).SingleOrDefault(s => s.SlotKey == slotKey);
}
