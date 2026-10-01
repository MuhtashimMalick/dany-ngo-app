using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.ApplicationCategories;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Documents;
using NgoFund.Contracts.FundCategories;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the M3 application workflow end to end: the Zakat rule at creation time, the status
/// state machine, remarks, and the document upload/download round trip — all through the real
/// HTTP API against a real, freshly migrated Postgres.
/// </summary>
public class ApplicationWorkflowTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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

    private static async Task<ApplicantDto> CreateApplicantAsync(HttpClient client, string cnic)
    {
        var response = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "Test Applicant", null, cnic, "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        return await ReadOrFailAsync<ApplicantDto>(response, HttpStatusCode.Created);
    }

    private static async Task<(Guid RozgarId, Guid HealthId, Guid ZakatFundId, Guid GeneralFundId)> LoadSeededIdsAsync(HttpClient client)
    {
        var categories = (await client.GetFromJsonAsync<List<ApplicationCategoryDto>>("/api/application-categories"))!;
        var funds = (await client.GetFromJsonAsync<List<FundCategoryDto>>("/api/fund-categories"))!;

        return (
            categories.Single(c => c.Code == "ROZGAR").Id,
            categories.Single(c => c.Code == "HEALTH").Id,
            funds.Single(f => f.Code == "ZAKAT").Id,
            funds.Single(f => f.Code == "GENERAL").Id);
    }

    [Fact]
    public async Task CreateApplication_NonZakatEligibleCategory_AgainstZakatFund_IsRejected()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "11111-1111111-1");
        var (rozgarId, _, zakatFundId, _) = await LoadSeededIdsAsync(client);

        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, rozgarId, zakatFundId, 10000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task FullWorkflow_Create_Review_Approve_WithRemarks()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "22222-2222222-2");
        var (_, healthId, zakatFundId, _) = await LoadSeededIdsAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, healthId, zakatFundId, 15000m, "High", DateOnly.FromDateTime(DateTime.UtcNow), "Medical treatment"));
        var application = await ReadOrFailAsync<ApplicationDto>(createResponse, HttpStatusCode.Created);
        Assert.Equal("Pending", application.Status);

        // Pending -> UnderReview
        var reviewResponse = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("UnderReview", "Looks legitimate", null));
        Assert.Equal(HttpStatusCode.NoContent, reviewResponse.StatusCode);

        // Feedback round 3, item I: HEALTH now has a real completeness manifest (Google Form
        // intake's A9) — the Approved transition below 422s without it.
        await ApplicationCompletenessTestHelpers.CompleteHealthDetailsAsync(client, application.Id);

        // UnderReview -> Approved
        var approveResponse = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", "Approved in full", null, 15000m));
        Assert.Equal(HttpStatusCode.NoContent, approveResponse.StatusCode);

        var afterApproval = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
        Assert.Equal("Approved", afterApproval.Status);
        Assert.Equal(15000m, afterApproval.ApprovedAmount); // item 1 (2026-09 feedback): explicitly supplied, never silently defaulted

        // Approved -> Pending is not a valid transition
        var invalidResponse = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Pending", null, null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidResponse.StatusCode);

        // history should have 3 rows: created (Pending), -> UnderReview, -> Approved
        var history = (await client.GetFromJsonAsync<List<ApplicationStatusHistoryDto>>($"/api/applications/{application.Id}/history"))!;
        Assert.Equal(3, history.Count);

        // remarks
        var remarkResponse = await client.PostAsJsonAsync($"/api/applications/{application.Id}/remarks",
            new AddRemarkRequest("Internal note: verify medical bills", true));
        Assert.Equal(HttpStatusCode.OK, remarkResponse.StatusCode);

        var remarks = (await client.GetFromJsonAsync<List<ApplicationRemarkDto>>($"/api/applications/{application.Id}/remarks"))!;
        Assert.Contains(remarks, r => r.IsInternal && r.Remark.Contains("verify medical bills"));
    }

    [Fact]
    public async Task DocumentUpload_ThenDownload_RoundTripsTheSameBytes()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "33333-3333333-3");

        var fileBytes = TestFiles.MinimalJpegBytes;
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "photo.jpg");

        var uploadResponse = await client.PostAsync(
            $"/api/documents?documentType=ApplicantPhoto&applicantId={applicant.Id}", form);
        var document = await ReadOrFailAsync<DocumentDto>(uploadResponse, HttpStatusCode.OK);
        Assert.Equal("photo.jpg", document.FileName);
        Assert.Equal(fileBytes.Length, document.SizeBytes);

        var downloadResponse = await client.GetAsync($"/api/documents/{document.Id}");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(fileBytes, downloadedBytes);

        // Applicant creation itself now uploads CnicFront + MembershipCard (A3), so the photo
        // uploaded above is the 3rd document on this applicant, not the only one.
        var forApplicant = (await client.GetFromJsonAsync<List<DocumentDto>>($"/api/documents/by-applicant/{applicant.Id}"))!;
        Assert.Single(forApplicant, d => d.Id == document.Id);
    }

    [Fact]
    public async Task DocumentUpload_WithNoOwnerSpecified_Returns422WithDetail()
    {
        var client = await CreateAuthenticatedClientAsync();

        var fileBytes = TestFiles.MinimalJpegBytes;
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "photo.jpg");

        // No applicantId/applicationId/donationId/paymentId query parameter at all.
        var uploadResponse = await client.PostAsync("/api/documents?documentType=ApplicantPhoto", form);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, uploadResponse.StatusCode);
        var problem = JsonSerializer.Deserialize<JsonElement>(await uploadResponse.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task CreateApplication_WithDuplicateExternalFormReference_Returns422NotRaw500()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "44444-4444444-4");
        var (_, healthId, zakatFundId, _) = await LoadSeededIdsAsync(client);

        var firstResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, healthId, zakatFundId, 5000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test",
            ExternalFormReference: "FORM-DUP-001"));
        await ReadOrFailAsync<ApplicationDto>(firstResponse, HttpStatusCode.Created);

        // Staff re-keying the same Google Form response against a second applicant/application.
        var secondApplicant = await CreateApplicantAsync(client, "44444-4444444-5");
        var secondResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            secondApplicant.Id, healthId, zakatFundId, 5000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test",
            ExternalFormReference: "FORM-DUP-001"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondResponse.StatusCode);
        var problem = JsonSerializer.Deserialize<JsonElement>(await secondResponse.Content.ReadAsStringAsync());
        Assert.Contains("FORM-DUP-001", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task CreateApplication_WithInvalidIntakeChannel_ReturnsValidationErrorNotRaw500()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "55555-5555555-1");
        var (_, healthId, zakatFundId, _) = await LoadSeededIdsAsync(client);

        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, healthId, zakatFundId, 5000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test",
            IntakeChannel: "Fax"));

        // FluentValidation failures on this endpoint surface as 400 (ValidationExceptionHandler),
        // not a raw 500 from the unguarded Enum.Parse this rule now prevents from being reached.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateApplication_WithOverlongExternalFormReference_ReturnsValidationErrorNotRaw500()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "55555-5555555-2");
        var (_, healthId, zakatFundId, _) = await LoadSeededIdsAsync(client);

        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, healthId, zakatFundId, 5000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test",
            ExternalFormReference: new string('X', 101)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateApplication_CategoryChange_AfterGuarantorAdded_IsRejected()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "44444-4444444-8");
        var (_, healthId, zakatFundId, _) = await LoadSeededIdsAsync(client);
        var educationId = (await client.GetFromJsonAsync<List<ApplicationCategoryDto>>("/api/application-categories"))!
            .Single(c => c.Code == "EDUCATION").Id;

        var createResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, healthId, zakatFundId, 5000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test"));
        var application = await ReadOrFailAsync<ApplicationDto>(createResponse, HttpStatusCode.Created);

        // Category-scoped data now exists: a guarantor row on file for this application.
        var guarantorResponse = await client.PutAsJsonAsync($"/api/applications/{application.Id}/guarantors",
            new ReplaceApplicationGuarantorsRequest([new GuarantorEntry(
                null, 1, "J-1234", "Guarantor One", null, null, null, "55555-5555555-5",
                ResidentialAddress: "123 Test Street", BusinessAddress: "456 Business Road", BusinessNature: null,
                PhoneHome: "021-1111111", PhoneOffice: "021-1111112", PhoneMobile: "0300-1111111", DeclarationAcceptedAt: null)]));
        Assert.Equal(HttpStatusCode.OK, guarantorResponse.StatusCode);

        var blockedUpdate = await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            educationId, zakatFundId, 5000m, null, "Normal", "Test"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blockedUpdate.StatusCode);

        // Re-saving with the category unchanged must still work.
        var noOpUpdate = await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            healthId, zakatFundId, 5000m, null, "Normal", "Test"));
        Assert.Equal(HttpStatusCode.OK, noOpUpdate.StatusCode);
    }

    [Fact]
    public async Task UpdateApplication_CategoryChange_OnFreshApplication_IsAllowed()
    {
        var client = await CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "44444-4444444-9");
        var (_, healthId, zakatFundId, _) = await LoadSeededIdsAsync(client);
        var educationId = (await client.GetFromJsonAsync<List<ApplicationCategoryDto>>("/api/application-categories"))!
            .Single(c => c.Code == "EDUCATION").Id;

        var createResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, healthId, zakatFundId, 5000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test"));
        var application = await ReadOrFailAsync<ApplicationDto>(createResponse, HttpStatusCode.Created);

        // Nothing category-scoped saved yet, so correcting the category is a legitimate no-op-adjacent edit.
        var updateResponse = await client.PutAsJsonAsync($"/api/applications/{application.Id}", new UpdateApplicationRequest(
            educationId, zakatFundId, 5000m, null, "Normal", "Test"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
    }
}
