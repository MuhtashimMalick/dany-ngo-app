using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.ApplicationCategories;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.FundCategories;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the M6 "global search" fields (membership number, category, date range) that were
/// added to the existing Applications endpoint rather than a separate search subsystem.
/// </summary>
public class ApplicationSearchTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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

    [Fact]
    public async Task SearchByMembershipNumber_FindsTheApplication()
    {
        var client = await CreateAuthenticatedClientAsync();
        var categories = (await client.GetFromJsonAsync<List<ApplicationCategoryDto>>("/api/application-categories"))!;
        var funds = (await client.GetFromJsonAsync<List<FundCategoryDto>>("/api/fund-categories"))!;
        var category = categories.Single(c => c.Code == "EDUCATION");
        // EDUCATION is ZakatOnly under the v1.5 FundEligibility amendment (it used to be
        // dual-eligible), so it needs the Zakat fund now, not General.
        var fund = funds.Single(f => f.Code == "ZAKAT");

        var applicantResponse = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            "SEARCH-001", "Search Test Applicant", null, "11122-3344556-7", "Female", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        var applicant = await ReadOrFailAsync<ApplicantDto>(applicantResponse, HttpStatusCode.Created);

        var createResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, category.Id, fund.Id, 4000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test"));
        var application = await ReadOrFailAsync<ApplicationDto>(createResponse, HttpStatusCode.Created);

        var searchResponse = await client.GetAsync("/api/applications?search=SEARCH-001");
        var page = await ReadOrFailAsync<PagedResult<ApplicationDto>>(searchResponse, HttpStatusCode.OK);

        Assert.Contains(page.Items, a => a.Id == application.Id);
    }

    [Fact]
    public async Task FilterByCategoryAndDateRange_ExcludesNonMatchingApplications()
    {
        var client = await CreateAuthenticatedClientAsync();
        var categories = (await client.GetFromJsonAsync<List<ApplicationCategoryDto>>("/api/application-categories"))!;
        var funds = (await client.GetFromJsonAsync<List<FundCategoryDto>>("/api/fund-categories"))!;
        var healthCategory = categories.Single(c => c.Code == "HEALTH");
        var rozgarCategory = categories.Single(c => c.Code == "ROZGAR");
        var generalFund = funds.Single(f => f.Code == "GENERAL");
        // HEALTH is ZakatOnly under the v1.5 FundEligibility amendment (it used to be
        // dual-eligible); ROZGAR stays GeneralOnly, unaffected.
        var zakatFund = funds.Single(f => f.Code == "ZAKAT");

        var applicantResponse = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "Filter Test Applicant", null, "22233-4455667-8", "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        var applicant = await ReadOrFailAsync<ApplicantDto>(applicantResponse, HttpStatusCode.Created);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2);
        var recentDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Matches neither the category filter nor the date range used below.
        var oldRozgarResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, rozgarCategory.Id, generalFund.Id, 1000m, "Normal", oldDate, "Old, wrong category"));
        var oldRozgar = await ReadOrFailAsync<ApplicationDto>(oldRozgarResponse, HttpStatusCode.Created);

        // Matches both.
        var recentHealthResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, healthCategory.Id, zakatFund.Id, 2000m, "Normal", recentDate, "Recent, right category"));
        var recentHealth = await ReadOrFailAsync<ApplicationDto>(recentHealthResponse, HttpStatusCode.Created);

        var url = $"/api/applications?categoryId={healthCategory.Id}&dateFrom={recentDate.AddDays(-1):yyyy-MM-dd}&dateTo={recentDate.AddDays(1):yyyy-MM-dd}";
        var response = await client.GetAsync(url);
        var page = await ReadOrFailAsync<PagedResult<ApplicationDto>>(response, HttpStatusCode.OK);

        Assert.Contains(page.Items, a => a.Id == recentHealth.Id);
        Assert.DoesNotContain(page.Items, a => a.Id == oldRozgar.Id);
    }
}
