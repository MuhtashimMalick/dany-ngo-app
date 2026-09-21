using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NgoFund.Contracts.Applications;
using static NgoFund.IntegrationTests.ApplicationCompletenessTestHelpers;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the completeness gate end to end through the real HTTP API: the client's bug report was
/// that a SHAADI application could reach Approved with zero category-specific details and zero
/// documents on file. Covers the gate itself (missing fields, missing documents, both), the two
/// optional slots that must never block (SHAADI.WEDDING_CARD, HOUSE_RENT.RENT_RECEIPTS), and
/// ROZGAR's two distinct rejection points (the guarantor-row validator vs. the completeness gate).
/// See <see cref="ApplicationCompletenessRegressionTests"/> and <see cref="DocumentSlotValidationTests"/>
/// for the rest — split into several classes/fixtures purely to stay under the shared
/// 10-logins/minute/IP rate-limiter budget (see <see cref="StatusTransitionInvariantTests"/>).
/// </summary>
public class ApplicationCompletenessTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Shaadi_NothingFilled_CannotReachApproved_AndReportsBothMissingFieldsAndDocuments()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50101-1111111-1");
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["SHAADI"], zakatFundId);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));

        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), JsonOptions);
        var missingFields = problem.GetProperty("missingFields").EnumerateArray().Select(e => e.GetString()).ToList();
        var missingDocuments = problem.GetProperty("missingDocuments").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.NotEmpty(missingFields);
        Assert.NotEmpty(missingDocuments);
    }

    [Fact]
    public async Task Shaadi_DetailsFilledButNoDocuments_StillRejected_OnlyDocumentsListed()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50101-1111111-2");
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["SHAADI"], zakatFundId);

        await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/marriage", FullyValidMarriageDetails);
        await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            categories["SHAADI"], zakatFundId, application.RequestedAmount, null, "Normal", "Test",
            DeclarationAcceptedAt: DateTimeOffset.UtcNow, TermsAcceptedAt: DateTimeOffset.UtcNow));

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), JsonOptions);
        Assert.Empty(problem.GetProperty("missingFields").EnumerateArray());
        Assert.NotEmpty(problem.GetProperty("missingDocuments").EnumerateArray());
    }

    // --- Item 5 (2026-09 feedback round 3): groom's address/mobile/CNIC document eased, bride's ---
    // --- side (fields and documents) deliberately left untouched. Locks in the asymmetry. ---

    [Fact]
    public async Task Shaadi_GroomAddressMobileAndCnicDocumentMissing_ApprovedSucceeds()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50101-1111111-8");
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["SHAADI"], zakatFundId);

        await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/marriage",
            FullyValidMarriageDetails with { GroomAddress = null, GroomMobile = null });
        await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            categories["SHAADI"], zakatFundId, application.RequestedAmount, null, "Normal", "Test",
            DeclarationAcceptedAt: DateTimeOffset.UtcNow, TermsAcceptedAt: DateTimeOffset.UtcNow));
        // Deliberately skip SHAADI.GROOM_CNIC — only bride's CNIC/B-form and bride's membership card.
        await UploadAsync(client, application.Id, "CnicFront", "SHAADI.BRIDE_CNIC_OR_BFORM");
        await UploadAsync(client, application.Id, "MembershipCard", "SHAADI.BRIDE_MEMBERSHIP_CARD");

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Shaadi_BrideFieldMissing_StillBlocksApproval()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50101-1111111-9");
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["SHAADI"], zakatFundId);

        // Every groom field/document present; only the bride's father's name is missing. (BrideName
        // itself is required at the PUT-details validator level, so BrideFatherName — nullable in
        // the request DTO but still checked by MissingMarriageFields — is what isolates the
        // Approve-transition completeness gate this test targets.)
        await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/marriage",
            FullyValidMarriageDetails with { BrideFatherName = null });
        await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            categories["SHAADI"], zakatFundId, application.RequestedAmount, null, "Normal", "Test",
            DeclarationAcceptedAt: DateTimeOffset.UtcNow, TermsAcceptedAt: DateTimeOffset.UtcNow));
        await UploadAllRequiredShaadiDocumentsAsync(client, application.Id);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), JsonOptions);
        Assert.Contains(
            problem.GetProperty("missingFields").EnumerateArray().Select(e => e.GetString()),
            m => m!.Contains("Bride"));
    }

    [Fact]
    public async Task Shaadi_BrideCnicDocumentMissing_StillBlocksApproval()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50101-1111112-0");
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["SHAADI"], zakatFundId);

        await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/marriage", FullyValidMarriageDetails);
        await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            categories["SHAADI"], zakatFundId, application.RequestedAmount, null, "Normal", "Test",
            DeclarationAcceptedAt: DateTimeOffset.UtcNow, TermsAcceptedAt: DateTimeOffset.UtcNow));
        // Groom's CNIC document uploaded, bride's CNIC/B-form deliberately skipped (required).
        await UploadAsync(client, application.Id, "CnicFront", "SHAADI.GROOM_CNIC");
        await UploadAsync(client, application.Id, "MembershipCard", "SHAADI.BRIDE_MEMBERSHIP_CARD");

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), JsonOptions);
        Assert.Contains(
            problem.GetProperty("missingDocuments").EnumerateArray().Select(e => e.GetString()),
            m => m!.Contains("Bride"));
    }

    [Fact]
    public async Task Shaadi_EveryRequiredSlotUploaded_ApprovedSucceeds_EvenWithoutOptionalWeddingCard()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50101-1111111-3");
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["SHAADI"], zakatFundId);

        await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/marriage", FullyValidMarriageDetails);
        await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            categories["SHAADI"], zakatFundId, application.RequestedAmount, null, "Normal", "Test",
            DeclarationAcceptedAt: DateTimeOffset.UtcNow, TermsAcceptedAt: DateTimeOffset.UtcNow));
        await UploadAllRequiredShaadiDocumentsAsync(client, application.Id);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
        Assert.Equal("Approved", after.Status);
    }

    [Fact]
    public async Task HouseRent_EveryRequiredSlotUploaded_ApprovedSucceeds_EvenWithoutOptionalRentReceipts()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50101-1111111-4");
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["HOUSE_RENT"], zakatFundId);

        await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/housing", FullyValidHousingDetails);
        await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            categories["HOUSE_RENT"], zakatFundId, application.RequestedAmount, null, "Normal", "Test",
            DeclaredMonthlyIncome: 30000m, DeclaredHouseholdSize: 5, DeclaredResidentialAddress: "123 Main St",
            DeclaredHouseStatus: "Rented",
            DeclarationAcceptedAt: DateTimeOffset.UtcNow, TermsAcceptedAt: DateTimeOffset.UtcNow));
        await UploadAllRequiredHouseRentDocumentsAsync(client, application.Id);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // --- ROZGAR: field-validator gate (guarantor row itself) vs. completeness gate (documents) ---

    [Fact]
    public async Task Rozgar_GuarantorsWithNamesButNoCnicOrMembershipNumber_RejectedAtSaveTime_NotAtApproval()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50101-1111111-5");
        var (categories, generalFundId) = await LoadSeedIdsAsync(client);
        var application = await RozgarCompletenessTestHelpers.CreateRozgarApplicationAsync(client, applicant.Id, categories["ROZGAR"], generalFundId);

        var response = await client.PutAsJsonAsync($"/api/applications/{application.Id}/guarantors", new ReplaceApplicationGuarantorsRequest(
        [
            new GuarantorEntry(null, 1, null, "Guarantor One", null, null, null, null, null, null, null, null, null, null, null),
            new GuarantorEntry(null, 2, null, "Guarantor Two", null, null, null, null, null, null, null, null, null, null, null),
        ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rozgar_TwoValidGuarantors_ButNoGuarantorDocuments_RejectedAtApproval()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50101-1111111-6");
        var (categories, generalFundId) = await LoadSeedIdsAsync(client);
        var application = await RozgarCompletenessTestHelpers.CreateRozgarApplicationAsync(client, applicant.Id, categories["ROZGAR"], generalFundId);

        await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/business-loan", RozgarCompletenessTestHelpers.FullyValidBusinessLoanDetails);
        await RozgarCompletenessTestHelpers.UploadAllRequiredApplicationDocumentsAsync(client, application.Id);
        await client.PutAsJsonAsync($"/api/applications/{application.Id}/guarantors", new ReplaceApplicationGuarantorsRequest(
        [
            new GuarantorEntry(null, 1, "MEM-1", "Guarantor One", null, null, null, "11111-1111111-1",
                ResidentialAddress: "123 Test Street", BusinessAddress: "456 Business Road", BusinessNature: null,
                PhoneHome: "021-1111111", PhoneOffice: "021-1111112", PhoneMobile: "0300-1111111", DeclarationAcceptedAt: null),
            new GuarantorEntry(null, 2, "MEM-2", "Guarantor Two", null, null, null, "22222-2222222-2",
                ResidentialAddress: "124 Test Street", BusinessAddress: "457 Business Road", BusinessNature: null,
                PhoneHome: "021-2222221", PhoneOffice: "021-2222222", PhoneMobile: "0300-2222222", DeclarationAcceptedAt: null),
        ]));

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), JsonOptions);
        Assert.Contains(
            problem.GetProperty("missingDocuments").EnumerateArray().Select(e => e.GetString()),
            m => m!.Contains("Guarantor"));
    }
}
