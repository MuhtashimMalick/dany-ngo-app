using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.ApplicationCategories;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Documents;
using NgoFund.Contracts.FundCategories;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the category-specific application-details endpoints (housing/marriage/business-loan
/// upsert round-trips, wrong-category rejection) and the ROZGAR guarantor-count gate on Approved,
/// all through the real HTTP API against a real, freshly migrated Postgres — same pattern as
/// <see cref="ApplicationWorkflowTests"/>.
/// </summary>
public class CategoryApplicationDetailsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = await factory.CreateSeededClientAsync();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@ngofund.local", "ChangeMe123!"));
        var auth = (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return client;
    }

    private static async Task<T> ReadOrFailAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {expected}, got {response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, JsonOptions)!;
    }

    private static async Task<ApplicantDto> CreateApplicantAsync(HttpClient client, string cnic)
    {
        var response = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "Test Applicant", null, cnic, "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        return await ReadOrFailAsync<ApplicantDto>(response, HttpStatusCode.Created);
    }

    private static async Task<(string Code, Guid Id)[]> LoadCategoriesAsync(HttpClient client)
    {
        var categories = (await client.GetFromJsonAsync<List<ApplicationCategoryDto>>("/api/application-categories"))!;
        return categories.Select(c => (c.Code, c.Id)).ToArray();
    }

    private static async Task<Guid> LoadGeneralFundIdAsync(HttpClient client)
    {
        var funds = (await client.GetFromJsonAsync<List<FundCategoryDto>>("/api/fund-categories"))!;
        return funds.Single(f => f.Code == "GENERAL").Id;
    }

    // HOUSE_RENT and SHAADI are ZakatOnly under the v1.5 FundEligibility amendment (they used to
    // be dual-eligible), so the housing/marriage-details tests below need a Zakat fund, not
    // General — these tests are about the category-details endpoints, not the Zakat rule itself.
    private static async Task<Guid> LoadZakatFundIdAsync(HttpClient client)
    {
        var funds = (await client.GetFromJsonAsync<List<FundCategoryDto>>("/api/fund-categories"))!;
        return funds.Single(f => f.Code == "ZAKAT").Id;
    }

    private static async Task<ApplicationDto> CreateApplicationAsync(HttpClient client, Guid applicantId, Guid categoryId, Guid fundId, decimal amount = 10000m) =>
        await ReadOrFailAsync<ApplicationDto>(
            await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
                applicantId, categoryId, fundId, amount, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test")),
            HttpStatusCode.Created);

    [Fact]
    public async Task HousingDetails_UpsertThenGet_RoundTrips()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "40001-4000001-1");
        var categories = await LoadCategoriesAsync(client);
        var (houseRentCode, houseRentId) = categories.Single(c => c.Code == "HOUSE_RENT");
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateApplicationAsync(client, applicant.Id, houseRentId, zakatFundId);

        var request = new UpsertHousingApplicationDetailsRequest(
            ApplicantAge: 45, CurrentHouseValue: 2000000m, MonthlyRent: 15000m, AdvancePaid: 45000m,
            YearsAtCurrentAddress: 3, PreviousResidentialAddress: "Old address", ReceivedAssistanceBefore: true,
            PreviousAssistanceDetails: "Received once in 2023", ReceivesMarriageAssistance: false,
            ReceivesEducationAssistance: true, ReceivesMedicalAssistance: false, ReceivesWidowAssistance: false);

        var putResponse = await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/housing", request);
        var upserted = await ReadOrFailAsync<HousingApplicationDetailsDto>(putResponse, HttpStatusCode.OK);
        Assert.Equal(15000m, upserted.MonthlyRent);
        Assert.True(upserted.ReceivesEducationAssistance);

        var getResponse = await client.GetAsync($"/api/applications/{application.Id}/details/housing");
        var fetched = await ReadOrFailAsync<HousingApplicationDetailsDto>(getResponse, HttpStatusCode.OK);
        Assert.Equal(45, fetched.ApplicantAge);
        Assert.Equal("Old address", fetched.PreviousResidentialAddress);
    }

    [Fact]
    public async Task MarriageDetails_UpsertThenGet_RoundTrips()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "40002-4000002-2");
        var categories = await LoadCategoriesAsync(client);
        var shaadiId = categories.Single(c => c.Code == "SHAADI").Id;
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateApplicationAsync(client, applicant.Id, shaadiId, zakatFundId);

        var request = new UpsertMarriageApplicationDetailsRequest(
            GuardianRelationshipToBride: "Father", BrideName: "Bride Name", BrideFatherName: "Bride Father",
            BrideFamilyName: "Family", BrideCnic: "40002-1111111-1", BrideMaritalStatus: "First Marriage",
            BridePreviousHusbandName: null, BrideJamaat: "Central Jamaat", BridePriorTrustAssistance: null,
            GroomName: "Groom Name", GroomFatherName: "Groom Father", GroomGrandfatherName: "Groom Grandfather",
            GroomJamaat: "Central Jamaat", GroomMaritalStatus: "Divorced", GroomPreviousWifeName: "Prev Wife",
            GroomAddress: "Groom address", GroomMobile: "0300-1111111", GroomBusinessAddress: null,
            NikahDate: new DateOnly(2026, 6, 1), RukhsatiDate: new DateOnly(2026, 6, 10));

        var putResponse = await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/marriage", request);
        var upserted = await ReadOrFailAsync<MarriageApplicationDetailsDto>(putResponse, HttpStatusCode.OK);

        // "First Marriage" and "Divorced" round-trip through MaritalStatusMapper -> the shared enum.
        Assert.Equal("Single", upserted.BrideMaritalStatus);
        Assert.Equal("Divorced", upserted.GroomMaritalStatus);
        Assert.Equal(new DateOnly(2026, 6, 10), upserted.RukhsatiDate);
    }

    [Fact]
    public async Task MarriageDetails_RukhsatiBeforeNikah_IsRejected()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "40003-4000003-3");
        var categories = await LoadCategoriesAsync(client);
        var shaadiId = categories.Single(c => c.Code == "SHAADI").Id;
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateApplicationAsync(client, applicant.Id, shaadiId, zakatFundId);

        var request = new UpsertMarriageApplicationDetailsRequest(
            null, "Bride", null, null, null, null, null, null, null,
            "Groom", null, null, null, null, null, null, null, null,
            NikahDate: new DateOnly(2026, 6, 10), RukhsatiDate: new DateOnly(2026, 6, 1));

        var response = await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/marriage", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BusinessLoanDetails_LargeCapitalMismatch_ReturnsWarningNotError()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "40004-4000004-4");
        var categories = await LoadCategoriesAsync(client);
        var rozgarId = categories.Single(c => c.Code == "ROZGAR").Id;
        var generalFundId = await LoadGeneralFundIdAsync(client);
        var application = await CreateApplicationAsync(client, applicant.Id, rozgarId, generalFundId, amount: 50000m);

        // All other required fields filled in so this test isolates the mismatch-warning behavior
        // from the completeness gate (layer 2, ApplicationDetailsService) added alongside it.
        var request = new UpsertBusinessLoanApplicationDetailsRequest(
            PaperFormNumber: null, BusinessPhone: null, Education: null, Skill: "Retail", Experience: "3 years",
            OtherIncomeSources: null, TotalMonthlyExpenses: 15000m,
            ProposedBusinessDescription: "Small grocery shop", ProposedBusinessLocation: "Main Bazaar",
            CapitalRequired: 300000m, CapitalAlreadyAvailable: 30000m, // gap = 270000, requested = 50000 -> mismatch
            HasPriorBusinessExperience: false, PriorBusinessDetails: null,
            EmergencyContactName: "Emergency Contact", EmergencyContactCnic: null, EmergencyContactPhone: "0300-0000000");

        var response = await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/business-loan", request);
        var upserted = await ReadOrFailAsync<BusinessLoanApplicationDetailsDto>(response, HttpStatusCode.OK);

        Assert.NotNull(upserted.AmountMismatchWarning);
    }

    [Fact]
    public async Task HousingDetails_PostedAgainstWrongCategory_IsRejected()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "40005-4000005-5");
        var categories = await LoadCategoriesAsync(client);
        var shaadiId = categories.Single(c => c.Code == "SHAADI").Id; // not HOUSE_RENT
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateApplicationAsync(client, applicant.Id, shaadiId, zakatFundId);

        var request = new UpsertHousingApplicationDetailsRequest(
            null, null, null, null, null, null, false, null, false, false, false, false);

        var response = await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/housing", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task RozgarApplication_CannotReachApproved_WithFewerThanTwoGuarantors_ButCanWithExactlyTwo()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "40006-4000006-6");
        var categories = await LoadCategoriesAsync(client);
        var rozgarId = categories.Single(c => c.Code == "ROZGAR").Id;
        var generalFundId = await LoadGeneralFundIdAsync(client);
        var application = await RozgarCompletenessTestHelpers.CreateRozgarApplicationAsync(client, applicant.Id, rozgarId, generalFundId);

        // Satisfy every other completeness requirement up front, so this test proves the
        // guarantor-count gate specifically, in isolation from the completeness gate added
        // alongside it (both run on the Approved transition — see FundApplicationService.ChangeStatusAsync).
        await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/business-loan", RozgarCompletenessTestHelpers.FullyValidBusinessLoanDetails);
        await RozgarCompletenessTestHelpers.UploadAllRequiredApplicationDocumentsAsync(client, application.Id);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));

        // Zero guarantors -> Approved must be rejected.
        var rejectedAtZero = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejectedAtZero.StatusCode);

        // One guarantor (with their required documents) -> still rejected: still fewer than the required two.
        var guarantorsAtOne = await ReadOrFailAsync<List<ApplicationGuarantorDto>>(
            await client.PutAsJsonAsync($"/api/applications/{application.Id}/guarantors", new ReplaceApplicationGuarantorsRequest(
            [
                new GuarantorEntry(null, 1, "MEM-G001", "Guarantor One", null, null, null, "11111-1111111-1", null, null, null, null, null, null, null),
            ])),
            HttpStatusCode.OK);
        await RozgarCompletenessTestHelpers.UploadRequiredGuarantorDocumentsAsync(client, guarantorsAtOne.Single().Id);
        var rejectedAtOne = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejectedAtOne.StatusCode);

        // Two guarantors, both with their required documents -> Approved succeeds.
        var guarantorsAtTwo = await ReadOrFailAsync<List<ApplicationGuarantorDto>>(
            await client.PutAsJsonAsync($"/api/applications/{application.Id}/guarantors", new ReplaceApplicationGuarantorsRequest(
            [
                new GuarantorEntry(guarantorsAtOne.Single().Id, 1, "MEM-G001", "Guarantor One", null, null, null, "11111-1111111-1", null, null, null, null, null, null, null),
                new GuarantorEntry(null, 2, "MEM-G002", "Guarantor Two", null, null, null, "22222-2222222-2", null, null, null, null, null, null, null),
            ])),
            HttpStatusCode.OK);
        await RozgarCompletenessTestHelpers.UploadRequiredGuarantorDocumentsAsync(client, guarantorsAtTwo.Single(g => g.SequenceNumber == 2).Id);

        var approvedAtTwo = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null));
        Assert.Equal(HttpStatusCode.NoContent, approvedAtTwo.StatusCode);

        var afterApproval = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
        Assert.Equal("Approved", afterApproval.Status);
        Assert.Equal(2, afterApproval.GuarantorCount);
    }

    [Fact]
    public async Task GuarantorDocument_IsReturnedByGuarantorEndpoint_ButNotByApplicationEndpoint()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "40007-4000007-7");
        var categories = await LoadCategoriesAsync(client);
        var rozgarId = categories.Single(c => c.Code == "ROZGAR").Id;
        var generalFundId = await LoadGeneralFundIdAsync(client);
        var application = await CreateApplicationAsync(client, applicant.Id, rozgarId, generalFundId);

        var guarantors = await ReadOrFailAsync<List<ApplicationGuarantorDto>>(
            await client.PutAsJsonAsync($"/api/applications/{application.Id}/guarantors", new ReplaceApplicationGuarantorsRequest(
            [
                new GuarantorEntry(null, 1, "MEM-G001", "Guarantor One", null, null, null, "11111-1111111-1", null, null, null, null, null, null, null),
            ])),
            HttpStatusCode.OK);
        var guarantorId = guarantors.Single().Id;

        var fileBytes = TestFiles.MinimalJpegBytes;
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "cnic.jpg");

        var uploadResponse = await client.PostAsync(
            $"/api/documents?documentType=CnicFront&applicationGuarantorId={guarantorId}", form);
        var document = await ReadOrFailAsync<DocumentDto>(uploadResponse, HttpStatusCode.OK);

        var forGuarantor = (await client.GetFromJsonAsync<List<DocumentDto>>($"/api/documents/by-guarantor/{guarantorId}"))!;
        Assert.Single(forGuarantor);
        Assert.Equal(document.Id, forGuarantor[0].Id);

        var forApplication = (await client.GetFromJsonAsync<List<DocumentDto>>($"/api/documents/by-application/{application.Id}"))!;
        Assert.Empty(forApplication);
    }

    [Fact]
    public async Task ReplaceGuarantors_EditingAnExistingGuarantorById_PreservesItsDocumentLink()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "40008-4000008-8");
        var categories = await LoadCategoriesAsync(client);
        var rozgarId = categories.Single(c => c.Code == "ROZGAR").Id;
        var generalFundId = await LoadGeneralFundIdAsync(client);
        var application = await CreateApplicationAsync(client, applicant.Id, rozgarId, generalFundId);

        var created = await ReadOrFailAsync<List<ApplicationGuarantorDto>>(
            await client.PutAsJsonAsync($"/api/applications/{application.Id}/guarantors", new ReplaceApplicationGuarantorsRequest(
            [
                new GuarantorEntry(null, 1, "MEM-G001", "Guarantor One", null, null, null, "11111-1111111-1", null, null, null, "0300-1111111", null, null, null),
            ])),
            HttpStatusCode.OK);
        var guarantorId = created.Single().Id;

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(TestFiles.MinimalJpegBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "cnic.jpg");
        var document = await ReadOrFailAsync<DocumentDto>(
            await client.PostAsync($"/api/documents?documentType=CnicFront&applicationGuarantorId={guarantorId}", form),
            HttpStatusCode.OK);

        // Edit the same guarantor (echoing its Id) with a changed phone number — this must update
        // the row in place, not recreate it, so the document uploaded above stays reachable.
        var edited = await ReadOrFailAsync<List<ApplicationGuarantorDto>>(
            await client.PutAsJsonAsync($"/api/applications/{application.Id}/guarantors", new ReplaceApplicationGuarantorsRequest(
            [
                new GuarantorEntry(guarantorId, 1, "MEM-G001", "Guarantor One", null, null, null, "11111-1111111-1", null, null, null, "0300-9999999", null, null, null),
            ])),
            HttpStatusCode.OK);

        Assert.Equal(guarantorId, edited.Single().Id);
        Assert.Equal("0300-9999999", edited.Single().PhoneHome);

        var forGuarantor = (await client.GetFromJsonAsync<List<DocumentDto>>($"/api/documents/by-guarantor/{guarantorId}"))!;
        Assert.Single(forGuarantor);
        Assert.Equal(document.Id, forGuarantor[0].Id);
    }

    [Fact]
    public async Task ReplaceGuarantors_OmittingAPreviouslyExistingId_SoftDeletesThatGuarantor()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "40009-4000009-9");
        var categories = await LoadCategoriesAsync(client);
        var rozgarId = categories.Single(c => c.Code == "ROZGAR").Id;
        var generalFundId = await LoadGeneralFundIdAsync(client);
        var application = await CreateApplicationAsync(client, applicant.Id, rozgarId, generalFundId);

        var created = await ReadOrFailAsync<List<ApplicationGuarantorDto>>(
            await client.PutAsJsonAsync($"/api/applications/{application.Id}/guarantors", new ReplaceApplicationGuarantorsRequest(
            [
                new GuarantorEntry(null, 1, "MEM-G001", "Guarantor One", null, null, null, "11111-1111111-1", null, null, null, null, null, null, null),
                new GuarantorEntry(null, 2, "MEM-G002", "Guarantor Two", null, null, null, "22222-2222222-2", null, null, null, null, null, null, null),
            ])),
            HttpStatusCode.OK);
        var keptId = created.Single(g => g.SequenceNumber == 1).Id;

        // Send only guarantor 1 back (renumbered to seq 1, already was) -> guarantor 2 must be
        // soft-deleted, and the surviving row must keep its original Id.
        var afterRemoval = await ReadOrFailAsync<List<ApplicationGuarantorDto>>(
            await client.PutAsJsonAsync($"/api/applications/{application.Id}/guarantors", new ReplaceApplicationGuarantorsRequest(
            [
                new GuarantorEntry(keptId, 1, "MEM-G001", "Guarantor One", null, null, null, "11111-1111111-1", null, null, null, null, null, null, null),
            ])),
            HttpStatusCode.OK);

        var single = Assert.Single(afterRemoval);
        Assert.Equal(keptId, single.Id);

        var stillThere = await ReadOrFailAsync<List<ApplicationGuarantorDto>>(
            await client.GetAsync($"/api/applications/{application.Id}/guarantors"), HttpStatusCode.OK);
        Assert.Single(stillThere);
    }
}
