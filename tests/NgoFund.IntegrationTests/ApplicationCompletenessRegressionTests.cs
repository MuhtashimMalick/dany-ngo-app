using System.Net;
using System.Net.Http.Json;
using NgoFund.Contracts.Applications;
using static NgoFund.IntegrationTests.ApplicationCompletenessTestHelpers;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// The regression guard for the completeness gate added in <see cref="ApplicationCompletenessTests"/>:
/// HEALTH/EDUCATION/EMERGENCY/OTHER have no manifest at all (empty slot list, empty required-field
/// list) and must continue to Approve exactly as they did before this milestone. Also covers the
/// read-only <c>GET .../completeness</c> endpoint. Kept in its own class/fixture purely to stay
/// under the shared login-rate-limit budget — see <see cref="ApplicationCompletenessTests"/>.
/// </summary>
public class ApplicationCompletenessRegressionTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Theory]
    [InlineData("HEALTH")]
    [InlineData("EDUCATION")]
    [InlineData("EMERGENCY")]
    [InlineData("OTHER")]
    public async Task NonFormCategory_ApprovesWithZeroDetailsAndZeroDocuments_ExactlyAsBefore(string categoryCode)
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, $"50102-{Math.Abs(categoryCode.GetHashCode()) % 10000000:D7}-1");
        var (categories, generalFundId) = await LoadSeedIdsAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories[categoryCode], generalFundId);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetCompleteness_MatchesManifest_AndReflectsUploadsAccurately()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50102-1111111-7");
        var (categories, generalFundId) = await LoadSeedIdsAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["HOUSE_RENT"], generalFundId);

        var before = await ReadOrFailAsync<ApplicationCompletenessDto>(
            await client.GetAsync($"/api/applications/{application.Id}/completeness"), HttpStatusCode.OK);
        Assert.False(before.IsComplete);
        Assert.Equal(4, before.Slots.Count); // HOUSE_RENT manifest: 4 slots
        // v1.4 (B2): APPLICANT_CNIC/MEMBERSHIP_CARD are Applicant-scoped and already satisfied by
        // the CNIC/membership-card documents CreateApplicantAsync uploaded to the applicant's own
        // profile — the only unsatisfied slot at this point is UTILITY_BILLS.
        Assert.True(before.Slots.Single(s => s.SlotKey == "HOUSE_RENT.APPLICANT_CNIC").IsSatisfied);
        Assert.True(before.Slots.Single(s => s.SlotKey == "HOUSE_RENT.MEMBERSHIP_CARD").IsSatisfied);
        Assert.False(before.Slots.Single(s => s.SlotKey == "HOUSE_RENT.UTILITY_BILLS").IsSatisfied);

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
