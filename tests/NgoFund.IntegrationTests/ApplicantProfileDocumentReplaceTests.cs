using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.AuditLogs;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Documents;
using NgoFund.Contracts.Users;
using static NgoFund.IntegrationTests.ApplicationCompletenessTestHelpers;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// v1.6: in-place replacement of an applicant's own profile document (CNIC front/back, membership
/// card, photo) via <c>PUT /api/applicants/{id}/documents/{documentType}</c> — see
/// <see cref="NgoFund.Infrastructure.Services.ApplicantService.ReplaceProfileDocumentAsync"/> and
/// "Replacing an applicant profile document" in docs/schema.md.
/// </summary>
public class ApplicantProfileDocumentReplaceTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static async Task<HttpResponseMessage> ReplaceDocumentAsync(
        HttpClient client, Guid applicantId, string documentType, byte[] bytes, string contentType, string fileName = "replacement")
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        return await client.PutAsync($"/api/applicants/{applicantId}/documents/{documentType}", form);
    }

    private static async Task<HttpResponseMessage> UploadApplicantDocumentAsync(
        HttpClient client, Guid applicantId, string documentType, byte[] bytes, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", "dup.png");
        return await client.PostAsync($"/api/documents?documentType={documentType}&applicantId={applicantId}", form);
    }

    private static async Task<List<DocumentDto>> GetApplicantDocumentsAsync(HttpClient client, Guid applicantId) =>
        (await client.GetFromJsonAsync<List<DocumentDto>>($"/api/documents/by-applicant/{applicantId}"))!;

    private static async Task<PagedResult<ActivityLogDto>> GetAuditFeedAsync(HttpClient client) =>
        await ReadOrFailAsync<PagedResult<ActivityLogDto>>(await client.GetAsync("/api/audit-logs?page=1&pageSize=20"), HttpStatusCode.OK);

    [Fact]
    public async Task ReplaceExistingCnicFront_UpdatesInPlace_LeavesOtherTypesUnaffected()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "70101-1111111-1");
        var before = await GetApplicantDocumentsAsync(client, applicant.Id);
        var originalCnicFront = before.Single(d => d.DocumentType == "CnicFront");

        var response = await ReplaceDocumentAsync(client, applicant.Id, "CnicFront", TestFiles.MinimalJpegBytes, "image/jpeg");
        var replaced = await ReadOrFailAsync<DocumentDto>(response, HttpStatusCode.OK);

        Assert.Equal(originalCnicFront.Id, replaced.Id); // in-place update, not delete+insert
        Assert.Equal("image/jpeg", replaced.ContentType);

        var after = await GetApplicantDocumentsAsync(client, applicant.Id);
        Assert.Single(after, d => d.DocumentType == "CnicFront");
        Assert.Single(after, d => d.DocumentType == "CnicBack");
        Assert.Single(after, d => d.DocumentType == "MembershipCard");

        // The old PNG bytes are gone — downloading the same document id now returns the new JPEG bytes.
        var downloadResponse = await client.GetAsync($"/api/documents/{replaced.Id}");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(TestFiles.MinimalJpegBytes, downloadedBytes);
    }

    [Fact]
    public async Task ReplaceEmptyCategory_AfterDeletingExisting_CreatesExactlyOne()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "70101-1111111-2");
        var existingCnicBack = (await GetApplicantDocumentsAsync(client, applicant.Id)).Single(d => d.DocumentType == "CnicBack");

        var deleteResponse = await client.DeleteAsync($"/api/documents/{existingCnicBack.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.DoesNotContain(await GetApplicantDocumentsAsync(client, applicant.Id), d => d.DocumentType == "CnicBack");

        var response = await ReplaceDocumentAsync(client, applicant.Id, "CnicBack", TestFiles.MinimalPngBytes, "image/png");
        await ReadOrFailAsync<DocumentDto>(response, HttpStatusCode.OK);

        Assert.Single((await GetApplicantDocumentsAsync(client, applicant.Id)), d => d.DocumentType == "CnicBack");
    }

    [Fact]
    public async Task ReplaceWithDuplicatesOnFile_CollapsesToExactlyOne()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "70101-1111111-3");
        var original = (await GetApplicantDocumentsAsync(client, applicant.Id)).Single(d => d.DocumentType == "CnicFront");

        var dupResponse = await UploadApplicantDocumentAsync(client, applicant.Id, "CnicFront", TestFiles.MinimalPngBytes, "image/png");
        var newest = await ReadOrFailAsync<DocumentDto>(dupResponse, HttpStatusCode.OK);
        Assert.True(newest.UploadedAt >= original.UploadedAt); // the dup upload is the newest of the two
        Assert.Equal(2, (await GetApplicantDocumentsAsync(client, applicant.Id)).Count(d => d.DocumentType == "CnicFront"));

        var response = await ReplaceDocumentAsync(client, applicant.Id, "CnicFront", TestFiles.MinimalJpegBytes, "image/jpeg");
        var replaced = await ReadOrFailAsync<DocumentDto>(response, HttpStatusCode.OK);

        var survivor = Assert.Single((await GetApplicantDocumentsAsync(client, applicant.Id)), d => d.DocumentType == "CnicFront");
        Assert.Equal(newest.Id, survivor.Id); // the newest-by-UploadedAt document survives, the older original is the one deleted
        Assert.Equal(replaced.Id, survivor.Id);
    }

    [Fact]
    public async Task ReplaceDisallowedDocumentType_Returns422AndLeavesDocumentsUnchanged()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "70101-1111111-4");
        var before = await GetApplicantDocumentsAsync(client, applicant.Id);

        var response = await ReplaceDocumentAsync(client, applicant.Id, "SupportingDocument", TestFiles.MinimalPngBytes, "image/png");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(before.Count, (await GetApplicantDocumentsAsync(client, applicant.Id)).Count);
    }

    /// <summary>ApplicantPhoto is deliberately NOT one of the replaceable types here — it has its
    /// own separate, unrelated flow (<c>PUT /api/applicants/{id}/photo</c> /
    /// <c>SetProfilePhotoAsync</c>). Confirms the rejection and that nothing about the applicant's
    /// existing photo is touched by the rejected request. Also covers the unrelated unknown-
    /// applicant 404 case (see the trailing assertion) on the same client/login.</summary>
    [Fact]
    public async Task ReplaceApplicantPhoto_Returns422_LeavesPhotoAndPhotoDocumentIdUnchanged()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var createResponse = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "Photo Test Applicant", null, "70101-1111111-9", "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload, Photo: TestFiles.MinimalPngUpload));
        var applicant = await ReadOrFailAsync<ApplicantDto>(createResponse, HttpStatusCode.Created);
        Assert.NotNull(applicant.PhotoDocumentId);

        var beforePhotoDoc = (await GetApplicantDocumentsAsync(client, applicant.Id)).Single(d => d.DocumentType == "ApplicantPhoto");

        var response = await ReplaceDocumentAsync(client, applicant.Id, "ApplicantPhoto", TestFiles.MinimalJpegBytes, "image/jpeg");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var reloaded = await ReadOrFailAsync<ApplicantDto>(await client.GetAsync($"/api/applicants/{applicant.Id}"), HttpStatusCode.OK);
        Assert.Equal(applicant.PhotoDocumentId, reloaded.PhotoDocumentId);

        var afterPhotoDoc = (await GetApplicantDocumentsAsync(client, applicant.Id)).Single(d => d.DocumentType == "ApplicantPhoto");
        Assert.Equal(beforePhotoDoc.Id, afterPhotoDoc.Id);

        var downloadResponse = await client.GetAsync($"/api/documents/{afterPhotoDoc.Id}");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal(TestFiles.MinimalPngBytes, await downloadResponse.Content.ReadAsByteArrayAsync());

        // Reuses this test's already-authenticated client for the unrelated "unknown applicant"
        // case too (folded in from a separate former test method) — the per-IP login rate limiter
        // (Program.cs, PermitLimit=10/minute) caps how many fresh logins this class can make.
        var unknownApplicantResponse = await ReplaceDocumentAsync(client, Guid.NewGuid(), "CnicFront", TestFiles.MinimalPngBytes, "image/png");
        Assert.Equal(HttpStatusCode.NotFound, unknownApplicantResponse.StatusCode);
    }

    [Fact]
    public async Task ReplaceWithContentSniffMismatch_Returns422_RollsBack_OldFileStillDownloads()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "70101-1111111-5");
        var originalCnicFront = (await GetApplicantDocumentsAsync(client, applicant.Id)).Single(d => d.DocumentType == "CnicFront");

        // Declared image/png, actually JPEG bytes — fails DocumentFileValidator's magic-byte sniff.
        var response = await ReplaceDocumentAsync(client, applicant.Id, "CnicFront", TestFiles.MinimalJpegBytes, "image/png");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var after = await GetApplicantDocumentsAsync(client, applicant.Id);
        var stillThere = Assert.Single(after, d => d.DocumentType == "CnicFront");
        Assert.Equal(originalCnicFront.Id, stillThere.Id);

        var downloadResponse = await client.GetAsync($"/api/documents/{originalCnicFront.Id}");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal(TestFiles.MinimalPngBytes, await downloadResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ReplaceWithoutApplicantsEditPermission_Returns403()
    {
        var adminClient = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(adminClient, "70101-1111111-6");

        var email = $"viewer-{Guid.NewGuid():N}@ngofund.local";
        var createUserResponse = await adminClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            email, "No Applicants Edit User", null, "TempPass123!", ["Viewer"]));
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        var viewerClient = factory.CreateClient();
        var loginResponse = await viewerClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "TempPass123!"));
        var auth = (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        viewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await ReplaceDocumentAsync(viewerClient, applicant.Id, "CnicFront", TestFiles.MinimalPngBytes, "image/png");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReplaceCnicFront_NarratesReplaceSentence_WithoutAGenericApplicantUpdatedRow()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "70101-1111111-7");

        var before = await GetAuditFeedAsync(client);

        var response = await ReplaceDocumentAsync(client, applicant.Id, "CnicFront", TestFiles.MinimalJpegBytes, "image/jpeg");
        await ReadOrFailAsync<DocumentDto>(response, HttpStatusCode.OK);

        var after = await GetAuditFeedAsync(client);

        var expectedSummary = $"CNIC (Front) for applicant \"{applicant.FullName}\" was replaced";

        Assert.Equal(1, after.TotalCount - before.TotalCount);
        Assert.Contains(after.Items, i => i.Summary == expectedSummary);
        Assert.DoesNotContain(after.Items, i => i.Summary == $"Applicant \"{applicant.FullName}\" was updated");
    }

    [Fact]
    public async Task ReplaceCnicFrontAndMembershipCard_ApplicationCompletenessSlotsStillSatisfied()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "70101-1111111-8");
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["HOUSE_RENT"], zakatFundId);

        await ReadOrFailAsync<DocumentDto>(
            await ReplaceDocumentAsync(client, applicant.Id, "CnicFront", TestFiles.MinimalJpegBytes, "image/jpeg"), HttpStatusCode.OK);
        await ReadOrFailAsync<DocumentDto>(
            await ReplaceDocumentAsync(client, applicant.Id, "MembershipCard", TestFiles.MinimalJpegBytes, "image/jpeg"), HttpStatusCode.OK);

        var completeness = await ReadOrFailAsync<ApplicationCompletenessDto>(
            await client.GetAsync($"/api/applications/{application.Id}/completeness"), HttpStatusCode.OK);

        Assert.True(completeness.Slots.Single(s => s.SlotKey == "HOUSE_RENT.APPLICANT_CNIC").IsSatisfied);
        Assert.True(completeness.Slots.Single(s => s.SlotKey == "HOUSE_RENT.MEMBERSHIP_CARD").IsSatisfied);
    }
}
