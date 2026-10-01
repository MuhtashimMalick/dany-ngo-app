using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.ApplicationCategories;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.FundCategories;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Item 1 (2026-09 feedback round 3): the approved amount is now an explicit reviewer input on the
/// Approved transition, never silently defaulted from <c>RequestedAmount</c>
/// (<c>entity.ApprovedAmount ??= entity.RequestedAmount</c> was the old, removed behaviour). Every
/// OTHER test in the suite that reaches Approved already supplies an explicit amount, so it would
/// stay green even if that silent default were reinstated — <see cref="ApproveTransition_WithNoAmountSuppliedAndNoneOnFile_Returns422"/>
/// and <see cref="ReApproveFromOnHold_WithNoAmountSupplied_KeepsExistingApprovedAmount"/> are written
/// specifically to fail in that scenario. Also covers the v1.8 correction (2026-09-16): the requested
/// amount is not a ceiling — a committee of elders may approve less than, equal to, or more than what
/// was requested, so <see cref="ApproveTransition_WithAmountAboveRequestedAmount_IsAllowed"/> asserts
/// an above-requested approval succeeds and persists. Kept in its own class/fixture for the shared
/// login-rate-limit budget (see <see cref="StatusTransitionInvariantTests"/>).
/// </summary>
public class ApprovedAmountWorkflowTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static async Task<(Guid OtherCategoryId, Guid ZakatFundId)> LoadIdsAsync(HttpClient client)
    {
        var categories = (await client.GetFromJsonAsync<List<ApplicationCategoryDto>>("/api/application-categories"))!;
        var funds = (await client.GetFromJsonAsync<List<FundCategoryDto>>("/api/fund-categories"))!;
        // OTHER: no completeness manifest and no guarantor requirement, so nothing but the
        // approved-amount rule itself can block the Approved transition in these tests.
        return (categories.Single(c => c.Code == "OTHER").Id, funds.Single(f => f.Code == "ZAKAT").Id);
    }

    private static async Task<ApplicationDto> CreateApplicationAsync(HttpClient client, string cnic, Guid categoryId, Guid fundId, decimal requestedAmount)
    {
        var applicantResponse = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "Approved Amount Test Applicant", null, cnic, "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        var applicant = await ReadOrFailAsync<ApplicantDto>(applicantResponse, HttpStatusCode.Created);

        var createResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, categoryId, fundId, requestedAmount, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test"));
        var application = await ReadOrFailAsync<ApplicationDto>(createResponse, HttpStatusCode.Created);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));
        // Feedback round 3, item I: OTHER now has a real completeness manifest.
        await ApplicationCompletenessTestHelpers.CompleteOtherDocumentsAsync(client, application.Id);
        return application;
    }

    [Fact]
    public async Task ApproveTransition_WithNoAmountSuppliedAndNoneOnFile_Returns422()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId) = await LoadIdsAsync(client);
        var application = await CreateApplicationAsync(client, "60101-1111111-1", categoryId, zakatFundId, 10000m);

        // ApprovedAmount omitted (null) on a first-time approval, with none already on file — if
        // the old `entity.ApprovedAmount ??= entity.RequestedAmount` silent default were reinstated,
        // this would succeed with NoContent instead of 422.
        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ApproveTransition_WithAmountAboveRequestedAmount_IsAllowed()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId) = await LoadIdsAsync(client);
        var application = await CreateApplicationAsync(client, "60101-1111111-2", categoryId, zakatFundId, 10000m);

        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null, 10000.01m));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var after = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
        Assert.Equal("Approved", after.Status);
        Assert.Equal(10000.01m, after.ApprovedAmount);
    }

    [Fact]
    public async Task ReApproveFromOnHold_WithNoAmountSupplied_KeepsExistingApprovedAmount()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId) = await LoadIdsAsync(client);
        var application = await CreateApplicationAsync(client, "60101-1111111-3", categoryId, zakatFundId, 10000m);

        var firstApproval = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null, 7500m));
        Assert.Equal(HttpStatusCode.NoContent, firstApproval.StatusCode);

        var onHold = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("OnHold", "Pausing for review", null));
        Assert.Equal(HttpStatusCode.NoContent, onHold.StatusCode);

        // Re-approving with ApprovedAmount omitted must be legal here (an amount already exists on
        // the entity) and must keep 7500m, not fall back to the 10000m RequestedAmount.
        var reApproval = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null));
        Assert.Equal(HttpStatusCode.NoContent, reApproval.StatusCode);

        var after = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
        Assert.Equal("Approved", after.Status);
        Assert.Equal(7500m, after.ApprovedAmount);
    }

    /// <summary>D2 regression: the service used to append an "(Approved amount: N.NN.)" suffix onto
    /// the reviewer's own Remarks before saving to <c>application_status_history.remarks</c>
    /// (<c>varchar(1000)</c>). A max-length (1000-char, the validator's own cap) Remarks value would
    /// then overflow the column and raise a raw Npgsql 22001 error (HTTP 500) on an otherwise-valid
    /// approval. The fix stores Remarks as-is.</summary>
    [Fact]
    public async Task ApproveTransition_WithMaxLengthRemarks_DoesNotOverflowColumn()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId) = await LoadIdsAsync(client);
        var application = await CreateApplicationAsync(client, "60101-1111111-4", categoryId, zakatFundId, 10000m);

        var maxLengthRemarks = new string('x', 1000);
        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", maxLengthRemarks, null, 7500m));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>D1 regression: clearing ApprovedAmount on an already-Approved application (zero
    /// payments made yet) used to raise <c>ApprovedAmountBelowCompletedPaymentsException</c>, whose
    /// message ("Cannot clear the approved amount: 0.00 has already been paid...") is simply untrue
    /// when nothing has been paid. The message must now accurately describe why clearing is
    /// disallowed, without referencing a completed-payments total.</summary>
    [Fact]
    public async Task UpdateApplication_ClearingApprovedAmountAfterApproval_ReturnsAccurateMessage()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId) = await LoadIdsAsync(client);
        var application = await CreateApplicationAsync(client, "60101-1111111-5", categoryId, zakatFundId, 10000m);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null, 7500m));

        var updateResponse = await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            categoryId, zakatFundId, 10000m, null, "Normal", "Test"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, updateResponse.StatusCode);
        var problem = JsonSerializer.Deserialize<JsonElement>(await updateResponse.Content.ReadAsStringAsync());
        var detail = problem.GetProperty("detail").GetString();
        Assert.DoesNotContain("has already been paid", detail);
        Assert.Contains("cleared", detail);
    }

    /// <summary>v1.8 correction (2026-09-16): the requested amount is not a ceiling on the PUT path
    /// either — a committee of elders may approve more than what was requested. Uses a Zakat-eligible
    /// category/fund (not ROZGAR/General) so no loan agreement is created, keeping the unrelated
    /// loan-lock guard in <c>UpdateAsync</c> out of the way.</summary>
    [Fact]
    public async Task UpdateApplication_WithApprovedAmountAboveRequestedAmount_IsAllowed()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId) = await LoadIdsAsync(client);
        var application = await CreateApplicationAsync(client, "60101-1111111-6", categoryId, zakatFundId, 10000m);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null, 7500m));

        var updateResponse = await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            categoryId, zakatFundId, 10000m, 12500m, "Normal", "Test"));

        var updated = await ReadOrFailAsync<ApplicationDto>(updateResponse, HttpStatusCode.OK);
        Assert.Equal(12500m, updated.ApprovedAmount);
    }
}
