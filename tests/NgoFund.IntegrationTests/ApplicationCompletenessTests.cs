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
        var (categories, generalFundId) = await LoadSeedIdsAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["SHAADI"], generalFundId);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));

        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null));

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
        var (categories, generalFundId) = await LoadSeedIdsAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["SHAADI"], generalFundId);

        await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/marriage", FullyValidMarriageDetails);
        await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            categories["SHAADI"], generalFundId, application.RequestedAmount, null, "Normal", "Test",
            DeclarationAcceptedAt: DateTimeOffset.UtcNow, TermsAcceptedAt: DateTimeOffset.UtcNow));

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), JsonOptions);
        Assert.Empty(problem.GetProperty("missingFields").EnumerateArray());
        Assert.NotEmpty(problem.GetProperty("missingDocuments").EnumerateArray());
    }

    [Fact]
    public async Task Shaadi_EveryRequiredSlotUploaded_ApprovedSucceeds_EvenWithoutOptionalWeddingCard()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50101-1111111-3");
        var (categories, generalFundId) = await LoadSeedIdsAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["SHAADI"], generalFundId);

        await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/marriage", FullyValidMarriageDetails);
        await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            categories["SHAADI"], generalFundId, application.RequestedAmount, null, "Normal", "Test",
            DeclarationAcceptedAt: DateTimeOffset.UtcNow, TermsAcceptedAt: DateTimeOffset.UtcNow));
        await UploadAllRequiredShaadiDocumentsAsync(client, application.Id);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
        Assert.Equal("Approved", after.Status);
    }

    [Fact]
    public async Task HouseRent_EveryRequiredSlotUploaded_ApprovedSucceeds_EvenWithoutOptionalRentReceipts()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50101-1111111-4");
        var (categories, generalFundId) = await LoadSeedIdsAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["HOUSE_RENT"], generalFundId);

        await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/housing", FullyValidHousingDetails);
        await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            categories["HOUSE_RENT"], generalFundId, application.RequestedAmount, null, "Normal", "Test",
            DeclaredMonthlyIncome: 30000m, DeclaredHouseholdSize: 5, DeclaredResidentialAddress: "123 Main St",
            DeclaredHouseStatus: "Rented",
            DeclarationAcceptedAt: DateTimeOffset.UtcNow, TermsAcceptedAt: DateTimeOffset.UtcNow));
        await UploadAllRequiredHouseRentDocumentsAsync(client, application.Id);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null));

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
            new GuarantorEntry(null, 1, "MEM-1", "Guarantor One", null, null, null, "11111-1111111-1", null, null, null, null, null, null, null),
            new GuarantorEntry(null, 2, "MEM-2", "Guarantor Two", null, null, null, "22222-2222222-2", null, null, null, null, null, null, null),
        ]));

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), JsonOptions);
        Assert.Contains(
            problem.GetProperty("missingDocuments").EnumerateArray().Select(e => e.GetString()),
            m => m!.Contains("Guarantor"));
    }
}
