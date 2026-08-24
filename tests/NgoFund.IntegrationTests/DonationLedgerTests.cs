using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Donations;
using NgoFund.Contracts.Donors;
using NgoFund.Contracts.FundCategories;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the fund ledger invariant end to end through the real HTTP API: recording a donation
/// increases the fund's balance by exactly the donation amount, and voiding it brings the balance
/// back down — via a reversal row in fund_transactions, not by mutating or deleting anything.
/// </summary>
public class DonationLedgerTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = await factory.CreateSeededClientAsync();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@ngofund.local", "ChangeMe123!"));
        var auth = (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return client;
    }

    private static async Task<FundBalanceDto> GetGeneralFundBalanceDtoAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/fund-categories/balances");
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"GET /api/fund-categories/balances failed with {response.StatusCode}: {body}");
        }

        var balances = System.Text.Json.JsonSerializer.Deserialize<List<FundBalanceDto>>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        return balances.Single(b => b.Code == "GENERAL");
    }

    [Fact]
    public async Task RecordingDonation_IncreasesFundBalance_ByExactAmount()
    {
        var client = await CreateAuthenticatedClientAsync();
        var before = await GetGeneralFundBalanceDtoAsync(client);

        var donorResponse = await client.PostAsJsonAsync("/api/donors", new CreateDonorRequest(
            "Test Donor", "Individual", null, null, null, null, null, null, null, null, null, false, null));
        var donorBody = await donorResponse.Content.ReadAsStringAsync();
        Assert.True(donorResponse.StatusCode == HttpStatusCode.Created, $"Expected Created for donor, got {donorResponse.StatusCode}: {donorBody}");
        var donor = System.Text.Json.JsonSerializer.Deserialize<DonorDto>(donorBody, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.NotEqual(Guid.Empty, donor.Id);

        var fundCategories = (await client.GetFromJsonAsync<List<FundCategoryDto>>("/api/fund-categories"))!;
        var generalFund = fundCategories.Single(f => f.Code == "GENERAL");

        var donationResponse = await client.PostAsJsonAsync("/api/donations", new CreateDonationRequest(
            donor.Id, generalFund.Id, 5000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null));

        var donationBody = await donationResponse.Content.ReadAsStringAsync();
        Assert.True(donationResponse.StatusCode == HttpStatusCode.Created, $"Expected Created, got {donationResponse.StatusCode}: {donationBody}");

        var after = await GetGeneralFundBalanceDtoAsync(client);
        Assert.Equal(before.Balance + 5000m, after.Balance);
        Assert.Equal(before.TotalCollected + 5000m, after.TotalCollected);
        Assert.Equal(before.TotalUtilized, after.TotalUtilized);
    }

    [Fact]
    public async Task VoidingDonation_ReversesFundBalance_ToOriginalValue()
    {
        var client = await CreateAuthenticatedClientAsync();
        var before = await GetGeneralFundBalanceDtoAsync(client);

        var donorResponse = await client.PostAsJsonAsync("/api/donors", new CreateDonorRequest(
            "Void Test Donor", "Individual", null, null, null, null, null, null, null, null, null, false, null));
        var donor = (await donorResponse.Content.ReadFromJsonAsync<DonorDto>())!;

        var fundCategories = (await client.GetFromJsonAsync<List<FundCategoryDto>>("/api/fund-categories"))!;
        var generalFund = fundCategories.Single(f => f.Code == "GENERAL");

        var donationResponse = await client.PostAsJsonAsync("/api/donations", new CreateDonationRequest(
            donor.Id, generalFund.Id, 2500m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null));
        var donation = (await donationResponse.Content.ReadFromJsonAsync<DonationDto>())!;

        var voidResponse = await client.PostAsJsonAsync($"/api/donations/{donation.Id}/void", new VoidDonationRequest("Entered by mistake"));
        Assert.Equal(HttpStatusCode.NoContent, voidResponse.StatusCode);

        var after = await GetGeneralFundBalanceDtoAsync(client);
        Assert.Equal(before.Balance, after.Balance);

        // The exact client-reported bug: a voided donation's reversal Debit was being counted as
        // a real disbursement, inflating TotalUtilized. It must be completely unchanged by a
        // donation void.
        Assert.Equal(before.TotalUtilized, after.TotalUtilized);
        Assert.Equal(before.TotalCollected, after.TotalCollected);

        // voiding a second time must be rejected, not silently accepted
        var secondVoidResponse = await client.PostAsJsonAsync($"/api/donations/{donation.Id}/void", new VoidDonationRequest("Trying again"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondVoidResponse.StatusCode);
    }
}
