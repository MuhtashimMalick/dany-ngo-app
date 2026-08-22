using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.ApplicationCategories;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Donations;
using NgoFund.Contracts.Donors;
using NgoFund.Contracts.FundCategories;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Shared scaffolding (auth, funding a fund, driving an application to Approved) needed by both
/// <see cref="PaymentWorkflowTests"/> (money invariants) and <see cref="PaymentSearchTests"/>
/// (filter/search correctness) so neither file re-implements it.
/// </summary>
internal static class PaymentTestHelpers
{
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<HttpClient> CreateAuthenticatedClientAsync(this AuthApiFactory factory)
    {
        var client = await factory.CreateSeededClientAsync();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@ngofund.local", "ChangeMe123!"));
        var auth = (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return client;
    }

    public static async Task<T> ReadOrFailAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {expected}, got {response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, JsonOptions)!;
    }

    /// <summary>Donates the given amount into a fund so payments against it have balance to draw from.</summary>
    public static async Task FundAsync(HttpClient client, Guid fundCategoryId, decimal amount)
    {
        var donorResponse = await client.PostAsJsonAsync("/api/donors", new CreateDonorRequest(
            "Payment Test Donor", "Individual", null, null, null, null, null, null, null, null, null, false, null));
        var donor = (await donorResponse.Content.ReadFromJsonAsync<DonorDto>())!;

        var donationResponse = await client.PostAsJsonAsync("/api/donations", new CreateDonationRequest(
            donor.Id, fundCategoryId, amount, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null, null));
        await ReadOrFailAsync<DonationDto>(donationResponse, HttpStatusCode.Created);
    }

    public static async Task<(Guid HealthCategoryId, Guid ZakatFundId, Guid GeneralFundId)> LoadSeededIdsAsync(HttpClient client)
    {
        var categories = (await client.GetFromJsonAsync<List<ApplicationCategoryDto>>("/api/application-categories"))!;
        var funds = (await client.GetFromJsonAsync<List<FundCategoryDto>>("/api/fund-categories"))!;
        return (categories.Single(c => c.Code == "HEALTH").Id, funds.Single(f => f.Code == "ZAKAT").Id, funds.Single(f => f.Code == "GENERAL").Id);
    }

    /// <summary>Creates an applicant + application and drives it to Approved, ready to accept payments.</summary>
    public static async Task<ApplicationDto> CreateApprovedApplicationAsync(
        HttpClient client, string cnic, Guid categoryId, Guid fundId, decimal requestedAmount)
    {
        var applicantResponse = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "Payment Test Applicant", null, cnic, "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        var applicant = await ReadOrFailAsync<ApplicantDto>(applicantResponse, HttpStatusCode.Created);

        var createResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, categoryId, fundId, requestedAmount, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test"));
        var application = await ReadOrFailAsync<ApplicationDto>(createResponse, HttpStatusCode.Created);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null));

        return await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
    }
}
