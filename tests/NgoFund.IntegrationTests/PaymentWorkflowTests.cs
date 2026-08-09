using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.FundCategories;
using NgoFund.Contracts.Payments;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the M4 payment invariants end to end through the real HTTP API: a payment can never
/// push total-paid past the approved amount, can never exceed the fund's ledger balance, the
/// application's status auto-updates as payments are recorded, and voiding a payment reverses
/// both the ledger and the application status.
/// </summary>
public class PaymentWorkflowTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task CreatePayment_PartialThenFull_UpdatesApplicationStatus_PartiallyPaidThenPaid()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId, _) = await PaymentTestHelpers.LoadSeededIdsAsync(client);
        await FundAsync(client, zakatFundId, 50000m);

        var application = await CreateApprovedApplicationAsync(client, "44444-4444444-4", categoryId, zakatFundId, 10000m);

        var firstPaymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 4000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, "Recipient", null, null, null));
        await ReadOrFailAsync<PaymentDto>(firstPaymentResponse, HttpStatusCode.Created);

        var afterFirst = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
        Assert.Equal("PartiallyPaid", afterFirst.Status);

        var secondPaymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 6000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, "Recipient", null, null, null));
        await ReadOrFailAsync<PaymentDto>(secondPaymentResponse, HttpStatusCode.Created);

        var afterSecond = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
        Assert.Equal("Paid", afterSecond.Status);
    }

    [Fact]
    public async Task CreatePayment_ExceedingApprovedAmount_IsRejected()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId, _) = await LoadSeededIdsAsync(client);
        await FundAsync(client, zakatFundId, 50000m);

        var application = await CreateApprovedApplicationAsync(client, "55555-5555555-5", categoryId, zakatFundId, 5000m);

        var response = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 5000.01m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null, null, null, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreatePayment_ExceedingFundBalance_IsRejected()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId, _) = await LoadSeededIdsAsync(client);

        // Deliberately no FundAsync call here — approve a large application against an
        // under-funded (or zero-balance) Zakat fund and confirm the payment is rejected.
        var application = await CreateApprovedApplicationAsync(client, "66666-6666666-6", categoryId, zakatFundId, 999_999_999m);

        var response = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 999_999_999m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null, null, null, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreatePayment_AgainstNonApprovedApplication_IsRejected()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId, _) = await LoadSeededIdsAsync(client);
        await FundAsync(client, zakatFundId, 50000m);

        var applicantResponse = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "Pending Applicant", null, "77777-7777777-7", "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null));
        var applicant = await ReadOrFailAsync<ApplicantDto>(applicantResponse, HttpStatusCode.Created);

        var createResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, categoryId, zakatFundId, 5000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test"));
        var application = await ReadOrFailAsync<ApplicationDto>(createResponse, HttpStatusCode.Created);

        // application is still Pending, never approved
        var response = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 1000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null, null, null, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private static async Task<FundBalanceDto> GetFundBalanceDtoAsync(HttpClient client, string code)
    {
        var balances = (await client.GetFromJsonAsync<List<FundBalanceDto>>("/api/fund-categories/balances", JsonOptions))!;
        return balances.Single(b => b.Code == code);
    }

    [Fact]
    public async Task VoidPayment_ReversesLedger_AndRecomputesApplicationStatusBackToApproved()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId, _) = await LoadSeededIdsAsync(client);
        await FundAsync(client, zakatFundId, 50000m);

        var application = await CreateApprovedApplicationAsync(client, "88888-8888888-8", categoryId, zakatFundId, 8000m);

        var beforePayment = await GetFundBalanceDtoAsync(client, "ZAKAT");

        var paymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 8000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null, null, null, null));
        var payment = await ReadOrFailAsync<PaymentDto>(paymentResponse, HttpStatusCode.Created);

        var afterPayment = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
        Assert.Equal("Paid", afterPayment.Status);

        var voidResponse = await client.PostAsJsonAsync($"/api/payments/{payment.Id}/void", new VoidPaymentRequest("Entered by mistake"));
        Assert.Equal(HttpStatusCode.NoContent, voidResponse.StatusCode);

        var afterVoid = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
        Assert.Equal("Approved", afterVoid.Status);

        var afterVoidBalance = await GetFundBalanceDtoAsync(client, "ZAKAT");
        Assert.Equal(beforePayment.Balance, afterVoidBalance.Balance);

        // The mirror-image of the donation-void bug: a voided payment's reversal Credit was being
        // counted as a fresh donation, inflating TotalCollected. It must be completely unchanged
        // by a payment void, and TotalUtilized must return to its pre-payment value.
        Assert.Equal(beforePayment.TotalUtilized, afterVoidBalance.TotalUtilized);
        Assert.Equal(beforePayment.TotalCollected, afterVoidBalance.TotalCollected);

        // voiding a second time must be rejected, not silently accepted
        var secondVoidResponse = await client.PostAsJsonAsync($"/api/payments/{payment.Id}/void", new VoidPaymentRequest("Trying again"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondVoidResponse.StatusCode);
    }
}
