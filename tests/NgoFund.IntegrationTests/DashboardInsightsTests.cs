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
using NgoFund.Contracts.Payments;
using NgoFund.Contracts.Reports;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Covers the new <c>GET /api/reports/dashboard/insights</c> endpoint end to end through the real
/// HTTP API. Each test class below gets its own <see cref="AuthApiFactory"/> (its own freshly
/// migrated Postgres container) rather than sharing one across multiple [Fact]s in the same
/// class — the empty-database assertions require a database nothing else has written to yet,
/// which a shared-fixture class with several tests can't guarantee ordering-wise.
/// </summary>
public class DashboardInsightsEmptyDatabaseTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// The highest-priority case from the spec: every GroupBy(_ => 1)/Max()/Average() in this
    /// feature must degrade to zero counts and null averages on an empty (seed-only) database,
    /// never throw a 500.
    /// </summary>
    [Fact]
    public async Task GetDashboardInsights_OnFreshlySeededDatabase_ReturnsZeroesNotError()
    {
        var client = await factory.CreateSeededClientAsync();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@ngofund.local", "ChangeMe123!"));
        var auth = (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.GetAsync("/api/reports/dashboard/insights");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {body}");

        var insights = JsonSerializer.Deserialize<DashboardInsightsDto>(body, JsonOptions)!;

        Assert.Empty(insights.ApplicationsByStatusLast12Months);
        Assert.Null(insights.ProcessingTime.AverageDays);
        Assert.Null(insights.ProcessingTime.MedianDays);
        Assert.Equal(0, insights.ProcessingTime.DecidedCount);
        Assert.Empty(insights.OutstandingCommitments);
        Assert.Equal(0, insights.Donors.Total);
        Assert.Equal(0, insights.Donors.Active);
        Assert.Equal(0, insights.Donors.NewThisMonth);
        Assert.Equal(0, insights.Donors.NewLastMonth);
        Assert.Equal(0, insights.DonationCountThisMonth);
        Assert.Equal(0m, insights.LargestDonationThisMonth);
        Assert.Empty(insights.DonationsByMethodLast12Months);
        Assert.Empty(insights.PaymentsByMethodLast12Months);
        Assert.Empty(insights.OpenApplicationsByPriority);
    }
}

public class DashboardInsightsWorkflowTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static async Task<T> ReadOrFailAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {expected}, got {response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, JsonOptions)!;
    }

    [Fact]
    public async Task GetDashboardInsights_AfterApproveAndPartialPayment_ReflectsStatusAndOutstandingCommitment()
    {
        var client = await factory.CreateSeededClientAsync();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@ngofund.local", "ChangeMe123!"));
        var auth = (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var categories = (await client.GetFromJsonAsync<List<ApplicationCategoryDto>>("/api/application-categories"))!;
        var funds = (await client.GetFromJsonAsync<List<FundCategoryDto>>("/api/fund-categories"))!;
        var healthCategory = categories.Single(c => c.Code == "HEALTH");
        var zakatFund = funds.Single(f => f.Code == "ZAKAT");

        // Fund the Zakat fund so the payment below has balance to draw from.
        var donorResponse = await client.PostAsJsonAsync("/api/donors", new CreateDonorRequest(
            "Insights Test Donor", "Individual", null, null, null, null, null, null, null, null, null, false, null));
        var donor = await ReadOrFailAsync<DonorDto>(donorResponse, HttpStatusCode.Created);
        await client.PostAsJsonAsync("/api/donations", new CreateDonationRequest(
            donor.Id, zakatFund.Id, 50000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null, null));

        var applicantResponse = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "Insights Test Applicant", null, "11111-2222222-3", "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        var applicant = await ReadOrFailAsync<ApplicantDto>(applicantResponse, HttpStatusCode.Created);

        var createResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, healthCategory.Id, zakatFund.Id, 10000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test"));
        var application = await ReadOrFailAsync<ApplicationDto>(createResponse, HttpStatusCode.Created);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null));

        var paymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 4000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, "Recipient", null, null, null));
        await ReadOrFailAsync<PaymentDto>(paymentResponse, HttpStatusCode.Created);

        var insightsResponse = await client.GetAsync("/api/reports/dashboard/insights");
        var insights = await ReadOrFailAsync<DashboardInsightsDto>(insightsResponse, HttpStatusCode.OK);

        var partiallyPaid = insights.ApplicationsByStatusLast12Months.Single(s => s.Status == "PartiallyPaid");
        Assert.Equal(1, partiallyPaid.Count);

        var commitment = insights.OutstandingCommitments.Single(c => c.FundCategoryId == zakatFund.Id);
        Assert.Equal(6000m, commitment.ApprovedOutstanding);
    }
}

public class DashboardInsightsAuthTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task GetDashboardInsights_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = await factory.CreateSeededClientAsync();

        var response = await client.GetAsync("/api/reports/dashboard/insights");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
