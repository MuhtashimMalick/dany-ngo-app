using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NgoFund.Contracts.ApplicationCategories;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Donations;
using NgoFund.Contracts.Donors;
using NgoFund.Contracts.Ledgers;
using NgoFund.Contracts.Payments;
using NgoFund.Infrastructure.Persistence;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// A5: the chronological, running-balance fund ledger
/// (<c>GET api/fund-categories/{id}/transaction-ledger</c>). The mandatory correctness property:
/// the running balance after the last row must equal <c>vw_fund_balances.balance</c> for the same
/// fund exactly, because the feed is a full projection of <c>fund_transactions</c> — every
/// reference type, including reversals — never a <c>donations</c>/<c>payments</c> UNION. See
/// <see cref="NgoFund.Infrastructure.Services.FundTransactionLedgerService"/>.
/// </summary>
public class FundTransactionLedgerTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private async Task<decimal> GetViewBalanceAsync(Guid fundCategoryId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database.SqlQuery<decimal>(
            $"""SELECT balance AS "Value" FROM vw_fund_balances WHERE fund_category_id = {fundCategoryId}""")
            .SingleAsync();
    }

    [Fact]
    public async Task Ledger_ReconcilesExactlyWithVwFundBalances_IncludingReversalsAndPagination()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (healthCategoryId, zakatFundId, _) = await LoadSeededIdsAsync(client);
        var healthCategory = (await client.GetFromJsonAsync<List<ApplicationCategoryDto>>("/api/application-categories"))!
            .Single(c => c.Id == healthCategoryId);

        var donor1 = (await (await client.PostAsJsonAsync("/api/donors", new CreateDonorRequest(
            "Ledger Test Donor One", "Individual", null, null, null, null, null, null, null, null, null, false, null)))
            .Content.ReadFromJsonAsync<DonorDto>())!;
        var donor2 = (await (await client.PostAsJsonAsync("/api/donors", new CreateDonorRequest(
            "Ledger Test Donor Two", "Individual", null, null, null, null, null, null, null, null, null, false, null)))
            .Content.ReadFromJsonAsync<DonorDto>())!;

        var donation1 = await ReadOrFailAsync<DonationDto>(await client.PostAsJsonAsync("/api/donations", new CreateDonationRequest(
            donor1.Id, zakatFundId, 20000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null)), HttpStatusCode.Created);
        var donation2 = await ReadOrFailAsync<DonationDto>(await client.PostAsJsonAsync("/api/donations", new CreateDonationRequest(
            donor2.Id, zakatFundId, 15000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null)), HttpStatusCode.Created);

        var application1 = await CreateApprovedApplicationAsync(client, "70110-1111111-1", healthCategoryId, zakatFundId, 3000m);
        var payment1 = await ReadOrFailAsync<PaymentDto>(await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application1.Id, 3000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null)), HttpStatusCode.Created);

        var application2 = await CreateApprovedApplicationAsync(client, "70110-1111111-2", healthCategoryId, zakatFundId, 2000m);
        var payment2 = await ReadOrFailAsync<PaymentDto>(await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application2.Id, 2000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null)), HttpStatusCode.Created);

        // Reversal rows: one voided donation, one voided payment — must appear as their own rows,
        // never filtered out, since that's what keeps the running balance reconcilable.
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/donations/{donation2.Id}/void", new VoidDonationRequest("Test void"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/payments/{payment2.Id}/void", new VoidPaymentRequest("Test void"))).StatusCode);

        var unpaged = await ReadOrFailAsync<PagedResult<FundTransactionLedgerRowDto>>(
            await client.GetAsync($"/api/fund-categories/{zakatFundId}/transaction-ledger?pageSize=100"), HttpStatusCode.OK);

        // 2 donations + 2 payments + 1 donation reversal + 1 payment reversal = 6 rows.
        Assert.Equal(6, unpaged.TotalCount);
        Assert.Equal(6, unpaged.Items.Count);

        var viewBalance = await GetViewBalanceAsync(zakatFundId);
        Assert.Equal(viewBalance, unpaged.Items[^1].RunningBalance);

        decimal expectedRunning = 0m;
        foreach (var row in unpaged.Items)
        {
            Assert.True(row.AmountIn is not null ^ row.AmountOut is not null, "Exactly one of AmountIn/AmountOut must be set.");
            expectedRunning += row.AmountIn ?? 0m;
            expectedRunning -= row.AmountOut ?? 0m;
            Assert.Equal(expectedRunning, row.RunningBalance);
        }

        for (var i = 1; i < unpaged.Items.Count; i++)
        {
            Assert.True(unpaged.Items[i].TransactionDate >= unpaged.Items[i - 1].TransactionDate);
        }

        // Page 2 must carry the SAME running balances the unpaged fetch had for those rows — proves
        // the window function ran over the whole history before OFFSET/LIMIT were applied.
        var page2 = await ReadOrFailAsync<PagedResult<FundTransactionLedgerRowDto>>(
            await client.GetAsync($"/api/fund-categories/{zakatFundId}/transaction-ledger?page=2&pageSize=2"), HttpStatusCode.OK);
        Assert.Equal(
            unpaged.Items.Skip(2).Take(2).Select(r => r.RunningBalance),
            page2.Items.Select(r => r.RunningBalance));

        var donationRow = unpaged.Items.Single(r => r.ReferenceNumber == donation1.DonationNumber);
        Assert.Contains(donor1.FullName, donationRow.PartyName);
        Assert.Null(donationRow.CategoryName);

        var paymentRow = unpaged.Items.Single(r => r.ReferenceNumber == payment1.PaymentNumber);
        Assert.Equal(healthCategory.Name, paymentRow.CategoryName);
        Assert.Equal(application1.ApplicationNumber, paymentRow.CaseNumber);

        // --- Search filter (client feedback: transaction-ledger Search was silently ignored) ---

        // Searching by a donor's name returns only that donor's donation row(s).
        var byDonorName = await ReadOrFailAsync<PagedResult<FundTransactionLedgerRowDto>>(
            await client.GetAsync($"/api/fund-categories/{zakatFundId}/transaction-ledger?pageSize=100&search={Uri.EscapeDataString(donor1.FullName)}"),
            HttpStatusCode.OK);
        Assert.All(byDonorName.Items, r => Assert.Contains(donor1.FullName, r.PartyName));
        Assert.Contains(byDonorName.Items, r => r.ReferenceNumber == donation1.DonationNumber);

        // Searching by an application number returns only that application's payment row(s).
        var byApplicationNumber = await ReadOrFailAsync<PagedResult<FundTransactionLedgerRowDto>>(
            await client.GetAsync($"/api/fund-categories/{zakatFundId}/transaction-ledger?pageSize=100&search={Uri.EscapeDataString(application1.ApplicationNumber)}"),
            HttpStatusCode.OK);
        Assert.All(byApplicationNumber.Items, r => Assert.Equal(application1.ApplicationNumber, r.CaseNumber));
        Assert.Contains(byApplicationNumber.Items, r => r.ReferenceNumber == payment1.PaymentNumber);

        // The critical property: a searched row's RunningBalance must equal that same row's
        // RunningBalance in the unfiltered fetch — proving the search predicate was applied AFTER
        // the running-balance window, never inside it (which would silently turn RunningBalance into
        // a partial sum over just the matching subset).
        var searchedPaymentRow = byApplicationNumber.Items.Single(r => r.ReferenceNumber == payment1.PaymentNumber);
        var unpagedPaymentRow = unpaged.Items.Single(r => r.ReferenceNumber == payment1.PaymentNumber);
        Assert.Equal(unpagedPaymentRow.RunningBalance, searchedPaymentRow.RunningBalance);

        // TotalCount must match the number of rows a filtered fetch actually returns.
        Assert.Equal(byApplicationNumber.Items.Count, byApplicationNumber.TotalCount);

        // A search term matching nothing returns an empty page with TotalCount == 0, not an error.
        var noMatches = await ReadOrFailAsync<PagedResult<FundTransactionLedgerRowDto>>(
            await client.GetAsync($"/api/fund-categories/{zakatFundId}/transaction-ledger?search=no-such-party-name-xyz"),
            HttpStatusCode.OK);
        Assert.Empty(noMatches.Items);
        Assert.Equal(0, noMatches.TotalCount);
    }
}
