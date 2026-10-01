using NgoFund.Domain.Applications;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;

namespace NgoFund.UnitTests.Domain;

public class ApplicationCompletenessEvaluatorTests
{
    private static ApplicationCategory Category(string code, string? termsText = "Some terms") => new()
    {
        Code = code,
        Name = code,
        TermsText = termsText,
    };

    private static FundApplication Application(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        ApplicationNumber = "APP-0001",
        // A4: RequestedAmount is now a base required field for every category — defaulted here so
        // every pre-existing test in this file (written before that rule existed) keeps testing
        // what it actually means to test, not incidentally failing on a field it never cared about.
        RequestedAmount = 10000m,
    };

    // --- MissingHousingFields ---

    [Fact]
    public void MissingHousingFields_NullDetails_ReportsAllRequiredBaseFields()
    {
        var missing = ApplicationCompletenessEvaluator.MissingHousingFields(null);

        Assert.Contains(ApplicationRequirements.ApplicantAge, missing);
        Assert.Contains(ApplicationRequirements.MonthlyRent, missing);
        Assert.Contains(ApplicationRequirements.AdvancePaid, missing);
        Assert.Contains(ApplicationRequirements.YearsAtCurrentAddress, missing);
        // ReceivedAssistanceBefore defaults to false on a bare `new()`, so the conditional field isn't required.
        Assert.DoesNotContain(ApplicationRequirements.PreviousAssistanceDetails, missing);
    }

    [Fact]
    public void MissingHousingFields_ReceivedAssistanceBeforeTrue_WithoutDetails_ReportsPreviousAssistanceDetails()
    {
        var details = new HousingApplicationDetails
        {
            ApplicantAge = 40, MonthlyRent = 10000m, AdvancePaid = 20000m, YearsAtCurrentAddress = 2,
            ReceivedAssistanceBefore = true, PreviousAssistanceDetails = null,
        };

        var missing = ApplicationCompletenessEvaluator.MissingHousingFields(details);

        Assert.Contains(ApplicationRequirements.PreviousAssistanceDetails, missing);
    }

    [Fact]
    public void MissingHousingFields_EveryFieldFilled_ReportsNothing()
    {
        var details = new HousingApplicationDetails
        {
            ApplicantAge = 40, MonthlyRent = 10000m, AdvancePaid = 20000m, YearsAtCurrentAddress = 2,
            ReceivedAssistanceBefore = false,
        };

        Assert.Empty(ApplicationCompletenessEvaluator.MissingHousingFields(details));
    }

    // --- MissingMarriageFields ---

    [Fact]
    public void MissingMarriageFields_DivorcedBride_WithoutPreviousHusbandName_IsMissing()
    {
        var details = FullMarriageDetails();
        details.BrideMaritalStatus = MaritalStatus.Divorced;
        details.BridePreviousHusbandName = null;

        var missing = ApplicationCompletenessEvaluator.MissingMarriageFields(details);

        Assert.Contains(ApplicationRequirements.BridePreviousHusbandName, missing);
    }

    [Fact]
    public void MissingMarriageFields_SingleBride_PreviousHusbandNameNeverRequired()
    {
        var details = FullMarriageDetails();
        details.BrideMaritalStatus = MaritalStatus.Single;
        details.BridePreviousHusbandName = null;

        Assert.DoesNotContain(ApplicationRequirements.BridePreviousHusbandName, ApplicationCompletenessEvaluator.MissingMarriageFields(details));
    }

    [Fact]
    public void MissingMarriageFields_WidowedGroom_WithoutPreviousWifeName_IsMissing()
    {
        var details = FullMarriageDetails();
        details.GroomMaritalStatus = MaritalStatus.Widowed;
        details.GroomPreviousWifeName = null;

        Assert.Contains(ApplicationRequirements.GroomPreviousWifeName, ApplicationCompletenessEvaluator.MissingMarriageFields(details));
    }

    [Fact]
    public void MissingMarriageFields_EveryFieldFilled_ReportsNothing()
    {
        Assert.Empty(ApplicationCompletenessEvaluator.MissingMarriageFields(FullMarriageDetails()));
    }

    [Fact]
    public void MissingMarriageFields_FormBSatisfiesBrideCnicOrBFormSlot()
    {
        // Slot-level (document) behavior, not a field — proven in the slot tests below, but this
        // confirms BrideCnic (the FIELD) is independent of which document type satisfies the slot.
        var details = FullMarriageDetails();
        details.BrideCnic = "40001-1111111-1";

        Assert.DoesNotContain(ApplicationRequirements.BrideCnic, ApplicationCompletenessEvaluator.MissingMarriageFields(details));
    }

    private static MarriageApplicationDetails FullMarriageDetails() => new()
    {
        GuardianRelationshipToBride = "Father",
        BrideName = "Bride",
        BrideFatherName = "Bride Father",
        BrideCnic = "40001-1111111-1",
        BrideMaritalStatus = MaritalStatus.Single,
        BrideJamaat = "Central",
        GroomName = "Groom",
        GroomFatherName = "Groom Father",
        GroomJamaat = "Central",
        GroomMaritalStatus = MaritalStatus.Single,
        GroomAddress = "Groom Address",
        GroomMobile = "0300-0000000",
        NikahDate = new DateOnly(2026, 6, 1),
    };

    // --- MissingBusinessLoanFields ---

    [Fact]
    public void MissingBusinessLoanFields_PriorExperienceTrue_WithoutDetails_IsMissing()
    {
        var details = FullBusinessLoanDetails();
        details.HasPriorBusinessExperience = true;
        details.PriorBusinessDetails = null;

        Assert.Contains(ApplicationRequirements.PriorBusinessDetails, ApplicationCompletenessEvaluator.MissingBusinessLoanFields(details));
    }

    [Fact]
    public void MissingBusinessLoanFields_EveryFieldFilled_ReportsNothing()
    {
        Assert.Empty(ApplicationCompletenessEvaluator.MissingBusinessLoanFields(FullBusinessLoanDetails()));
    }

    private static BusinessLoanApplicationDetails FullBusinessLoanDetails() => new()
    {
        Skill = "Tailoring", Experience = "5 years", TotalMonthlyExpenses = 10000m,
        ProposedBusinessDescription = "Shop", ProposedBusinessLocation = "Bazaar",
        CapitalRequired = 50000m, CapitalAlreadyAvailable = 10000m,
        HasPriorBusinessExperience = false,
        EmergencyContactName = "Contact", EmergencyContactPhone = "0300-0000000",
    };

    // --- MissingBaseFields ---

    [Fact]
    public void MissingBaseFields_HouseRent_WithoutDeclaredFields_ReportsThem()
    {
        var application = Application();
        var category = Category("HOUSE_RENT", termsText: null); // no terms -> signing block not required

        var missing = ApplicationCompletenessEvaluator.MissingBaseFields(application, category);

        Assert.Contains(ApplicationRequirements.DeclaredMonthlyIncome, missing);
        Assert.Contains(ApplicationRequirements.DeclaredHouseholdSize, missing);
        Assert.Contains(ApplicationRequirements.DeclaredResidentialAddress, missing);
        Assert.Contains(ApplicationRequirements.DeclaredHouseStatus, missing);
    }

    [Fact]
    public void MissingBaseFields_CategoryWithTermsText_RequiresDeclarationAndTermsOnly()
    {
        var application = Application();
        var category = Category("SHAADI", termsText: "Some terms");

        var missing = ApplicationCompletenessEvaluator.MissingBaseFields(application, category);

        // In-app signature capture (SignedByName) was dropped per the client's request — the
        // signing block now only requires DeclarationAcceptedAt/TermsAcceptedAt.
        Assert.Contains(ApplicationRequirements.DeclarationAcceptedAt, missing);
        Assert.Contains(ApplicationRequirements.TermsAcceptedAt, missing);
        Assert.DoesNotContain(missing, f => f.Key == "SignedByName");
    }

    [Fact]
    public void MissingBaseFields_CategoryWithoutTermsText_NeverRequiresSigningBlock()
    {
        var application = Application();
        var category = Category("HEALTH", termsText: null);

        Assert.Empty(ApplicationCompletenessEvaluator.MissingBaseFields(application, category));
    }

    [Fact]
    public void MissingBaseFields_Rozgar_RequiresDeclaredBusinessAddress()
    {
        var application = Application();
        var category = Category("ROZGAR", termsText: null);

        Assert.Contains(ApplicationRequirements.DeclaredBusinessAddress, ApplicationCompletenessEvaluator.MissingBaseFields(application, category));
    }

    // A4: RequestedAmount is required for every category now — the field Google Form intake can
    // leave null (every category except ROZGAR's form has no amount question).
    [Theory]
    [InlineData("HOUSE_RENT")]
    [InlineData("EDUCATION")]
    [InlineData("HEALTH")]
    [InlineData("OTHER")]
    [InlineData("EMERGENCY")]
    public void MissingBaseFields_NullRequestedAmount_IsMissing_ForEveryCategory(string categoryCode)
    {
        var application = Application();
        application.RequestedAmount = null;
        var category = Category(categoryCode, termsText: null);

        Assert.Contains(ApplicationRequirements.RequestedAmount, ApplicationCompletenessEvaluator.MissingBaseFields(application, category));
    }

    [Fact]
    public void MissingBaseFields_RequestedAmountSet_IsNotMissing()
    {
        var application = Application(); // RequestedAmount = 10000m by default
        var category = Category("HEALTH", termsText: null);

        Assert.DoesNotContain(ApplicationRequirements.RequestedAmount, ApplicationCompletenessEvaluator.MissingBaseFields(application, category));
    }

    // --- MissingEducationFields / MissingHealthFields (A9) ---

    [Fact]
    public void MissingEducationFields_NullDetails_ReportsAllThree()
    {
        var missing = ApplicationCompletenessEvaluator.MissingEducationFields(null);

        Assert.Contains(ApplicationRequirements.StudentName, missing);
        Assert.Contains(ApplicationRequirements.CurrentClass, missing);
        Assert.Contains(ApplicationRequirements.MotherName, missing);
    }

    [Fact]
    public void MissingEducationFields_EveryFieldFilled_ReportsNothing()
    {
        var details = new EducationApplicationDetails { StudentName = "S", CurrentClass = "5th", MotherName = "M" };

        Assert.Empty(ApplicationCompletenessEvaluator.MissingEducationFields(details));
    }

    [Fact]
    public void MissingHealthFields_NullAge_IsMissing()
    {
        Assert.Contains(ApplicationRequirements.HealthApplicantAge, ApplicationCompletenessEvaluator.MissingHealthFields(null));
    }

    [Fact]
    public void MissingHealthFields_AgeSet_ReportsNothing()
    {
        Assert.Empty(ApplicationCompletenessEvaluator.MissingHealthFields(new HealthApplicationDetails { ApplicantAge = 30 }));
    }

    // --- MissingGuarantorFields (A10) ---

    [Fact]
    public void MissingGuarantorFields_SinglePhoneOnly_ReportsHomeAndOfficePhone_NotMobile()
    {
        var guarantor = new ApplicationGuarantor
        {
            SequenceNumber = 1, FullName = "G1", Cnic = "40001-1111111-1", MembershipNumber = "M1",
            ResidentialAddress = "Addr", BusinessAddress = "Biz", PhoneMobile = "0300-0000000",
            PhoneHome = null, PhoneOffice = null,
        };

        var missing = ApplicationCompletenessEvaluator.MissingGuarantorFields([guarantor]);

        Assert.Contains(missing, f => f.Label == "Guarantor 1: Home Phone");
        Assert.Contains(missing, f => f.Label == "Guarantor 1: Office Phone");
        Assert.DoesNotContain(missing, f => f.Label.Contains("Mobile"));
    }

    [Fact]
    public void MissingGuarantorFields_EveryFieldFilled_ReportsNothing()
    {
        var guarantor = new ApplicationGuarantor
        {
            SequenceNumber = 2, FullName = "G2", Cnic = "40001-1111111-1", MembershipNumber = "M2",
            ResidentialAddress = "Addr", BusinessAddress = "Biz", PhoneHome = "021-1111111",
            PhoneOffice = "021-2222222", PhoneMobile = "0300-0000000",
        };

        Assert.Empty(ApplicationCompletenessEvaluator.MissingGuarantorFields([guarantor]));
    }

    [Fact]
    public void MissingGuarantorFields_NoGuarantors_ReportsNothing()
    {
        Assert.Empty(ApplicationCompletenessEvaluator.MissingGuarantorFields([]));
    }

    // --- EvaluateSlots ---

    private static Document Doc(Guid? applicationId, Guid? guarantorId, DocumentType type, string? slotKey) => new()
    {
        ApplicationId = applicationId,
        ApplicationGuarantorId = guarantorId,
        DocumentType = type,
        SlotKey = slotKey,
        FileName = "f", StorageKey = "k", ContentType = "image/jpeg", Sha256 = "h",
    };

    [Fact]
    public void EvaluateSlots_NullSlotKey_SatisfiesNothing()
    {
        var appId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var docs = new[] { Doc(appId, null, DocumentType.CnicFront, slotKey: null) };

        var slots = ApplicationCompletenessEvaluator.EvaluateSlots(appId, applicantId, "HOUSE_RENT", docs, []);

        // Scoped to slots that actually require at least one document — HOUSE_RENT.UTILITY_BILLS
        // is optional with MinCount 0 (item 6, 2026-09 feedback) so it always reads as satisfied
        // regardless of what's on file, which is unrelated to what this test is proving.
        Assert.All(slots.Where(s => s.Slot.MinCount > 0), s => Assert.False(s.IsSatisfied));
    }

    [Fact]
    public void EvaluateSlots_WeddingCardAndRentReceipts_ReportedButNeverBlockRequiredCheck()
    {
        var appId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();

        var shaadiSlots = ApplicationCompletenessEvaluator.EvaluateSlots(appId, applicantId, "SHAADI", [], []);
        var weddingCard = shaadiSlots.Single(s => s.Slot.SlotKey == "SHAADI.WEDDING_CARD");
        Assert.False(weddingCard.IsSatisfied);
        Assert.False(weddingCard.Slot.IsRequired);

        var houseRentSlots = ApplicationCompletenessEvaluator.EvaluateSlots(appId, applicantId, "HOUSE_RENT", [], []);
        var rentReceipts = houseRentSlots.Single(s => s.Slot.SlotKey == "HOUSE_RENT.RENT_RECEIPTS");
        Assert.False(rentReceipts.IsSatisfied);
        Assert.False(rentReceipts.Slot.IsRequired);
    }

    // Item 6 (2026-09 feedback): HOUSE_RENT.UTILITY_BILLS is now fully optional with MinCount 0 —
    // "an applicant may provide None" and must never show as a partial/incomplete upload state.

    [Fact]
    public void EvaluateSlots_UtilityBillsMinCountZero_NoDocumentsAtAll_StillReportsSatisfied()
    {
        var appId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();

        var slots = ApplicationCompletenessEvaluator.EvaluateSlots(appId, applicantId, "HOUSE_RENT", [], []);
        var utilityBills = slots.Single(s => s.Slot.SlotKey == "HOUSE_RENT.UTILITY_BILLS");

        Assert.True(utilityBills.IsSatisfied);
        Assert.False(utilityBills.Slot.IsRequired);
        Assert.Empty(utilityBills.Documents);
    }

    [Fact]
    public void EvaluateSlots_UtilityBillsMinCountZero_PartialUploadsStillReportSatisfied()
    {
        var appId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var docs = new[]
        {
            Doc(appId, null, DocumentType.UtilityBill, "HOUSE_RENT.UTILITY_BILLS"),
            Doc(appId, null, DocumentType.UtilityBill, "HOUSE_RENT.UTILITY_BILLS"),
        };

        var slots = ApplicationCompletenessEvaluator.EvaluateSlots(appId, applicantId, "HOUSE_RENT", docs, []);
        var utilityBills = slots.Single(s => s.Slot.SlotKey == "HOUSE_RENT.UTILITY_BILLS");

        Assert.True(utilityBills.IsSatisfied);
        Assert.Equal(2, utilityBills.Documents.Count);
    }

    [Fact]
    public void EvaluateSlots_MinCountTwo_PassportPhotos_PartiallySatisfied_StillReportsUnsatisfied()
    {
        var appId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var docs = new[] { Doc(appId, null, DocumentType.PassportPhoto, "ROZGAR.PASSPORT_PHOTOS") };

        var slots = ApplicationCompletenessEvaluator.EvaluateSlots(appId, applicantId, "ROZGAR", docs, []);

        Assert.False(slots.Single(s => s.Slot.SlotKey == "ROZGAR.PASSPORT_PHOTOS").IsSatisfied);
    }

    [Fact]
    public void EvaluateSlots_FormBSatisfiesBrideCnicOrBFormSlot()
    {
        var appId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var docs = new[] { Doc(appId, null, DocumentType.FormB, "SHAADI.BRIDE_CNIC_OR_BFORM") };

        var slots = ApplicationCompletenessEvaluator.EvaluateSlots(appId, applicantId, "SHAADI", docs, []);

        Assert.True(slots.Single(s => s.Slot.SlotKey == "SHAADI.BRIDE_CNIC_OR_BFORM").IsSatisfied);
    }

    [Fact]
    public void EvaluateSlots_NoGuarantors_EmitsZeroGuarantorScopedSlots()
    {
        var appId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();

        var slots = ApplicationCompletenessEvaluator.EvaluateSlots(appId, applicantId, "ROZGAR", [], []);

        Assert.DoesNotContain(slots, s => s.Slot.OwnerScope == DocumentSlotOwnerScope.Guarantor);
    }

    [Fact]
    public void EvaluateSlots_TwoGuarantors_EmitsGuarantorScopedSlotsOncePerGuarantor()
    {
        var appId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var guarantors = new[]
        {
            new ApplicationGuarantor { Id = Guid.NewGuid(), ApplicationId = appId, SequenceNumber = 1, FullName = "G1" },
            new ApplicationGuarantor { Id = Guid.NewGuid(), ApplicationId = appId, SequenceNumber = 2, FullName = "G2" },
        };

        var slots = ApplicationCompletenessEvaluator.EvaluateSlots(appId, applicantId, "ROZGAR", [], guarantors);
        var guarantorSlots = slots.Where(s => s.Slot.OwnerScope == DocumentSlotOwnerScope.Guarantor).ToList();

        // 2 guarantor-scoped slot definitions (CNIC, membership card) x 2 guarantors = 4 statuses.
        Assert.Equal(4, guarantorSlots.Count);
        Assert.Equal(2, guarantorSlots.Count(s => s.GuarantorId == guarantors[0].Id));
        Assert.Equal(2, guarantorSlots.Count(s => s.GuarantorId == guarantors[1].Id));
    }

    [Fact]
    public void EvaluateSlots_GuarantorDocument_DoesNotSatisfyApplicantScopedSlotOfSameType()
    {
        var appId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var guarantorId = Guid.NewGuid();
        var guarantors = new[] { new ApplicationGuarantor { Id = guarantorId, ApplicationId = appId, SequenceNumber = 1, FullName = "G1" } };
        var docs = new[] { Doc(null, guarantorId, DocumentType.CnicFront, "ROZGAR.GUARANTOR_CNIC") };

        var slots = ApplicationCompletenessEvaluator.EvaluateSlots(appId, applicantId, "ROZGAR", docs, guarantors);

        // ROZGAR.APPLICANT_CNIC is Applicant-scoped (v1.4) — a guarantor-owned document (neither
        // ApplicantId nor ApplicationId set) satisfies neither the applicant-profile path nor the
        // legacy application-owned fallback.
        Assert.False(slots.Single(s => s.Slot.SlotKey == "ROZGAR.APPLICANT_CNIC").IsSatisfied);
        Assert.True(slots.Single(s => s.Slot.SlotKey == "ROZGAR.GUARANTOR_CNIC" && s.GuarantorId == guarantorId).IsSatisfied);
    }

    [Fact]
    public void EvaluateSlots_ApplicantOwnedMembershipCard_DoesNotSatisfyBrideMembershipCardSlot_ButSatisfiesApplicantSlot()
    {
        // SHAADI.BRIDE_MEMBERSHIP_CARD is Application-scoped on purpose despite accepting the same
        // DocumentType (MembershipCard) as the Applicant-scoped SHAADI.APPLICANT_MEMBERSHIP_CARD
        // slot. If the applicant-profile match ever widened to ignore OwnerScope, the bride's
        // requirement would silently pass off an unrelated document and let an incomplete SHAADI
        // application reach Approved.
        var appId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var doc = new Document
        {
            ApplicantId = applicantId,
            DocumentType = DocumentType.MembershipCard,
            SlotKey = null, // applicant profile documents are always uploaded with slotKey: null
            FileName = "f", StorageKey = "k", ContentType = "image/jpeg", Sha256 = "h",
        };

        var slots = ApplicationCompletenessEvaluator.EvaluateSlots(appId, applicantId, "SHAADI", [doc], []);

        Assert.False(slots.Single(s => s.Slot.SlotKey == "SHAADI.BRIDE_MEMBERSHIP_CARD").IsSatisfied);
        Assert.True(slots.Single(s => s.Slot.SlotKey == "SHAADI.APPLICANT_MEMBERSHIP_CARD").IsSatisfied);
    }

    [Fact]
    public void EvaluateSlots_ApplicantOwnedCnicFront_DoesNotSatisfyGroomOrBrideCnicSlots_ButSatisfiesApplicantSlot()
    {
        // Same reasoning as the membership-card trap above: SHAADI.GROOM_CNIC and
        // SHAADI.BRIDE_CNIC_OR_BFORM both accept DocumentType.CnicFront and both stay
        // Application-scoped on purpose, so an applicant-owned CNIC must not satisfy either.
        var appId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var doc = new Document
        {
            ApplicantId = applicantId,
            DocumentType = DocumentType.CnicFront,
            SlotKey = null,
            FileName = "f", StorageKey = "k", ContentType = "image/jpeg", Sha256 = "h",
        };

        var slots = ApplicationCompletenessEvaluator.EvaluateSlots(appId, applicantId, "SHAADI", [doc], []);

        Assert.False(slots.Single(s => s.Slot.SlotKey == "SHAADI.GROOM_CNIC").IsSatisfied);
        Assert.False(slots.Single(s => s.Slot.SlotKey == "SHAADI.BRIDE_CNIC_OR_BFORM").IsSatisfied);
        Assert.True(slots.Single(s => s.Slot.SlotKey == "SHAADI.APPLICANT_CNIC").IsSatisfied);
    }

    [Fact]
    public void EvaluateSlots_ApplicantOwnedCnic_SatisfiesApplicantScopedSlot_IgnoringSlotKey()
    {
        var appId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var doc = new Document
        {
            ApplicantId = applicantId,
            DocumentType = DocumentType.CnicFront,
            SlotKey = null, // applicant profile documents are always uploaded with slotKey: null
            FileName = "f", StorageKey = "k", ContentType = "image/jpeg", Sha256 = "h",
        };

        var slots = ApplicationCompletenessEvaluator.EvaluateSlots(appId, applicantId, "HOUSE_RENT", [doc], []);
        var slot = slots.Single(s => s.Slot.SlotKey == "HOUSE_RENT.APPLICANT_CNIC");

        Assert.True(slot.IsSatisfied);
        Assert.True(slot.SatisfiedByApplicantProfile);
    }

    [Fact]
    public void EvaluateSlots_LegacyApplicationOwnedCnic_StillSatisfiesApplicantScopedSlot()
    {
        var appId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var docs = new[] { Doc(appId, null, DocumentType.CnicFront, "HOUSE_RENT.APPLICANT_CNIC") };

        var slots = ApplicationCompletenessEvaluator.EvaluateSlots(appId, applicantId, "HOUSE_RENT", docs, []);
        var slot = slots.Single(s => s.Slot.SlotKey == "HOUSE_RENT.APPLICANT_CNIC");

        Assert.True(slot.IsSatisfied);
        Assert.False(slot.SatisfiedByApplicantProfile);
    }

    // --- Evaluate (dispatcher) regression: EMERGENCY still has no manifest at all ---

    [Fact]
    public void Evaluate_Emergency_IsAlwaysComplete_RegardlessOfEmptyData()
    {
        var application = Application();
        var category = Category("EMERGENCY", termsText: null);

        var result = ApplicationCompletenessEvaluator.Evaluate(application, category, null, null, null, null, null, [], []);

        Assert.True(result.IsComplete);
        Assert.Empty(result.MissingFields);
        Assert.Empty(result.Slots);
    }

    // A9: HEALTH/EDUCATION/OTHER now DO have a manifest (fields and/or document slots).

    [Fact]
    public void Evaluate_Health_EmptyData_IsIncomplete_AndListsExpectedGaps()
    {
        var application = Application();
        var category = Category("HEALTH", termsText: null);

        var result = ApplicationCompletenessEvaluator.Evaluate(application, category, null, null, null, null, null, [], []);

        Assert.False(result.IsComplete);
        Assert.Contains(ApplicationRequirements.HealthApplicantAge, result.MissingFields);
        Assert.Contains(result.Slots, s => s.Slot.SlotKey == "HEALTH.MEDICAL_DOCUMENTS" && s.Slot.IsRequired && !s.IsSatisfied);
    }

    [Fact]
    public void Evaluate_Education_EmptyData_IsIncomplete_AndListsExpectedGaps()
    {
        var application = Application();
        var category = Category("EDUCATION", termsText: null);

        var result = ApplicationCompletenessEvaluator.Evaluate(application, category, null, null, null, null, null, [], []);

        Assert.False(result.IsComplete);
        Assert.Contains(ApplicationRequirements.StudentName, result.MissingFields);
        Assert.Contains(result.Slots, s => s.Slot.SlotKey == "EDUCATION.STUDENT_BFORM_OR_CNIC" && s.Slot.IsRequired && !s.IsSatisfied);
    }

    [Fact]
    public void Evaluate_Other_EmptyData_IsIncomplete_OnDocumentSlotsOnly_NoFields()
    {
        var application = Application();
        var category = Category("OTHER", termsText: null);

        var result = ApplicationCompletenessEvaluator.Evaluate(application, category, null, null, null, null, null, [], []);

        Assert.False(result.IsComplete);
        // OTHER has no category-specific fields (A9) — only the base RequestedAmount field (already
        // satisfied by the Application() factory default) plus its three document slots.
        Assert.DoesNotContain(result.MissingFields, f => f.SectionLabel != "Request");
        Assert.Contains(result.Slots, s => s.Slot.SlotKey == "OTHER.SUPPORTING_DOCUMENTS" && !s.IsSatisfied);
    }

    [Fact]
    public void Evaluate_Rozgar_GuarantorMissingContactFields_BlocksCompleteness()
    {
        var application = Application();
        var category = Category("ROZGAR", termsText: null);
        var businessLoan = FullBusinessLoanDetails();
        application.DeclaredBusinessAddress = "Business address";
        var guarantor = new ApplicationGuarantor
        {
            SequenceNumber = 1, FullName = "G1", Cnic = "40001-1111111-1", MembershipNumber = "M1",
            ResidentialAddress = "Addr", BusinessAddress = "Biz", PhoneMobile = "0300-0000000",
            PhoneHome = null, PhoneOffice = null, // Google Form only collects one phone (A10)
        };

        var result = ApplicationCompletenessEvaluator.Evaluate(application, category, null, null, businessLoan, null, null, [], [guarantor]);

        Assert.False(result.IsComplete);
        Assert.Contains(result.MissingFields, f => f.Label == "Guarantor 1: Home Phone");
    }
}
