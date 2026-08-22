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

    // v1.4: APPLICANT_CNIC/MEMBERSHIP_CARD slots below are Applicant-scoped, not
    // Application-scoped — the applicant's own CNIC and Jamaat membership card, already mandatory
    // on the applicant record itself (see ApplicantService.CreateAsync), satisfy these slots so
    // staff aren't asked to upload the same two documents again on every application. See
    // ApplicationCompletenessEvaluator.EvaluateSlots for how an Applicant-scoped slot is matched.
    private static readonly IReadOnlyList<RequiredDocumentSlot> HouseRentSlots =
    [
        new("HOUSE_RENT.APPLICANT_CNIC", "Applicant's CNIC", true, 1, DocumentSlotOwnerScope.Applicant, [DocumentType.CnicFront, DocumentType.CnicBack]),
        new("HOUSE_RENT.MEMBERSHIP_CARD", "Applicant's Jamaat Membership Card", true, 1, DocumentSlotOwnerScope.Applicant, [DocumentType.MembershipCard]),
        new("HOUSE_RENT.UTILITY_BILLS", "3 Months' Utility Bills", true, 3, DocumentSlotOwnerScope.Application, [DocumentType.UtilityBill]),
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
        new("SHAADI.GROOM_CNIC", "Groom's CNIC", true, 1, DocumentSlotOwnerScope.Application, [DocumentType.CnicFront]),
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
        new("ROZGAR.UTILITY_BILLS", "Photocopies of Household Utility Bills", true, 1, DocumentSlotOwnerScope.Application, [DocumentType.UtilityBill]),
        // Guarantor slots stay Guarantor-scoped on purpose — they are the GUARANTOR's own CNIC/
        // membership card, a different person from the applicant.
        new("ROZGAR.GUARANTOR_CNIC", "Guarantor's CNIC", true, 1, DocumentSlotOwnerScope.Guarantor, [DocumentType.CnicFront, DocumentType.CnicBack]),
        new("ROZGAR.GUARANTOR_MEMBERSHIP_CARD", "Guarantor's Jamaat Membership Card", true, 1, DocumentSlotOwnerScope.Guarantor, [DocumentType.MembershipCard]),
    ];

    private static readonly Dictionary<string, IReadOnlyList<RequiredDocumentSlot>> SlotsByCategory = new()
    {
        [HouseRent] = HouseRentSlots,
        [Shaadi] = ShaadiSlots,
        [Rozgar] = RozgarSlots,
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
            BridePreviousHusbandName, GroomName, GroomFatherName, GroomJamaat, GroomMaritalStatus, GroomAddress,
            GroomMobile, GroomPreviousWifeName, NikahDate,
            DeclarationAcceptedAt, TermsAcceptedAt,
        ],
        [Rozgar] =
        [
            Skill, Experience, TotalMonthlyExpenses, ProposedBusinessDescription, ProposedBusinessLocation,
            CapitalRequired, CapitalAlreadyAvailable, EmergencyContactName, EmergencyContactPhone, PriorBusinessDetails,
            DeclaredBusinessAddress,
            DeclarationAcceptedAt, TermsAcceptedAt,
        ],
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
