using NgoFund.Application.Intake;
using NgoFund.Contracts.Intake;
using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;

namespace NgoFund.UnitTests.Intake;

public class GoogleFormSubmissionMapperTests
{
    private static readonly DateTimeOffset SubmittedAt = new(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);

    private static GoogleFormSubmissionRequest Request(
        string applicationType, IEnumerable<(string Title, string? Help, string[] Values)> answers, IReadOnlyList<GoogleFormFileManifestEntry>? files = null) =>
        new("resp-1", SubmittedAt, "someone@example.com", applicationType,
            answers.Select(a => new GoogleFormAnswer(a.Title, a.Help, a.Values)).ToList(),
            files ?? []);

    private static (string Title, string? Help, string[] Values) A(string title, params string[] values) => (title, null, values);

    // ---------- ApplicationType -> category/fund ----------

    [Theory]
    [InlineData("Housing Assistance", "HOUSE_RENT", "ZAKAT")]
    [InlineData("Marriage Assistance", "SHAADI", "ZAKAT")]
    [InlineData("Business Loan", "ROZGAR", "GENERAL")]
    [InlineData("Education Assistance", "EDUCATION", "ZAKAT")]
    [InlineData("Health Assistance", "HEALTH", "ZAKAT")]
    [InlineData("General Assistance Form", "OTHER", "ZAKAT")]
    public void Map_ApplicationType_ResolvesCategoryAndFund(string applicationType, string expectedCategory, string expectedFund)
    {
        var request = applicationType switch
        {
            "Housing Assistance" => HousingRequest(),
            "Marriage Assistance" => MarriageRequest(),
            "Business Loan" => BusinessLoanRequest(),
            "Education Assistance" => EducationRequest(),
            "Health Assistance" => HealthRequest(),
            _ => OtherRequest(),
        };

        var result = GoogleFormSubmissionMapper.Map(request);

        Assert.Equal(expectedCategory, result.CategoryCode);
        Assert.Equal(expectedFund, result.FundCategoryCode);
    }

    [Fact]
    public void Map_ApplicationTypeWithUrduParenthetical_ResolvesCategory()
    {
        // The live form's radio option value is "Housing Assistance (رہائشی مکان مدد)", not the
        // bare English text — regression for the 2026-09-30 intake pipeline failure.
        var request = Request("Housing Assistance (رہائشی مکان مدد)",
            [A("CNIC Number", "42101-3704613-7"), A("Full Name", "X")]);

        var result = GoogleFormSubmissionMapper.Map(request);

        Assert.Equal("HOUSE_RENT", result.CategoryCode);
    }

    [Fact]
    public void Map_UnknownApplicationType_Throws()
    {
        var request = Request("Something Else", [A("CNIC Number", "42101-3704613-7"), A("Full Name", "X")]);

        Assert.Throws<UnrecognizedGoogleFormDataException>(() => GoogleFormSubmissionMapper.Map(request));
    }

    [Fact]
    public void Map_UnparseableApplicantCnic_Throws()
    {
        var request = Request("Housing Assistance", [A("CNIC Number", "not-a-cnic"), A("Full Name", "X")]);

        Assert.Throws<UnrecognizedGoogleFormDataException>(() => GoogleFormSubmissionMapper.Map(request));
    }

    // ---------- HOUSE_RENT ----------

    private static GoogleFormSubmissionRequest HousingRequest() => Request("Housing Assistance",
    [
        A("Bhavnagar Jamaat Membership Number", "M1001"),
        A("Full Name", "Applicant One"),
        A("Father's Name", "Father One"),
        A("CNIC Number", "42101-3704613-7"),
        A("Age", "45"),
        A("Current Residential Address", "123 Main St"),
        A("House Status", "Katchi (temporary structure)"),
        A("Current House Value (approx.)", "500000"),
        A("Monthly Rent (if applicable)", "Rs. 15,000"),
        A("Advance Paid (if applicable)", "30000"),
        A("Years Living at Current Address", "3"),
        A("Monthly Income", "40000"),
        A("Number of People in Household", "6"),
        A("Has your family received housing assistance from A.Z Dany Welfare Trust before?", "No"),
        A("Which other assistance do you currently receive from A.Z Dany Welfare Trust?",
            "Medical Assistance (طبی مدد)", "Education Assistance (تعلیمی مدد)"),
    ]);

    [Fact]
    public void Map_Housing_MapsCoreFields()
    {
        // A1-A4: House Status/Current House Value/Monthly Rent/Advance Paid all use the FORM's real
        // titles now (no "(Owned/Katchi/Pucci/Rented)"/no missing "(approx.)"/"(if applicable)").
        var result = GoogleFormSubmissionMapper.Map(HousingRequest());

        Assert.Equal("42101-3704613-7", result.Applicant.Cnic);
        Assert.Equal("Applicant One", result.Applicant.FullName);
        Assert.NotNull(result.Housing);
        Assert.Equal(45, result.Housing!.ApplicantAge);
        Assert.Equal(500000m, result.Housing.CurrentHouseValue);
        Assert.Equal(15000m, result.Housing.MonthlyRent);
        Assert.Equal(30000m, result.Housing.AdvancePaid);
        Assert.Equal("Katchi", result.DeclaredHouseStatus);
        Assert.False(result.Housing.ReceivedAssistanceBefore);
        Assert.Equal("Housing assistance request (Google Form)", result.Purpose);
    }

    [Fact]
    public void Map_Housing_OtherAssistanceCheckboxes_MatchFullOptionNames()
    {
        // A6: checkbox options are "Marriage Assistance (...)" etc. — after parenthetical stripping
        // they are "Marriage Assistance", not "Marriage". A Contains("...", "Marriage") must NOT match.
        var result = GoogleFormSubmissionMapper.Map(HousingRequest());

        Assert.True(result.Housing!.ReceivesMedicalAssistance);
        Assert.True(result.Housing.ReceivesEducationAssistance);
        Assert.False(result.Housing.ReceivesMarriageAssistance);
        Assert.False(result.Housing.ReceivesWidowAssistance);
    }

    [Fact]
    public void Map_Housing_UploadTitlesWithInlineUrduParenthetical_ResolveToSlots()
    {
        // D: the upload QUESTION TITLE itself carries the Urdu translation inline, not a separate
        // options list — must still resolve after stripping the trailing " (...)".
        var membershipFile = new GoogleFormFileManifestEntry("Attach: Bhavnagar Jamaat membership card (ممبر شپ کارڈ)", null, "drive-1", "card.jpg", "image/jpeg");
        var cnicFile = new GoogleFormFileManifestEntry("Attach: photocopy of CNIC (قومی شناختی کارڈ)", null, "drive-2", "cnic.jpg", "image/jpeg");

        var membershipTarget = GoogleFormSubmissionMapper.ResolveFileTarget("HOUSE_RENT", membershipFile);
        var cnicTarget = GoogleFormSubmissionMapper.ResolveFileTarget("HOUSE_RENT", cnicFile);

        Assert.NotNull(membershipTarget);
        Assert.Equal(DocumentType.MembershipCard, membershipTarget!.DocumentType);
        Assert.NotNull(cnicTarget);
        Assert.Equal(DocumentType.CnicFront, cnicTarget!.DocumentType);
    }

    // ---------- SHAADI ----------

    private static GoogleFormSubmissionRequest MarriageRequest() => Request("Marriage Assistance",
    [
        A("Applicant's (Guardian's) Name", "Guardian One"),
        A("Father's Name", "Guardian Father"),
        A("CNIC Number", "42101-3704613-7"),
        A("Bhavnagar Jamaat Membership Number", "M2001"),
        A("House Address", "456 Second St"),
        A("Mobile Number", "0333-2909639"),
        A("Bride's Name", "Bride One"),
        A("Bride's Marital Status", "First Marriage (کنوارہ)"),
        A("Groom's Name", "Groom One"),
        A("Groom's Marital Status", "Widower"),
        A("Previous Wife's Name (if applicable)", "Previous Wife"),
        A("Groom's Jamaat / Community", "Central"),
        A("Groom's Jamaat", "Central Jamaat"),
        A("Declaration", "I am a Jamaat member and declare under oath that I am eligible to receive Zakat, and request that I be helped. I have provided complete and correct information and will accept the committee's decision."),
    ]);

    [Fact]
    public void Map_Marriage_MapsBrideGroomAndDeclaration()
    {
        var result = GoogleFormSubmissionMapper.Map(MarriageRequest());

        Assert.NotNull(result.Marriage);
        Assert.Equal("Bride One", result.Marriage!.BrideName);
        Assert.Equal(MaritalStatus.Single, Enum.Parse<MaritalStatus>(result.Marriage.BrideMaritalStatus!));
        Assert.Equal(MaritalStatus.Widowed, Enum.Parse<MaritalStatus>(result.Marriage.GroomMaritalStatus!));
        Assert.Equal("Previous Wife", result.Marriage.GroomPreviousWifeName);
        Assert.Equal("Central / Central Jamaat", result.Marriage.GroomJamaat);
        Assert.Equal("Marriage assistance for Bride One", result.Purpose);
    }

    [Fact]
    public void Map_Marriage_DeclarationCheckbox_OathTextTicksIt()
    {
        // C: the "Declaration" checkbox's single option is the full oath sentence, never "Yes" —
        // any non-empty selected value must tick it.
        var result = GoogleFormSubmissionMapper.Map(MarriageRequest());

        Assert.Equal(SubmittedAt, result.DeclarationAcceptedAt);
    }

    [Fact]
    public void Map_Marriage_DeclarationNotSelected_LeavesAcceptedAtNull()
    {
        var request = Request("Marriage Assistance",
        [
            A("Applicant's (Guardian's) Name", "Guardian One"),
            A("CNIC Number", "42101-3704613-7"),
            A("Bride's Name", "Bride One"),
        ]);

        var result = GoogleFormSubmissionMapper.Map(request);

        Assert.Null(result.DeclarationAcceptedAt);
    }

    [Fact]
    public void Map_Marriage_FileMapping_BrideCnicAndMembershipCard_NotApplicantOwned()
    {
        // 2026-09 correction: these three uploads on the Marriage form are the BRIDE's documents,
        // not the applicant/guardian's — confirmed directly by the client.
        var files = new[]
        {
            new GoogleFormFileManifestEntry("Attach: photocopy of CNIC", null, "drive-cnic", "cnic.jpg", "image/jpeg"),
            new GoogleFormFileManifestEntry("Attach: photocopy of Bhavnagar Jamaat membership card", null, "drive-card", "card.jpg", "image/jpeg"),
            new GoogleFormFileManifestEntry("Attach:original Marriage/Nikah card", null, "drive-nikah", "nikah.pdf", "application/pdf"),
        };

        var cnicTarget = GoogleFormSubmissionMapper.ResolveFileTarget("SHAADI", files[0]);
        var cardTarget = GoogleFormSubmissionMapper.ResolveFileTarget("SHAADI", files[1]);
        var nikahTarget = GoogleFormSubmissionMapper.ResolveFileTarget("SHAADI", files[2]);

        Assert.Equal(DocumentSlotOwnerScope.Application, cnicTarget!.OwnerScope);
        Assert.Equal("SHAADI.BRIDE_CNIC_OR_BFORM", cnicTarget.SlotKey);

        Assert.Equal(DocumentSlotOwnerScope.Application, cardTarget!.OwnerScope);
        Assert.Equal("SHAADI.BRIDE_MEMBERSHIP_CARD", cardTarget.SlotKey);

        Assert.Equal(DocumentSlotOwnerScope.Application, nikahTarget!.OwnerScope);
        Assert.Equal("SHAADI.WEDDING_CARD", nikahTarget.SlotKey);
        Assert.Equal(DocumentType.WeddingCard, nikahTarget.DocumentType);
    }

    // ---------- ROZGAR ----------

    private static GoogleFormSubmissionRequest BusinessLoanRequest() => Request("Business Loan",
    [
        A("Applicant's Name", "Applicant Rozgar"),
        A("CNIC Number", "42101-3704613-7"),
        A("House Address", "789 Third St"),
        A("House Status", "Rented"),
        A("Marital Status", "Married"),
        A("What business do you want to start? Please explain.", "A tailoring shop"),
        A("Where will you run this business? Please explain.", "From home"),
        A("Loan Amount Needed", "100,000"),
        A("Applicant's Declaration", "I certify that all information about my household provided above is complete, true and correct. I authorize A.Z Dany Welfare Trust to investigate this information, and I understand the committees may reject my application if any information is found to be false."),
        A("Guarantor 1 — Name", "Guarantor One"),
        A("Guarantor 1 — CNIC Number", "42101-3704613-7"),
        A("Guarantor 1 — Phone (Home / Office / Mobile)", "0333-2909639"),
        A("Guarantor 2 — Name", "Guarantor Two"),
        A("Guarantor 2 — Phone (Home / Office / Mobile)", "0333-2909639"),
    ]);

    [Fact]
    public void Map_BusinessLoan_MapsGuarantorsWithMobileOnly()
    {
        var result = GoogleFormSubmissionMapper.Map(BusinessLoanRequest());

        Assert.Equal(2, result.Guarantors.Count);
        var g1 = result.Guarantors.Single(g => g.SequenceNumber == 1);
        Assert.Equal("Guarantor One", g1.FullName);
        Assert.Equal("0333-2909639", g1.PhoneMobile);
        Assert.Equal(100000m, result.RequestedAmount);
        Assert.StartsWith("Business loan:", result.Purpose);
        Assert.Contains(result.Notes, n => n.Contains("signature", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Map_BusinessLoan_MaritalStatus_MarriedUnmarried_MapsToMarriedSingle()
    {
        var result = GoogleFormSubmissionMapper.Map(BusinessLoanRequest());
        Assert.Equal(nameof(MaritalStatus.Married), result.Applicant.MaritalStatus);
    }

    [Fact]
    public void Map_BusinessLoan_RequiredFieldTitles_AreMappedNotUnmapped()
    {
        // A7/A8/A9: "What business..."/"Where will you run..." need "Please explain." to match the
        // real form, and the required "House Status" radio (previously not mapped at all).
        var result = GoogleFormSubmissionMapper.Map(BusinessLoanRequest());

        Assert.NotNull(result.BusinessLoan);
        Assert.Equal("A tailoring shop", result.BusinessLoan!.ProposedBusinessDescription);
        Assert.Equal("From home", result.BusinessLoan.ProposedBusinessLocation);
        Assert.Equal("Rented", result.DeclaredHouseStatus);
        Assert.DoesNotContain(result.Notes, n => n.Contains("Unmapped answer") && n.Contains("House Status"));
    }

    [Fact]
    public void Map_BusinessLoan_DeclarationCheckbox_OathTextTicksIt()
    {
        var result = GoogleFormSubmissionMapper.Map(BusinessLoanRequest());

        Assert.Equal(SubmittedAt, result.DeclarationAcceptedAt);
    }

    // ---------- EDUCATION ----------

    private static GoogleFormSubmissionRequest EducationRequest(IReadOnlyList<GoogleFormFileManifestEntry>? files = null) => Request("Education Assistance",
    [
        A("Census No. (office reference, if known)", "C-1"),
        A("Name of Student", "Student One"),
        A("Current Class", "8th"),
        A("Father's Name", "Father Edu"),
        A("Father's CNIC Number", "42101-3704613-7"),
        A("Father's Bhavnagar Jamaat Membership Number", "M3001"),
        A("Mother's Name", "Mother Edu"),
        A("Mother's Caste / Zaat", "Zaat Name"),
        A("Mother's CNIC Number", "42101-3704613-8"),
        A("Description(Please provide detail regarding the assisstance needed)", "Needs school fees for this term"),
    ], files);

    [Fact]
    public void Map_Education_ApplicantIsFather_StudentAndMotherInDetails()
    {
        var result = GoogleFormSubmissionMapper.Map(EducationRequest());

        Assert.Equal("Father Edu", result.Applicant.FullName);
        Assert.Equal("42101-3704613-7", result.Applicant.Cnic);
        Assert.NotNull(result.Education);
        Assert.Equal("Student One", result.Education!.StudentName);
        Assert.Equal("Mother Edu", result.Education.MotherName);
    }

    [Fact]
    public void Map_Education_RequiredFieldTitles_AreMappedNotUnmapped()
    {
        // A10-A13: Census No./Description/Mother's Caste/Mother's CNIC all need the real form
        // titles — Purpose used to always be null, Mother's Caste/CNIC used to always be dropped.
        var result = GoogleFormSubmissionMapper.Map(EducationRequest());

        Assert.Equal("C-1", result.Education!.CensusNumber);
        Assert.Equal("Zaat Name", result.Education.MotherCaste);
        Assert.Equal("42101-3704613-8", result.Education.MotherCnic);
        Assert.Equal("Needs school fees for this term", result.Purpose);
    }

    [Fact]
    public void Map_Education_FileUpload_DisambiguatedByHelpText()
    {
        var files = new[]
        {
            new GoogleFormFileManifestEntry("File Upload", "Attach the Jamaat Card", "drive-1", "a.jpg", "image/jpeg"),
            new GoogleFormFileManifestEntry("File Upload", "Attach Student's B-Form or Cnic", "drive-2", "b.jpg", "image/jpeg"),
            new GoogleFormFileManifestEntry("File Upload", "Attach a passport-size photo of student", "drive-3", "c.jpg", "image/jpeg"),
        };

        var membershipTarget = GoogleFormSubmissionMapper.ResolveFileTarget("EDUCATION", files[0]);
        var bformTarget = GoogleFormSubmissionMapper.ResolveFileTarget("EDUCATION", files[1]);
        var photoTarget = GoogleFormSubmissionMapper.ResolveFileTarget("EDUCATION", files[2]);

        Assert.Equal(DocumentSlotOwnerScope.Applicant, membershipTarget!.OwnerScope);
        Assert.Equal(DocumentType.MembershipCard, membershipTarget.DocumentType);

        Assert.Equal("EDUCATION.STUDENT_BFORM_OR_CNIC", bformTarget!.SlotKey);
        Assert.Equal(DocumentType.FormB, bformTarget.DocumentType);

        Assert.Equal("EDUCATION.STUDENT_PHOTO_AND_RESULT", photoTarget!.SlotKey);
        Assert.Equal(DocumentType.PassportPhoto, photoTarget.DocumentType);
    }

    // ---------- HEALTH ----------

    private static GoogleFormSubmissionRequest HealthRequest() => Request("Health Assistance",
    [
        A("Full Name", "Health Applicant"),
        A("CNIC Number", "42101-3704613-7"),
        A("Age", "60"),
        A("Description(Please explain your health issue in details)", "need surgery (heart bypass) urgently"),
    ]);

    [Fact]
    public void Map_Health_MapsAgeAndPurpose()
    {
        var result = GoogleFormSubmissionMapper.Map(HealthRequest());

        Assert.Equal(60, result.Health!.ApplicantAge);
        Assert.Equal("need surgery (heart bypass) urgently", result.Purpose);
    }

    [Fact]
    public void Map_FreeTextAnswer_ContainingParenthetical_IsNeverStripped()
    {
        // B: GetString (free text) must NOT run StripOptionParenthetical — only GetOption
        // (radio/checkbox) does. A description or address legitimately contains " (".
        var request = Request("Health Assistance",
        [
            A("Full Name", "Health Applicant"),
            A("CNIC Number", "42101-3704613-7"),
            A("Current Residential Address", "House 12 (near the mosque), Main Road"),
            A("Description(Please explain your health issue in details)", "Chest pain (possible cardiac) since last week"),
        ]);

        var result = GoogleFormSubmissionMapper.Map(request);

        Assert.Equal("House 12 (near the mosque), Main Road", result.DeclaredResidentialAddress);
        Assert.Equal("Chest pain (possible cardiac) since last week", result.Purpose);
    }

    // ---------- OTHER ----------

    private static GoogleFormSubmissionRequest OtherRequest() => Request("General Assistance Form",
    [
        A("Name", "Other Applicant"),
        A("CNIC NO", "42101-3704613-7"),
        A("Description( Please explain your issue in detail )", "General help needed"),
    ]);

    [Fact]
    public void Map_Other_NoDetailsTable()
    {
        var result = GoogleFormSubmissionMapper.Map(OtherRequest());

        Assert.Null(result.Housing);
        Assert.Null(result.Marriage);
        Assert.Null(result.BusinessLoan);
        Assert.Null(result.Education);
        Assert.Null(result.Health);
        Assert.Equal("General help needed", result.Purpose);
    }

    // ---------- unmapped/unparseable -> notes ----------

    [Fact]
    public void Map_UnmappedAnswer_BecomesNote()
    {
        var request = Request("General Assistance Form",
        [
            A("Name", "Other Applicant"),
            A("CNIC NO", "42101-3704613-7"),
            A("Some Totally Unknown Question", "some value"),
        ]);

        var result = GoogleFormSubmissionMapper.Map(request);

        Assert.Contains(result.Notes, n => n.Contains("Unmapped answer") && n.Contains("Some Totally Unknown Question"));
    }

    [Fact]
    public void Map_UnparseableNonApplicantCnic_DegradesToNullWithNote()
    {
        var request = Request("Marriage Assistance",
        [
            A("Applicant's (Guardian's) Name", "Guardian One"),
            A("CNIC Number", "42101-3704613-7"),
            A("Bride's Name", "Bride One"),
            A("Bride's CNIC Number", "not-a-cnic"),
        ]);

        var result = GoogleFormSubmissionMapper.Map(request);

        Assert.Null(result.Marriage!.BrideCnic);
        Assert.Contains(result.Notes, n => n.Contains("Bride's CNIC Number"));
    }

    [Fact]
    public void Map_UnsupportedFileMimeType_BecomesNoteWithDriveLink()
    {
        var files = new[] { new GoogleFormFileManifestEntry("Attach: photocopy of CNIC", null, "drive-x", "doc.docx", "application/msword") };
        var request = Request("Housing Assistance",
        [
            A("CNIC Number", "42101-3704613-7"), A("Full Name", "X"),
        ], files);

        var result = GoogleFormSubmissionMapper.Map(request);

        Assert.Contains(result.Notes, n => n.Contains("Unsupported file type") && n.Contains("drive.google.com/open?id=drive-x"));
    }

    // ---------- E: over-length answers never reach the DB as a 22001 ----------

    [Fact]
    public void Map_OverLengthMembershipNumber_BecomesNullWithFullValueInNote()
    {
        // Cap is PakistaniFormats.JamaatMembershipMaxLength (10) — never truncated (that would
        // corrupt the identifier), always null + a note carrying the original value.
        var tooLong = "M12345678901234567890";
        var request = Request("Housing Assistance",
        [
            A("Bhavnagar Jamaat Membership Number", tooLong),
            A("CNIC Number", "42101-3704613-7"),
            A("Full Name", "X"),
        ]);

        var result = GoogleFormSubmissionMapper.Map(request);

        Assert.Null(result.Applicant.MembershipNumber);
        Assert.Contains(result.Notes, n => n.Contains("Bhavnagar Jamaat Membership Number") && n.Contains(tooLong));
    }

    [Fact]
    public void Map_OverLengthSurname_BecomesNullWithFullValueInNote()
    {
        var tooLong = new string('S', 150); // cap is 100
        var request = Request("Housing Assistance",
        [
            A("Surname / Identification", tooLong),
            A("CNIC Number", "42101-3704613-7"),
            A("Full Name", "X"),
        ]);

        var result = GoogleFormSubmissionMapper.Map(request);

        Assert.Null(result.Applicant.Surname);
        Assert.Contains(result.Notes, n => n.Contains("Surname / Identification") && n.Contains(tooLong));
    }
}
