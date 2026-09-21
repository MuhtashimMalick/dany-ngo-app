using System.Net;
using System.Net.Http.Json;
using NgoFund.Contracts.FundCategories;
using NgoFund.Contracts.Loans;
using NgoFund.Contracts.Payments;
using NgoFund.Contracts.Reports;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

public class LoanRepaymentLedgerTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static async Task<FundBalanceDto> GetFundBalanceAsync(HttpClient client, string code)
    {
        var balances = (await client.GetFromJsonAsync<List<FundBalanceDto>>("/api/fund-categories/balances", JsonOptions))!;
        return balances.Single(b => b.Code == code);
    }

    [Fact]
    public async Task RecordRepayment_IncreasesBalanceAndTotalRepaid_LeavesTotalCollectedAndDonationReportsUnchanged()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, _, generalFundId) = await LoadSeededIdsAsync(client);
        await FundAsync(client, generalFundId, 50000m);

        var application = await CreateApprovedApplicationAsync(client, "30001-3000001-1", categoryId, generalFundId, 4000m);
        var agreement = await LoanTestHelpers.CreateLoanAgreementAsync(client, application.Id, installmentCount: 4);

        var paymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 4000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null));
        await ReadOrFailAsync<PaymentDto>(paymentResponse, HttpStatusCode.Created);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var beforeBalance = await GetFundBalanceAsync(client, "GENERAL");
        var donationsBefore = (await client.GetFromJsonAsync<List<MonthlySummaryRowDto>>(
            $"/api/reports/donations/monthly?from={today.AddMonths(-1):yyyy-MM-dd}&to={today.AddMonths(1):yyyy-MM-dd}", JsonOptions))!;

        var repayment = await LoanTestHelpers.RecordRepaymentAsync(client, agreement.Id, 1000m);
        Assert.Equal("Completed", repayment.Status);

        var afterBalance = await GetFundBalanceAsync(client, "GENERAL");
        var donationsAfter = (await client.GetFromJsonAsync<List<MonthlySummaryRowDto>>(
            $"/api/reports/donations/monthly?from={today.AddMonths(-1):yyyy-MM-dd}&to={today.AddMonths(1):yyyy-MM-dd}", JsonOptions))!;

        Assert.Equal(beforeBalance.Balance + 1000m, afterBalance.Balance);
        Assert.Equal(beforeBalance.TotalRepaid + 1000m, afterBalance.TotalRepaid);
        Assert.Equal(beforeBalance.TotalCollected, afterBalance.TotalCollected); // a repayment is NOT a donation
        Assert.Equal(donationsBefore.Sum(d => d.Total), donationsAfter.Sum(d => d.Total)); // donation reports untouched

        var loanRepaymentReport = (await client.GetFromJsonAsync<List<MonthlySummaryRowDto>>(
            $"/api/reports/loan-repayments?from={today.AddMonths(-1):yyyy-MM-dd}&to={today.AddMonths(1):yyyy-MM-dd}", JsonOptions))!;
        Assert.Contains(loanRepaymentReport, r => r.FundCategoryName == "General Fund" && r.Total >= 1000m);
    }

    [Fact]
    public async Task VoidRepayment_PostsReversal_RestoresOutstandingBalance_AndBlocksDoubleVoid()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, _, generalFundId) = await LoadSeededIdsAsync(client);
        await FundAsync(client, generalFundId, 50000m);

        var application = await CreateApprovedApplicationAsync(client, "30001-3000002-2", categoryId, generalFundId, 4000m);
        var agreement = await LoanTestHelpers.CreateLoanAgreementAsync(client, application.Id, installmentCount: 4);

        var paymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 4000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null));
        await ReadOrFailAsync<PaymentDto>(paymentResponse, HttpStatusCode.Created);

        var repayment = await LoanTestHelpers.RecordRepaymentAsync(client, agreement.Id, 1000m);

        var scheduleAfterRepayment = await ReadOrFailAsync<LoanScheduleDto>(
            await client.GetAsync($"/api/loans/by-application/{application.Id}"), HttpStatusCode.OK);
        Assert.Equal(3000m, scheduleAfterRepayment.OutstandingBalance);

        var beforeVoidBalance = await GetFundBalanceAsync(client, "GENERAL");

        var voidResponse = await client.PostAsJsonAsync($"/api/loans/repayments/{repayment.Id}/void", new VoidLoanRepaymentRequest("Entered by mistake"));
        Assert.Equal(HttpStatusCode.NoContent, voidResponse.StatusCode);

        var afterVoidBalance = await GetFundBalanceAsync(client, "GENERAL");
        Assert.Equal(beforeVoidBalance.Balance - 1000m, afterVoidBalance.Balance);
        Assert.Equal(beforeVoidBalance.TotalRepaid - 1000m, afterVoidBalance.TotalRepaid);

        var scheduleAfterVoid = await ReadOrFailAsync<LoanScheduleDto>(
            await client.GetAsync($"/api/loans/by-application/{application.Id}"), HttpStatusCode.OK);
        Assert.Equal(4000m, scheduleAfterVoid.OutstandingBalance);

        var secondVoidResponse = await client.PostAsJsonAsync($"/api/loans/repayments/{repayment.Id}/void", new VoidLoanRepaymentRequest("Trying again"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondVoidResponse.StatusCode);
    }
}
