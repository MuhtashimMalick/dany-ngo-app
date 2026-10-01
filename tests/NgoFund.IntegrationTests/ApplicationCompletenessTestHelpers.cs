using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.ApplicationCategories;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.FundCategories;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>Shared scaffolding for the completeness-gate test classes (split across several small
/// classes, each with its own <see cref="AuthApiFactory"/>/rate-limiter partition — see
/// <see cref="StatusTransitionInvariantTests"/>'s doc comment for why one login-per-test class must
/// stay under the 10-logins/minute/IP budget).</summary>
internal static class ApplicationCompletenessTestHelpers
{
    public static async Task<ApplicantDto> CreateApplicantAsync(HttpClient client, string cnic)
    {
        var response = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "Completeness Test Applicant", null, cnic, "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        return await ReadOrFailAsync<ApplicantDto>(response, HttpStatusCode.Created);
    }

    public static async Task<(Dictionary<string, Guid> Categories, Guid GeneralFundId)> LoadSeedIdsAsync(HttpClient client)
    {
        var categories = (await client.GetFromJsonAsync<List<ApplicationCategoryDto>>("/api/application-categories"))!;
        var funds = (await client.GetFromJsonAsync<List<FundCategoryDto>>("/api/fund-categories"))!;
        return (categories.ToDictionary(c => c.Code, c => c.Id), funds.Single(f => f.Code == "GENERAL").Id);
    }

    /// <summary>SHAADI and HOUSE_RENT are ZakatOnly under the v1.5 FundEligibility amendment (they
    /// used to be dual-eligible) — completeness-gate tests for those two categories need this
    /// instead of <see cref="LoadSeedIdsAsync"/>'s GeneralFundId. ROZGAR stays on General.</summary>
    public static async Task<Guid> LoadZakatFundIdAsync(HttpClient client)
    {
        var funds = (await client.GetFromJsonAsync<List<FundCategoryDto>>("/api/fund-categories"))!;
        return funds.Single(f => f.Code == "ZAKAT").Id;
    }

    public static async Task<ApplicationDto> CreateBareApplicationAsync(HttpClient client, Guid applicantId, Guid categoryId, Guid fundId) =>
        await ReadOrFailAsync<ApplicationDto>(
            await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
                applicantId, categoryId, fundId, 10000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test")),
            HttpStatusCode.Created);

    public static readonly UpsertMarriageApplicationDetailsRequest FullyValidMarriageDetails = new(
        GuardianRelationshipToBride: "Father", BrideName: "Bride Name", BrideFatherName: "Bride Father",
        BrideFamilyName: null, BrideCnic: "40001-1111111-1", BrideMaritalStatus: "First Marriage",
        BridePreviousHusbandName: null, BrideJamaat: "Central Jamaat", BridePriorTrustAssistance: null,
        GroomName: "Groom Name", GroomFatherName: "Groom Father", GroomGrandfatherName: null,
        GroomJamaat: "Central Jamaat", GroomMaritalStatus: "First Marriage", GroomPreviousWifeName: null,
        GroomAddress: "Groom Address", GroomMobile: "0300-1111111", GroomBusinessAddress: null,
        NikahDate: DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(2), RukhsatiDate: null);

    public static readonly UpsertHousingApplicationDetailsRequest FullyValidHousingDetails = new(
        ApplicantAge: 45, CurrentHouseValue: null, MonthlyRent: 15000m, AdvancePaid: 45000m,
        YearsAtCurrentAddress: 3, PreviousResidentialAddress: null, ReceivedAssistanceBefore: false,
        PreviousAssistanceDetails: null, ReceivesMarriageAssistance: false, ReceivesEducationAssistance: false,
        ReceivesMedicalAssistance: false, ReceivesWidowAssistance: false);

    public static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid applicationId, string documentType, string slotKey)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(TestFiles.MinimalJpegBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "doc.jpg");
        return await client.PostAsync($"/api/documents?documentType={documentType}&slotKey={slotKey}&applicationId={applicationId}", form);
    }

    // v1.4 (B2/B6): SHAADI.APPLICANT_CNIC/APPLICANT_MEMBERSHIP_CARD and HOUSE_RENT.APPLICANT_CNIC/
    // MEMBERSHIP_CARD are NOT uploaded here anymore — they're Applicant-scoped now, satisfied by
    // the CNIC/membership-card documents every test applicant already has from
    // CreateApplicantAsync. An application-owned upload against either slot key would now be
    // rejected as an owner-scope mismatch by DocumentService.EnsureSlotMatchesAsync.

    public static async Task UploadAllRequiredShaadiDocumentsAsync(HttpClient client, Guid applicationId)
    {
        await UploadAsync(client, applicationId, "CnicFront", "SHAADI.BRIDE_CNIC_OR_BFORM");
        await UploadAsync(client, applicationId, "CnicFront", "SHAADI.GROOM_CNIC");
        await UploadAsync(client, applicationId, "MembershipCard", "SHAADI.BRIDE_MEMBERSHIP_CARD");
    }

    public static async Task UploadAllRequiredHouseRentDocumentsAsync(HttpClient client, Guid applicationId)
    {
        await UploadAsync(client, applicationId, "UtilityBill", "HOUSE_RENT.UTILITY_BILLS");
        await UploadAsync(client, applicationId, "UtilityBill", "HOUSE_RENT.UTILITY_BILLS");
        await UploadAsync(client, applicationId, "UtilityBill", "HOUSE_RENT.UTILITY_BILLS");
    }

    // Feedback round 3, item I: HEALTH/EDUCATION/OTHER gained a real completeness manifest (the
    // Google Form intake integration's A9) — a bare CreateBareApplicationAsync application no
    // longer clears the Approved gate for these three. Any fixture elsewhere in the suite that
    // needs an Approved HEALTH/EDUCATION/OTHER application (not just the completeness-gate tests
    // themselves) calls one of these three helpers first, rather than re-deriving the manifest
    // per test class. Applicant-scoped CNIC/membership-card slots are already satisfied by
    // CreateApplicantAsync's uploads; these only add what's application-owned.

    public static async Task CompleteHealthDetailsAsync(HttpClient client, Guid applicationId)
    {
        await client.PutAsJsonAsync($"/api/applications/{applicationId}/details/health",
            new UpsertHealthApplicationDetailsRequest(ApplicantAge: 40));
        await UploadAsync(client, applicationId, "MedicalReport", "HEALTH.MEDICAL_DOCUMENTS");
    }

    public static async Task CompleteEducationDetailsAsync(HttpClient client, Guid applicationId)
    {
        await client.PutAsJsonAsync($"/api/applications/{applicationId}/details/education",
            new UpsertEducationApplicationDetailsRequest(
                CensusNumber: null, StudentName: "Test Student", WmoId: null, StudentMobile: null,
                CurrentClass: "5th", PreviousClass: null, LastExamTotalMarks: null, LastExamMarksObtained: null,
                PreviousYearAttendancePercent: null, TotalAttendanceDays: null, TotalAcademicDays: null,
                FatherJamaat: null, MotherName: "Test Mother", MotherFatherName: null, MotherCaste: null,
                MotherJamaat: null, MotherMembershipNumber: null, MotherCnic: null, MotherMonthlyIncome: null,
                MotherMobile: null, MotherProfession: null));
        await UploadAsync(client, applicationId, "FormB", "EDUCATION.STUDENT_BFORM_OR_CNIC");
        await UploadAsync(client, applicationId, "PassportPhoto", "EDUCATION.STUDENT_PHOTO_AND_RESULT");
    }

    /// <summary>OTHER has no details table — RequestedAmount (set by CreateBareApplicationAsync)
    /// and the applicant-scoped CNIC/membership-card slots (from CreateApplicantAsync) are already
    /// satisfied; only the application-owned SUPPORTING_DOCUMENTS slot needs an upload.</summary>
    public static async Task CompleteOtherDocumentsAsync(HttpClient client, Guid applicationId) =>
        await UploadAsync(client, applicationId, "SupportingDocument", "OTHER.SUPPORTING_DOCUMENTS");
}
