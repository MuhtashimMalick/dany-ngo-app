using System.Net;
using System.Net.Http.Json;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Documents;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// A3/v1.4: a CNIC-front scan, a CNIC-back scan, and a Jamaat membership-card scan are now
/// mandatory at applicant creation, uploaded inline (base64) and committed atomically with the new
/// applicant row in one DB transaction
/// (<see cref="NgoFund.Infrastructure.Services.ApplicantService.CreateAsync"/>) — a
/// <c>documents</c> row cannot exist before its owner row does, so "upload first" is impossible.
/// </summary>
public class ApplicantCreationDocumentTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static async Task<bool> AnyApplicantWithCnicAsync(HttpClient client, string cnic)
    {
        var page = await ReadOrFailAsync<PagedResult<ApplicantDto>>(await client.GetAsync($"/api/applicants?search={cnic}"), HttpStatusCode.OK);
        return page.Items.Any(a => a.Cnic == cnic);
    }

    [Fact]
    public async Task Create_WithoutCnicFront_Returns400AndCreatesNoApplicant()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        const string cnic = "60110-1111111-1";

        var response = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "No Cnic Front Applicant", null, cnic, "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            MembershipCard: TestFiles.MinimalPngUpload));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await AnyApplicantWithCnicAsync(client, cnic));
    }

    [Fact]
    public async Task Create_WithoutMembershipCard_Returns400AndCreatesNoApplicant()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        const string cnic = "60110-1111111-2";

        var response = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "No Membership Card Applicant", null, cnic, "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await AnyApplicantWithCnicAsync(client, cnic));
    }

    [Fact]
    public async Task Create_WithoutCnicBack_Returns400AndCreatesNoApplicant()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        const string cnic = "60110-1111111-5";

        var response = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "No Cnic Back Applicant", null, cnic, "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await AnyApplicantWithCnicAsync(client, cnic));
    }

    [Fact]
    public async Task Create_HappyPath_UploadsCnicFrontMembershipCardAndPhoto()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "Full Upload Applicant", null, "60110-1111111-3", "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload, Photo: TestFiles.MinimalPngUpload));
        var applicant = await ReadOrFailAsync<ApplicantDto>(response, HttpStatusCode.Created);

        var docs = (await client.GetFromJsonAsync<List<DocumentDto>>($"/api/documents/by-applicant/{applicant.Id}"))!;

        Assert.Contains(docs, d => d.DocumentType == "CnicFront");
        Assert.Contains(docs, d => d.DocumentType == "MembershipCard");
        Assert.Contains(docs, d => d.DocumentType == "ApplicantPhoto");
        Assert.NotNull(applicant.PhotoDocumentId);
    }

    /// <summary>
    /// THE atomicity test: CnicFront is valid and persists first (per ApplicantService.CreateAsync's
    /// upload order), but MembershipCard's declared content type doesn't match its actual bytes, so
    /// DocumentService's content-sniffing (A4) rejects it partway through — after the applicant row
    /// and the CnicFront document already exist. Proves the whole request rolls back rather than
    /// leaving a half-created applicant with only one of its two required documents.
    /// </summary>
    [Fact]
    public async Task Create_MembershipCardFailsContentSniffing_RollsBackTheWholeApplicant()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        const string cnic = "60110-1111111-4";

        var corruptMembershipCard = new InlineFileUpload(
            "membership.png", "image/png", Convert.ToBase64String(TestFiles.MinimalJpegBytes)); // declared PNG, actually JPEG bytes

        var response = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "Atomicity Test Applicant", null, cnic, "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: corruptMembershipCard));

        Assert.False(response.IsSuccessStatusCode, $"Expected a failure response, got {response.StatusCode}");
        Assert.False(await AnyApplicantWithCnicAsync(client, cnic));
    }
}
