using System.Net;
using System.Net.Http.Json;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Payments;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the Payments list endpoint's new status/method/fund-category/date-range filters (added
/// alongside <see cref="ApplicationSearchTests"/>'s equivalent for Applications) actually narrow
/// the result set rather than returning everything, and that an unparseable <c>status</c> value
/// is silently ignored rather than rejected — mirroring how the Applications endpoint already
/// treats its own <c>Enum.TryParse</c> guard.
/// </summary>
public class PaymentSearchTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task FilterByStatusAndMethod_ExcludesNonMatchingPayments()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId, _) = await LoadSeededIdsAsync(client);
        await FundAsync(client, zakatFundId, 50000m);

        var cashApplication = await CreateApprovedApplicationAsync(client, "13001-1300001-1", categoryId, zakatFundId, 3000m);
        var cashPaymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            cashApplication.Id, 3000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null, null, null, null));
        var cashPayment = await ReadOrFailAsync<PaymentDto>(cashPaymentResponse, HttpStatusCode.Created);

        var bankApplication = await CreateApprovedApplicationAsync(client, "13001-1300002-2", categoryId, zakatFundId, 3000m);
        var bankPaymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            bankApplication.Id, 3000m, DateOnly.FromDateTime(DateTime.UtcNow), "BankTransfer", null, "Test Bank", null, null, null, null));
        var bankPayment = await ReadOrFailAsync<PaymentDto>(bankPaymentResponse, HttpStatusCode.Created);

        var voidResponse = await client.PostAsJsonAsync($"/api/payments/{bankPayment.Id}/void", new VoidPaymentRequest("Entered by mistake"));
        Assert.Equal(HttpStatusCode.NoContent, voidResponse.StatusCode);

        var voidedFilterResponse = await client.GetAsync("/api/payments?status=Voided");
        var voidedPage = await ReadOrFailAsync<PagedResult<PaymentDto>>(voidedFilterResponse, HttpStatusCode.OK);
        Assert.Contains(voidedPage.Items, p => p.Id == bankPayment.Id);
        Assert.DoesNotContain(voidedPage.Items, p => p.Id == cashPayment.Id);

        var cashFilterResponse = await client.GetAsync("/api/payments?paymentMethod=Cash");
        var cashPage = await ReadOrFailAsync<PagedResult<PaymentDto>>(cashFilterResponse, HttpStatusCode.OK);
        Assert.Contains(cashPage.Items, p => p.Id == cashPayment.Id);
        Assert.DoesNotContain(cashPage.Items, p => p.Id == bankPayment.Id);

        // An unparseable status must be silently ignored (return unfiltered results), not 400.
        var garbageFilterResponse = await client.GetAsync("/api/payments?status=NotARealStatus");
        var garbagePage = await ReadOrFailAsync<PagedResult<PaymentDto>>(garbageFilterResponse, HttpStatusCode.OK);
        Assert.Contains(garbagePage.Items, p => p.Id == cashPayment.Id);
        Assert.Contains(garbagePage.Items, p => p.Id == bankPayment.Id);
    }

    [Fact]
    public async Task FilterByFundCategoryAndDateRange_ExcludesNonMatchingPayments()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId, generalFundId) = await LoadSeededIdsAsync(client);
        await FundAsync(client, zakatFundId, 50000m);
        await FundAsync(client, generalFundId, 50000m);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-2);
        var recentDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var zakatApplication = await CreateApprovedApplicationAsync(client, "13001-1300003-3", categoryId, zakatFundId, 1500m);
        var zakatPaymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            zakatApplication.Id, 1500m, oldDate, "Cash", null, null, null, null, null, null));
        var zakatPayment = await ReadOrFailAsync<PaymentDto>(zakatPaymentResponse, HttpStatusCode.Created);

        var generalApplication = await CreateApprovedApplicationAsync(client, "13001-1300004-4", categoryId, generalFundId, 1500m);
        await LoanTestHelpers.CreateLoanAgreementAsync(client, generalApplication.Id, installmentCount: 1); // General-fund payments require a loan plan (M7)
        var generalPaymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            generalApplication.Id, 1500m, recentDate, "Cash", null, null, null, null, null, null));
        var generalPayment = await ReadOrFailAsync<PaymentDto>(generalPaymentResponse, HttpStatusCode.Created);

        var url = $"/api/payments?fundCategoryId={zakatFundId}&dateFrom={oldDate.AddDays(-1):yyyy-MM-dd}&dateTo={oldDate.AddDays(1):yyyy-MM-dd}";
        var response = await client.GetAsync(url);
        var page = await ReadOrFailAsync<PagedResult<PaymentDto>>(response, HttpStatusCode.OK);

        Assert.Contains(page.Items, p => p.Id == zakatPayment.Id);
        Assert.DoesNotContain(page.Items, p => p.Id == generalPayment.Id);
    }
}
