using System.Net;
using System.Net.Http.Json;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.Common;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// A2: Applicant.Surname was already end-to-end (entity, requests, DTO) but not searched. Proves
/// <c>GET /api/applicants?search=</c> now matches on it too.
/// </summary>
public class ApplicantSurnameSearchTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task SearchBySurname_FindsTheApplicant()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "Ahmed Khan", null, "60101-1111111-1", "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            Surname: "Qureshiwala", CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        var applicant = await ReadOrFailAsync<ApplicantDto>(createResponse, HttpStatusCode.Created);

        var searchResponse = await client.GetAsync("/api/applicants?search=Qureshiwala");
        var page = await ReadOrFailAsync<PagedResult<ApplicantDto>>(searchResponse, HttpStatusCode.OK);

        Assert.Contains(page.Items, a => a.Id == applicant.Id);
    }
}
