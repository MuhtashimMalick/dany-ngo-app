using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.ApplicationCategories;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Donations;
using NgoFund.Contracts.Donors;
using NgoFund.Contracts.FundCategories;
using NgoFund.Contracts.Payments;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Regression tests for the manual-status-transition bug (a reviewer could set an application to
/// Paid/PartiallyPaid by hand via the status-change endpoint regardless of actual payments) and
/// its N1 (missing audit trail on system-driven transitions) / N2 (PartiallyPaid -> OnHold ->
/// Rejected laundering path) fallout. All of these would have PASSED (NoContent / a wrong end
/// state) against the pre-fix code, since the old <c>AllowedTransitions</c> table listed
/// Paid/PartiallyPaid as legal manual targets from Approved/PartiallyPaid with no check against
/// payment totals, and OnHold -> Rejected stayed legal even once money had moved.
///
/// Kept in its own test class/fixture (own <see cref="AuthApiFactory"/>, own Postgres container)
/// rather than folded into <see cref="PaymentWorkflowTests"/> — same reasoning as
/// <c>SecurityHardeningTests</c>: each login-per-test test class shares one rate-limiter
/// partition (10 logins/minute/IP, see <c>Program.cs</c>), and combining this many tests with
/// <see cref="PaymentWorkflowTests"/>' existing ones would push a single class over that budget.
/// </summary>
public class StatusTransitionInvariantTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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

    /// <summary>Donates the given amount into a fund so payments against it have balance to draw from.</summary>
    private static async Task FundAsync(HttpClient client, Guid fundCategoryId, decimal amount)
    {
        var donorResponse = await client.PostAsJsonAsync("/api/donors", new CreateDonorRequest(
            "Status Invariant Test Donor", "Individual", null, null, null, null, null, null, null, null, null, false, null));
        var donor = (await donorResponse.Content.ReadFromJsonAsync<DonorDto>())!;

        var donationResponse = await client.PostAsJsonAsync("/api/donations", new CreateDonationRequest(
            donor.Id, fundCategoryId, amount, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null));
        await ReadOrFailAsync<DonationDto>(donationResponse, HttpStatusCode.Created);
    }

    private static async Task<(Guid HealthCategoryId, Guid ZakatFundId)> LoadSeededIdsAsync(HttpClient client)
    {
        var categories = (await client.GetFromJsonAsync<List<ApplicationCategoryDto>>("/api/application-categories"))!;
        var funds = (await client.GetFromJsonAsync<List<FundCategoryDto>>("/api/fund-categories"))!;
        return (categories.Single(c => c.Code == "HEALTH").Id, funds.Single(f => f.Code == "ZAKAT").Id);
    }

    /// <summary>Creates an applicant + application and drives it to Approved, ready to accept payments.</summary>
    private static async Task<ApplicationDto> CreateApprovedApplicationAsync(
        HttpClient client, string cnic, Guid categoryId, Guid fundId, decimal requestedAmount)
    {
        var applicantResponse = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "Status Invariant Test Applicant", null, cnic, "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        var applicant = await ReadOrFailAsync<ApplicantDto>(applicantResponse, HttpStatusCode.Created);

        var createResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, categoryId, fundId, requestedAmount, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test"));
        var application = await ReadOrFailAsync<ApplicationDto>(createResponse, HttpStatusCode.Created);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null, requestedAmount));

        return await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
    }

    [Fact]
    public async Task ManualStatusChange_ToPaid_OnApprovedApplicationWithUnmetAmount_IsRejected()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId) = await LoadSeededIdsAsync(client);
        var application = await CreateApprovedApplicationAsync(client, "10101-1010101-1", categoryId, zakatFundId, 5000m);

        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Paid", "Attempting to mark Paid by hand", null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ManualStatusChange_ToPartiallyPaid_OnApprovedApplication_IsRejected()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId) = await LoadSeededIdsAsync(client);
        var application = await CreateApprovedApplicationAsync(client, "10101-1010101-2", categoryId, zakatFundId, 5000m);

        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("PartiallyPaid", "Attempting to mark PartiallyPaid by hand", null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task AllowedNextStatuses_AfterPartialPayment_ExcludesPaidPartiallyPaidAndRejected()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId) = await LoadSeededIdsAsync(client);
        await FundAsync(client, zakatFundId, 50000m);
        var application = await CreateApprovedApplicationAsync(client, "10101-1010101-3", categoryId, zakatFundId, 10000m);

        var paymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 4000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null));
        await ReadOrFailAsync<PaymentDto>(paymentResponse, HttpStatusCode.Created);

        var afterPayment = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);

        Assert.Equal("PartiallyPaid", afterPayment.Status);
        Assert.DoesNotContain("Paid", afterPayment.AllowedNextStatuses);
        Assert.DoesNotContain("PartiallyPaid", afterPayment.AllowedNextStatuses);
        Assert.DoesNotContain("Rejected", afterPayment.AllowedNextStatuses);
    }

    [Fact]
    public async Task OnHoldWithPayments_CannotBeRejected_ButCanReturnToApproved_AndImmediatelyResettles()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId) = await LoadSeededIdsAsync(client);
        await FundAsync(client, zakatFundId, 50000m);
        var application = await CreateApprovedApplicationAsync(client, "10101-1010101-4", categoryId, zakatFundId, 15000m);

        var paymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 4000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null));
        await ReadOrFailAsync<PaymentDto>(paymentResponse, HttpStatusCode.Created);

        var toOnHold = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("OnHold", "Reviewing further documents", null));
        Assert.Equal(HttpStatusCode.NoContent, toOnHold.StatusCode);

        // N2 regression: the PartiallyPaid -> OnHold -> Rejected laundering path must now be closed.
        var toRejected = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Rejected", "Trying to launder to terminal Rejected", "Not a real reason"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, toRejected.StatusCode);

        var toApproved = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", "Resuming review", null));
        Assert.Equal(HttpStatusCode.NoContent, toApproved.StatusCode);

        // N1/D2 regression: the manual OnHold -> Approved transition must immediately re-settle to
        // ledger truth (PartiallyPaid), not sit incorrectly at Approved with 4000 already paid.
        var afterApproved = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
        Assert.Equal("PartiallyPaid", afterApproved.Status);
    }

    [Fact]
    public async Task StatusHistory_RecordsAutoTransitions_OnPaymentAndVoid_WithSystemAttributedRemarks()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId) = await LoadSeededIdsAsync(client);
        await FundAsync(client, zakatFundId, 50000m);
        var application = await CreateApprovedApplicationAsync(client, "10101-1010101-5", categoryId, zakatFundId, 15000m);

        var paymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 4000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null));
        var payment = await ReadOrFailAsync<PaymentDto>(paymentResponse, HttpStatusCode.Created);

        var historyAfterPayment = (await client.GetFromJsonAsync<List<ApplicationStatusHistoryDto>>($"/api/applications/{application.Id}/history"))!;
        Assert.Contains(historyAfterPayment, h => h.FromStatus == "Approved" && h.ToStatus == "PartiallyPaid" && h.Remarks != null && h.Remarks.Contains(payment.PaymentNumber));

        var voidResponse = await client.PostAsJsonAsync($"/api/payments/{payment.Id}/void", new VoidPaymentRequest("Entered by mistake"));
        Assert.Equal(HttpStatusCode.NoContent, voidResponse.StatusCode);

        var historyAfterVoid = (await client.GetFromJsonAsync<List<ApplicationStatusHistoryDto>>($"/api/applications/{application.Id}/history"))!;
        Assert.Contains(historyAfterVoid, h => h.FromStatus == "PartiallyPaid" && h.ToStatus == "Approved" && h.Remarks != null && h.Remarks.Contains(payment.PaymentNumber));
    }

    [Fact]
    public async Task CreatePayment_AgainstApplicationWithNullApprovedAmount_IsRejected()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId) = await LoadSeededIdsAsync(client);
        await FundAsync(client, zakatFundId, 50000m);
        var application = await CreateApprovedApplicationAsync(client, "10101-1010101-6", categoryId, zakatFundId, 5000m);

        // No legitimate API path can leave an Approved application with a null ApprovedAmount
        // (E6's UpdateAsync guard blocks it) — force it directly in the DB to exercise E5's guard.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE applications SET approved_amount = NULL WHERE id = {application.Id}");
        }

        var response = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 100m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task UpdateApplication_LoweringApprovedAmountBelowCompletedPayments_IsRejected()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId) = await LoadSeededIdsAsync(client);
        await FundAsync(client, zakatFundId, 50000m);
        var application = await CreateApprovedApplicationAsync(client, "10101-1010101-7", categoryId, zakatFundId, 15000m);

        var paymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 4000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null));
        await ReadOrFailAsync<PaymentDto>(paymentResponse, HttpStatusCode.Created);

        var updateResponse = await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            categoryId, zakatFundId, 15000m, 2000m, "Normal", "Trying to lower approved amount below what's already paid"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, updateResponse.StatusCode);
    }
}
