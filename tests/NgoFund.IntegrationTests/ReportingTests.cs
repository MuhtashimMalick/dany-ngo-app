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
using NgoFund.Contracts.Reports;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the M5 dashboard/report aggregations end to end through the real HTTP API: application
/// status/category counts reflect what was created, monthly donation totals group correctly by
/// (year, month, fund), and the CSV export endpoint returns a well-formed file.
/// </summary>
public class ReportingTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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
    public async Task GetDashboardSummary_ReflectsApplicationsByStatus()
    {
        var client = await CreateAuthenticatedClientAsync();

        var before = await ReadOrFailAsync<DashboardSummaryDto>(await client.GetAsync("/api/reports/dashboard"), HttpStatusCode.OK);
        var pendingBefore = before.ApplicationsByStatus.SingleOrDefault(s => s.Status == "Pending")?.Count ?? 0;

        var categories = (await client.GetFromJsonAsync<List<ApplicationCategoryDto>>("/api/application-categories"))!;
        var funds = (await client.GetFromJsonAsync<List<FundCategoryDto>>("/api/fund-categories"))!;
        var healthCategory = categories.Single(c => c.Code == "HEALTH");
        var zakatFund = funds.Single(f => f.Code == "ZAKAT");

        var applicantResponse = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "Dashboard Test Applicant", null, "99999-9999999-9", "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        var applicant = await ReadOrFailAsync<ApplicantDto>(applicantResponse, HttpStatusCode.Created);

        var createResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, healthCategory.Id, zakatFund.Id, 3000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test"));
        await ReadOrFailAsync<ApplicationDto>(createResponse, HttpStatusCode.Created);

        var after = await ReadOrFailAsync<DashboardSummaryDto>(await client.GetAsync("/api/reports/dashboard"), HttpStatusCode.OK);
        var pendingAfter = after.ApplicationsByStatus.Single(s => s.Status == "Pending").Count;

        Assert.Equal(pendingBefore + 1, pendingAfter);
        Assert.Contains(after.ApplicationsByCategory, c => c.CategoryName == healthCategory.Name);
    }

    [Fact]
    public async Task GetMonthlyDonations_GroupsByYearMonthAndFund()
    {
        var client = await CreateAuthenticatedClientAsync();

        var funds = (await client.GetFromJsonAsync<List<FundCategoryDto>>("/api/fund-categories"))!;
        var generalFund = funds.Single(f => f.Code == "GENERAL");

        var donorResponse = await client.PostAsJsonAsync("/api/donors", new CreateDonorRequest(
            "Report Test Donor", "Individual", null, null, null, null, null, null, null, null, null, false, null));
        var donor = (await donorResponse.Content.ReadFromJsonAsync<DonorDto>())!;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await client.PostAsJsonAsync("/api/donations", new CreateDonationRequest(
            donor.Id, generalFund.Id, 1500m, today, "Cash", null, null, null, null));
        await client.PostAsJsonAsync("/api/donations", new CreateDonationRequest(
            donor.Id, generalFund.Id, 2500m, today, "Cash", null, null, null, null));

        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var response = await client.GetAsync($"/api/reports/donations/monthly?from={monthStart:yyyy-MM-dd}&to={monthEnd:yyyy-MM-dd}");
        var rows = await ReadOrFailAsync<List<MonthlySummaryRowDto>>(response, HttpStatusCode.OK);

        var generalRow = rows.Single(r => r.FundCategoryId == generalFund.Id && r.Year == today.Year && r.Month == today.Month);
        Assert.Equal(4000m, generalRow.Total);
    }

    [Fact]
    public async Task ExportMonthlyDonations_ReturnsCsvWithHeaderRow()
    {
        var client = await CreateAuthenticatedClientAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var response = await client.GetAsync($"/api/reports/donations/monthly/export?from={monthStart:yyyy-MM-dd}&to={monthEnd:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var csv = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("Year,Month,Fund,Total", csv);
    }
}
