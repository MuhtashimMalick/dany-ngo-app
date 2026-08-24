using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Donations;
using NgoFund.Contracts.Donors;
using NgoFund.Contracts.Ledgers;
using NgoFund.Contracts.Users;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// <c>GET api/fund-categories/{id}/transaction-ledger/export</c> (CSV + PDF). Reuses
/// <see cref="FundTransactionLedgerTests"/>'s reconciliation guarantees — this only covers what's
/// specific to the export path: full (unpaginated) rows, RFC-4180 CSV escaping, a real PDF byte
/// stream, the derived filename, and the extra <c>reports.export</c> permission gate.
/// </summary>
public class FundLedgerExportTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task Csv_ReturnsTheFullUnpaginatedRowSet_ForAFundWithMoreRowsThanOnePage()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (_, zakatFundId, _) = await LoadSeededIdsAsync(client);

        for (var i = 0; i < 5; i++)
        {
            await FundAsync(client, zakatFundId, 1000m + i);
        }

        // pageSize=2 proves the ledger has more than one page; the export must ignore paging
        // entirely and return every row that matches.
        var pagedFirstPage = await ReadOrFailAsync<PagedResult<FundTransactionLedgerRowDto>>(
            await client.GetAsync($"/api/fund-categories/{zakatFundId}/transaction-ledger?pageSize=2"), HttpStatusCode.OK);
        Assert.True(pagedFirstPage.TotalCount > 2, "Test setup should produce more rows than one page.");
        Assert.Equal(2, pagedFirstPage.Items.Count);

        var response = await client.GetAsync($"/api/fund-categories/{zakatFundId}/transaction-ledger/export?format=csv");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();

        // A UTF-8 BOM must actually be on the wire (Excel needs it to render Urdu/Arabic names) —
        // check the raw bytes rather than trusting a decoded string, since Encoding.GetBytes()
        // silently drops the preamble even when encoderShouldEmitUTF8Identifier is set.
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);

        var csv = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        var lines = csv.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

        // Header row + one row per transaction — the exported row count must match TotalCount, not
        // any single page's Items.Count.
        Assert.Equal(pagedFirstPage.TotalCount + 1, lines.Length);
        Assert.Equal("Date,Category,No.,Reference No.,Type,Name,GRN (PKR),OG (PKR),Total (PKR)", lines[0]);
    }

    [Fact]
    public async Task Csv_EscapesAPartyNameContainingACommaAndADoubleQuote()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (_, zakatFundId, _) = await LoadSeededIdsAsync(client);

        const string donorName = "Big Al, \"The Rock\"";
        var donorResponse = await client.PostAsJsonAsync("/api/donors", new CreateDonorRequest(
            donorName, "Individual", null, null, null, null, null, null, null, null, null, false, null));
        var donor = await ReadOrFailAsync<DonorDto>(donorResponse, HttpStatusCode.Created);

        await ReadOrFailAsync<DonationDto>(await client.PostAsJsonAsync("/api/donations", new CreateDonationRequest(
            donor.Id, zakatFundId, 500m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, null)), HttpStatusCode.Created);

        var response = await client.GetAsync($"/api/fund-categories/{zakatFundId}/transaction-ledger/export?format=csv");
        var csv = await response.Content.ReadAsStringAsync();

        // RFC-4180 round-trip: the comma stays inside the quoted field, and the embedded double
        // quotes are each doubled ("" per ") rather than breaking the field.
        Assert.Contains("\"Received from Big Al, \"\"The Rock\"\"\"", csv);
    }

    [Fact]
    public async Task Pdf_ReturnsNonEmptyPdfBytes()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (_, zakatFundId, _) = await LoadSeededIdsAsync(client);
        await FundAsync(client, zakatFundId, 750m);

        var response = await client.GetAsync($"/api/fund-categories/{zakatFundId}/transaction-ledger/export?format=pdf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
        Assert.True(bytes.Length > 4);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    [Fact]
    public async Task ContentDisposition_FilenameMatchesFromToSlugPattern_FilteredAndUnfiltered()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (_, zakatFundId, _) = await LoadSeededIdsAsync(client);
        await FundAsync(client, zakatFundId, 250m);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var filteredResponse = await client.GetAsync(
            $"/api/fund-categories/{zakatFundId}/transaction-ledger/export?format=csv&fromDate={today:yyyy-MM-dd}&toDate={today:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
        Assert.Equal($"{today:yyyy-MM-dd}_to_{today:yyyy-MM-dd}_zakat_report.csv", GetFileName(filteredResponse));

        var unfilteredResponse = await client.GetAsync($"/api/fund-categories/{zakatFundId}/transaction-ledger/export?format=pdf");
        Assert.Equal(HttpStatusCode.OK, unfilteredResponse.StatusCode);
        // No fromDate/toDate given — the filename is derived from the rows' own date range, which
        // for freshly-created transactions is today on both ends.
        Assert.Equal($"{today:yyyy-MM-dd}_to_{today:yyyy-MM-dd}_zakat_report.pdf", GetFileName(unfilteredResponse));
    }

    [Fact]
    public async Task ViewerRoleOnly_IsForbiddenFromExporting()
    {
        var adminClient = await factory.CreateAuthenticatedClientAsync();
        var (_, zakatFundId, _) = await LoadSeededIdsAsync(adminClient);

        const string viewerEmail = "viewer-export-test@ngofund.local";
        const string viewerPassword = "ViewerPass123!";
        await ReadOrFailAsync<UserSummaryDto>(
            await adminClient.PostAsJsonAsync("/api/users", new CreateUserRequest(viewerEmail, "Export Viewer", null, viewerPassword, ["Viewer"])),
            HttpStatusCode.Created);

        var viewerClient = factory.CreateClient();
        var loginResponse = await viewerClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(viewerEmail, viewerPassword));
        var auth = await ReadOrFailAsync<AuthResponse>(loginResponse, HttpStatusCode.OK);
        viewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Viewer has donations.view/payments.view (every *.view permission) but not reports.export
        // — the on-screen ledger stays visible, the export must not.
        var ledgerResponse = await viewerClient.GetAsync($"/api/fund-categories/{zakatFundId}/transaction-ledger");
        Assert.Equal(HttpStatusCode.OK, ledgerResponse.StatusCode);

        var exportResponse = await viewerClient.GetAsync($"/api/fund-categories/{zakatFundId}/transaction-ledger/export?format=csv");
        Assert.Equal(HttpStatusCode.Forbidden, exportResponse.StatusCode);
    }

    private static string? GetFileName(HttpResponseMessage response) =>
        response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
}
