using System.Net;
using System.Net.Http.Json;
using NgoFund.Contracts.Applications;
using static NgoFund.IntegrationTests.ApplicationCompletenessTestHelpers;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// The regression guard for the completeness gate added in <see cref="ApplicationCompletenessTests"/>.
/// EMERGENCY has no manifest at all (empty slot list, empty required-field list) and must continue
/// to Approve exactly as before. HEALTH/EDUCATION/OTHER gained a real manifest via the Google Form
/// intake integration (A9) — feedback round 3, item I corrects this class, which previously (and
/// wrongly) asserted they still approved bare: a bare application in any of the three must now be
/// BLOCKED (422), and approves only once its manifest is satisfied (via the shared
/// <see cref="ApplicationCompletenessTestHelpers.CompleteHealthDetailsAsync"/>/
/// <see cref="ApplicationCompletenessTestHelpers.CompleteEducationDetailsAsync"/>/
/// <see cref="ApplicationCompletenessTestHelpers.CompleteOtherDocumentsAsync"/> helpers). Also
/// covers the read-only <c>GET .../completeness</c> endpoint. Kept in its own class/fixture purely
/// to stay under the shared login-rate-limit budget — see <see cref="ApplicationCompletenessTests"/>.
/// </summary>
public class ApplicationCompletenessRegressionTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task Emergency_ApprovesWithZeroDetailsAndZeroDocuments_ExactlyAsBefore()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50102-1111111-1");
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["EMERGENCY"], zakatFundId);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [InlineData("HEALTH")]
    [InlineData("EDUCATION")]
    [InlineData("OTHER")]
    public async Task FormDrivenCategory_BareApplication_BlockedFromApproved(string categoryCode)
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, $"50102-{Math.Abs(categoryCode.GetHashCode()) % 10000000:D7}-2");
        // Zakat, not General: HEALTH/EDUCATION are ZakatOnly under the v1.5 FundEligibility
        // amendment, and OTHER (Either) accepts Zakat too, so this one fund works for all three.
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories[categoryCode], zakatFundId);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Theory]
    [InlineData("HEALTH")]
    [InlineData("EDUCATION")]
    [InlineData("OTHER")]
    public async Task FormDrivenCategory_CompleteApplication_Approves(string categoryCode)
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, $"50102-{Math.Abs(categoryCode.GetHashCode()) % 10000000:D7}-3");
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories[categoryCode], zakatFundId);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        switch (categoryCode)
        {
            case "HEALTH": await CompleteHealthDetailsAsync(client, application.Id); break;
            case "EDUCATION": await CompleteEducationDetailsAsync(client, application.Id); break;
            case "OTHER": await CompleteOtherDocumentsAsync(client, application.Id); break;
        }

        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetCompleteness_MatchesManifest_AndReflectsUploadsAccurately()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50102-1111111-7");
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["HOUSE_RENT"], zakatFundId);

        var before = await ReadOrFailAsync<ApplicationCompletenessDto>(
            await client.GetAsync($"/api/applications/{application.Id}/completeness"), HttpStatusCode.OK);
        Assert.False(before.IsComplete); // housing-details fields (ApplicantAge etc.) still missing
        Assert.Equal(4, before.Slots.Count); // HOUSE_RENT manifest: 4 slots
        // v1.4 (B2): APPLICANT_CNIC/MEMBERSHIP_CARD are Applicant-scoped and already satisfied by
        // the CNIC/membership-card documents CreateApplicantAsync uploaded to the applicant's own
        // profile. UTILITY_BILLS is optional (item 6, 2026-09 feedback: MinCount 0), so every slot
        // already reads as satisfied at this point — overall completeness still correctly fails on
        // the missing housing-details fields checked below via IsComplete.
        Assert.True(before.Slots.Single(s => s.SlotKey == "HOUSE_RENT.APPLICANT_CNIC").IsSatisfied);
        Assert.True(before.Slots.Single(s => s.SlotKey == "HOUSE_RENT.MEMBERSHIP_CARD").IsSatisfied);
        Assert.True(before.Slots.Single(s => s.SlotKey == "HOUSE_RENT.UTILITY_BILLS").IsSatisfied);

        await UploadAsync(client, application.Id, "UtilityBill", "HOUSE_RENT.UTILITY_BILLS");
        await UploadAsync(client, application.Id, "UtilityBill", "HOUSE_RENT.UTILITY_BILLS");
        await UploadAsync(client, application.Id, "UtilityBill", "HOUSE_RENT.UTILITY_BILLS");

        var after = await ReadOrFailAsync<ApplicationCompletenessDto>(
            await client.GetAsync($"/api/applications/{application.Id}/completeness"), HttpStatusCode.OK);
        var utilityBillsSlot = after.Slots.Single(s => s.SlotKey == "HOUSE_RENT.UTILITY_BILLS");
        Assert.True(utilityBillsSlot.IsSatisfied);
        Assert.Equal(3, utilityBillsSlot.Documents.Count);
    }
}
