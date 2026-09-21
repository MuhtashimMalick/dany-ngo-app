using System.Net;
using System.Net.Http.Json;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.ApplicationCategories;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.FundCategories;
using NgoFund.Contracts.Payments;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Item 2 (2026-09 feedback round 3): <c>Purpose</c> is now a required field on both create and
/// update — enforced by FluentValidation (<c>ValidateAndThrowAsync</c>), which
/// <c>ValidationExceptionHandler</c> maps to 400 (not 422 -- that status is reserved for
/// domain-layer <c>DomainException</c>s via <c>DomainExceptionHandler</c>; see e.g. the existing
/// <c>Rozgar_GuarantorsWithNamesButNoCnicOrMembershipNumber_RejectedAtSaveTime_NotAtApproval</c>
/// test for the same 400-for-FluentValidation convention). Item 3:
/// <c>ApplicationDto.ActiveApplicationCount</c> — no prior test asserted its actual value (only
/// that the endpoints didn't error), so this locks in the count across a Pending/Rejected/Paid
/// lifecycle. Own class/fixture for the shared login-rate-limit budget (see
/// <see cref="StatusTransitionInvariantTests"/>).
/// </summary>
public class ApplicationFieldRequirementsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static async Task<(Guid OtherCategoryId, Guid ZakatFundId)> LoadIdsAsync(HttpClient client)
    {
        var categories = (await client.GetFromJsonAsync<List<ApplicationCategoryDto>>("/api/application-categories"))!;
        var funds = (await client.GetFromJsonAsync<List<FundCategoryDto>>("/api/fund-categories"))!;
        return (categories.Single(c => c.Code == "OTHER").Id, funds.Single(f => f.Code == "ZAKAT").Id);
    }

    private static async Task<ApplicantDto> CreateApplicantAsync(HttpClient client, string cnic)
    {
        var response = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "Field Requirements Test Applicant", null, cnic, "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        return await ReadOrFailAsync<ApplicantDto>(response, HttpStatusCode.Created);
    }

    [Theory]
    [InlineData(null, "1")]
    [InlineData("", "2")]
    [InlineData("   ", "3")]
    public async Task CreateApplication_WithMissingPurpose_Returns400(string? purpose, string cnicSuffix)
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId) = await LoadIdsAsync(client);
        var applicant = await CreateApplicantAsync(client, $"60102-111100{cnicSuffix}-{cnicSuffix}");

        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, categoryId, zakatFundId, 5000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), purpose));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateApplication_WithMissingPurpose_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId) = await LoadIdsAsync(client);
        var applicant = await CreateApplicantAsync(client, "60102-2222222-1");

        var createResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, categoryId, zakatFundId, 5000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test"));
        var application = await ReadOrFailAsync<ApplicationDto>(createResponse, HttpStatusCode.Created);

        var updateResponse = await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            categoryId, zakatFundId, 5000m, null, "Normal", Purpose: null));

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    [Fact]
    public async Task ActiveApplicationCount_TracksLifecycle_ExcludingRejectedAndPaid()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId) = await LoadIdsAsync(client);
        await FundAsync(client, zakatFundId, 50000m);
        var applicant = await CreateApplicantAsync(client, "60102-3333333-1");

        var app1Response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, categoryId, zakatFundId, 5000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test"));
        var app1 = await ReadOrFailAsync<ApplicationDto>(app1Response, HttpStatusCode.Created);

        var app2Response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, categoryId, zakatFundId, 5000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test"));
        var app2 = await ReadOrFailAsync<ApplicationDto>(app2Response, HttpStatusCode.Created);

        // Both Pending (active): count includes the fetched application itself plus its sibling.
        var app1AfterCreate = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{app1.Id}"), HttpStatusCode.OK);
        Assert.Equal(2, app1AfterCreate.ActiveApplicationCount);

        // Reject app1 (a terminal status) — it must drop out of the count.
        var rejectResponse = await client.PostAsJsonAsync($"/api/applications/{app1.Id}/status",
            new ChangeApplicationStatusRequest("Rejected", null, "Does not qualify"));
        Assert.Equal(HttpStatusCode.NoContent, rejectResponse.StatusCode);

        var app2AfterReject = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{app2.Id}"), HttpStatusCode.OK);
        Assert.Equal(1, app2AfterReject.ActiveApplicationCount);

        // Drive app2 to Paid (the other terminal status) — it must drop out of the count too.
        await client.PostAsJsonAsync($"/api/applications/{app2.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        await client.PostAsJsonAsync($"/api/applications/{app2.Id}/status", new ChangeApplicationStatusRequest("Approved", null, null, 5000m));
        var paymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            app2.Id, 5000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null));
        await ReadOrFailAsync<PaymentDto>(paymentResponse, HttpStatusCode.Created);

        var app2AfterPaid = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{app2.Id}"), HttpStatusCode.OK);
        Assert.Equal("Paid", app2AfterPaid.Status);
        Assert.Equal(0, app2AfterPaid.ActiveApplicationCount);
    }
}
