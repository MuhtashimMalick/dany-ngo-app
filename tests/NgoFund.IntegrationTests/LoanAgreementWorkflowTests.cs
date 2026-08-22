using System.Net;
using System.Net.Http.Json;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Loans;
using NgoFund.Contracts.Payments;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the M7 core requirement end to end through the real HTTP API: a General-fund
/// application cannot accept payments until a loan agreement exists, becomes payable once one is
/// created, and — the core regression for "Zakat is completely untouched" — a Zakat-fund
/// application never has any loan surface at all.
/// </summary>
public class LoanAgreementWorkflowTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task Payment_AgainstGeneralFundApplication_WithNoLoanPlan_IsRejected()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, _, generalFundId) = await LoadSeededIdsAsync(client);
        await FundAsync(client, generalFundId, 50000m);

        var application = await CreateApprovedApplicationAsync(client, "20001-2000001-1", categoryId, generalFundId, 5000m);

        var response = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 5000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null, null, null, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Payment_AgainstGeneralFundApplication_AfterLoanAgreementCreated_Succeeds()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, _, generalFundId) = await LoadSeededIdsAsync(client);
        await FundAsync(client, generalFundId, 50000m);

        var application = await CreateApprovedApplicationAsync(client, "20001-2000002-2", categoryId, generalFundId, 5000m);

        var agreement = await LoanTestHelpers.CreateLoanAgreementAsync(client, application.Id, installmentCount: 5);
        Assert.Equal("Active", agreement.Status);
        Assert.Equal(5000m, agreement.PrincipalAmount);

        var response = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 5000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null, null, null, null));

        await ReadOrFailAsync<PaymentDto>(response, HttpStatusCode.Created);
    }

    [Fact]
    public async Task ZakatFundApplication_HasNoLoanSurface_AndPaymentIsNeverGated()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId, _) = await LoadSeededIdsAsync(client);
        await FundAsync(client, zakatFundId, 50000m);

        var application = await CreateApprovedApplicationAsync(client, "20001-2000003-3", categoryId, zakatFundId, 4000m);

        // No loan agreement authored, yet a Zakat-fund payment is never gated.
        var paymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 4000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null, null, null, null));
        await ReadOrFailAsync<PaymentDto>(paymentResponse, HttpStatusCode.Created);

        var scheduleResponse = await client.GetAsync($"/api/loans/by-application/{application.Id}");
        var schedule = await ReadOrFailAsync<LoanScheduleDto>(scheduleResponse, HttpStatusCode.OK);

        Assert.Null(schedule.Agreement);
        Assert.Empty(schedule.Installments);
        Assert.Equal(0m, schedule.PrincipalDisbursed);
    }

    [Fact]
    public async Task ByApplication_ForNonexistentApplication_Returns404()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/loans/by-application/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAgreement_AgainstZakatFundApplication_IsRejected()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId, _) = await LoadSeededIdsAsync(client);
        await FundAsync(client, zakatFundId, 50000m);

        var application = await CreateApprovedApplicationAsync(client, "20001-2000004-4", categoryId, zakatFundId, 4000m);

        var response = await client.PostAsJsonAsync("/api/loans", new CreateLoanAgreementRequest(
            application.Id, 4, DateOnly.FromDateTime(DateTime.UtcNow), "Monthly", null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateAgreement_WhenAlreadyActive_IsRejected()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, _, generalFundId) = await LoadSeededIdsAsync(client);
        await FundAsync(client, generalFundId, 50000m);

        var application = await CreateApprovedApplicationAsync(client, "20001-2000005-5", categoryId, generalFundId, 3000m);
        await LoanTestHelpers.CreateLoanAgreementAsync(client, application.Id, installmentCount: 3);

        var response = await client.PostAsJsonAsync("/api/loans", new CreateLoanAgreementRequest(
            application.Id, 3, DateOnly.FromDateTime(DateTime.UtcNow), "Monthly", null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task UpdateApplication_ChangingApprovedAmount_WhileAgreementActive_IsRejected()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, _, generalFundId) = await LoadSeededIdsAsync(client);
        await FundAsync(client, generalFundId, 50000m);

        // Requested 8000, approved defaults to the requested amount (8000) — the update below asks
        // for a different-but-still-<=-requested approved amount (7000) so the loan-lock guard is
        // what rejects it, not the unrelated approved-amount-exceeds-requested check.
        var application = await CreateApprovedApplicationAsync(client, "20001-2000006-6", categoryId, generalFundId, 8000m);
        await LoanTestHelpers.CreateLoanAgreementAsync(client, application.Id, installmentCount: 6);

        var updateResponse = await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            categoryId, generalFundId, application.RequestedAmount, 7000m, application.Priority, application.Purpose));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, updateResponse.StatusCode);
    }
}
