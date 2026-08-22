using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Ledgers;
using NgoFund.Contracts.Loans;
using NgoFund.Contracts.Payments;
using NgoFund.Contracts.Users;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Per-applicant ledger (read-only aggregation, no schema change): <c>GET api/applicants/{id}/ledger</c>
/// returns each applicant's full financial history across every fund they've touched.
/// </summary>
public class ApplicantLedgerTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    /// <summary>Creates a DataEntryOperator user (no payments.view/loans.view — see RoleSeed.cs) and returns an authenticated client for them, for the permission-rejection test.</summary>
    private async Task<HttpClient> CreateLimitedPermissionClientAsync(HttpClient adminClient)
    {
        const string password = "TempPass123!";
        var email = $"deo-{Guid.NewGuid():N}@ngofund.local";

        var createResponse = await adminClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            email, "Ledger Test DEO", null, password, ["DataEntryOperator"]));
        await ReadOrFailAsync<UserSummaryDto>(createResponse, HttpStatusCode.Created);

        var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var auth = await ReadOrFailAsync<AuthResponse>(loginResponse, HttpStatusCode.OK);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    /// <summary>
    /// D3: a voided payment and a voided repayment must never count toward any total, but must
    /// still show up as history in the individual applicant ledger with their
    /// own status. One application carries a kept payment/repayment pair (to prove the surviving
    /// money still counts) alongside a voided payment and a voided repayment (to prove the voided
    /// ones don't) — voiding the second payment before ever repaying against it, so the repayment
    /// cap trigger's view of disbursed principal is never in question.
    /// </summary>
    [Fact]
    public async Task VoidedPaymentAndRepayment_ExcludedFromTotals_ButVisibleAsEntries()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, _, generalFundId) = await LoadSeededIdsAsync(client);
        await FundAsync(client, generalFundId, 50000m);

        const string cnic = "40001-4000003-3";
        var application = await CreateApprovedApplicationAsync(client, cnic, categoryId, generalFundId, 5000m);
        var agreement = await LoanTestHelpers.CreateLoanAgreementAsync(client, application.Id, installmentCount: 2);

        var keptPaymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 3000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null, null, null, null));
        await ReadOrFailAsync<PaymentDto>(keptPaymentResponse, HttpStatusCode.Created);

        var voidedPaymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 2000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null, null, null, null));
        var voidedPayment = await ReadOrFailAsync<PaymentDto>(voidedPaymentResponse, HttpStatusCode.Created);
        var voidPaymentResponse = await client.PostAsJsonAsync($"/api/payments/{voidedPayment.Id}/void", new VoidPaymentRequest("Entered by mistake"));
        Assert.Equal(HttpStatusCode.NoContent, voidPaymentResponse.StatusCode);

        var keptRepayment = await LoanTestHelpers.RecordRepaymentAsync(client, agreement.Id, 500m);
        var voidedRepayment = await LoanTestHelpers.RecordRepaymentAsync(client, agreement.Id, 300m);
        var voidRepaymentResponse = await client.PostAsJsonAsync($"/api/loans/repayments/{voidedRepayment.Id}/void", new VoidLoanRepaymentRequest("Entered by mistake"));
        Assert.Equal(HttpStatusCode.NoContent, voidRepaymentResponse.StatusCode);

        var ledger = await ReadOrFailAsync<ApplicantLedgerDto>(
            await client.GetAsync($"/api/applicants/{application.ApplicantId}/ledger"), HttpStatusCode.OK);

        var fundRow = Assert.Single(ledger.FundSummaries, r => r.FundCategoryId == generalFundId);
        Assert.Equal(3000m, fundRow.TotalReceived); // voided 2000 payment excluded
        Assert.Equal(500m, fundRow.TotalRepaid); // voided 300 repayment excluded
        Assert.Equal(2500m, fundRow.OutstandingRecoverable); // 3000 disbursed - 500 repaid
        Assert.Equal("Outstanding", fundRow.RecoveryStatus);

        Assert.Equal(4, ledger.Entries.Count);
        Assert.Contains(ledger.Entries, e => e.EntryType == "Disbursement" && e.Amount == 3000m && e.Status == "Completed");
        Assert.Contains(ledger.Entries, e => e.EntryType == "Disbursement" && e.Amount == 2000m && e.Status == "Voided");
        Assert.Contains(ledger.Entries, e => e.EntryType == "Repayment" && e.Amount == 500m && e.Status == "Completed");
        Assert.Contains(ledger.Entries, e => e.EntryType == "Repayment" && e.Amount == 300m && e.Status == "Voided");
        Assert.Equal(keptRepayment.RepaymentNumber, ledger.Entries.Single(e => e.Amount == 500m).ReferenceNumber);
    }

    [Fact]
    public async Task RejectsCallerLackingPermission()
    {
        var adminClient = await factory.CreateAuthenticatedClientAsync();
        var limitedClient = await CreateLimitedPermissionClientAsync(adminClient);

        var applicantLedgerResponse = await limitedClient.GetAsync($"/api/applicants/{Guid.NewGuid()}/ledger");
        Assert.Equal(HttpStatusCode.Forbidden, applicantLedgerResponse.StatusCode);
    }
}
