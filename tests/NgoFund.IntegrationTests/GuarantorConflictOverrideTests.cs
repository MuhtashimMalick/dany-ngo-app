using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Users;
using static NgoFund.IntegrationTests.ApplicationCompletenessTestHelpers;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Item 4 (2026-09 feedback): a guarantor whose CNIC also appears on another currently-active
/// application must block the Approved transition until staff explicitly approve a conflict
/// override — see <c>FundApplication.EnsureGuarantorConflictsResolved</c> and
/// <c>GuarantorConflictLookup</c>. Kept in its own class/fixture purely to stay under the shared
/// login-rate-limit budget (see <see cref="StatusTransitionInvariantTests"/>).
/// </summary>
public class GuarantorConflictOverrideTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    /// <summary>Creates an applicant + a HEALTH/Zakat application (left at its default Pending
    /// status — an "active" status) with one guarantor bearing <paramref name="guarantorCnic"/>, so
    /// that CNIC now conflicts with any other application's guarantor of the same CNIC.</summary>
    private static async Task CreateActiveConflictingApplicationAsync(HttpClient client, string guarantorCnic, string applicantCnic)
    {
        var applicant = await CreateApplicantAsync(client, applicantCnic);
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["HEALTH"], zakatFundId);

        var response = await client.PutAsJsonAsync($"/api/applications/{application.Id}/guarantors", new ReplaceApplicationGuarantorsRequest(
        [
            new GuarantorEntry(null, 1, "MEM-CONF", "Conflicting Guarantor", null, null, null, guarantorCnic,
                ResidentialAddress: "1 Test Street", BusinessAddress: "2 Test Street", BusinessNature: null,
                PhoneHome: "021-1111111", PhoneOffice: "021-1111112", PhoneMobile: "0300-1111111", DeclarationAcceptedAt: null),
        ]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Builds a ROZGAR application that satisfies every OTHER completeness/guarantor-count
    /// requirement (so a test can isolate the conflict gate specifically), with the two given
    /// guarantor CNICs, and drives it to UnderReview (one step before Approved).</summary>
    private static async Task<ApplicationDto> CreateReadyToApproveRozgarApplicationAsync(
        HttpClient client, string applicantCnic, string guarantor1Cnic, string guarantor2Cnic)
    {
        var applicant = await CreateApplicantAsync(client, applicantCnic);
        var (categories, generalFundId) = await LoadSeedIdsAsync(client);
        var application = await RozgarCompletenessTestHelpers.CreateRozgarApplicationAsync(client, applicant.Id, categories["ROZGAR"], generalFundId);

        await client.PutAsJsonAsync($"/api/applications/{application.Id}/details/business-loan", RozgarCompletenessTestHelpers.FullyValidBusinessLoanDetails);
        await RozgarCompletenessTestHelpers.UploadAllRequiredApplicationDocumentsAsync(client, application.Id);

        var guarantors = await ReadOrFailAsync<List<ApplicationGuarantorDto>>(
            await client.PutAsJsonAsync($"/api/applications/{application.Id}/guarantors", new ReplaceApplicationGuarantorsRequest(
            [
                new GuarantorEntry(null, 1, "MEM-G1", "Guarantor One", null, null, null, guarantor1Cnic,
                    ResidentialAddress: "123 Test Street", BusinessAddress: "456 Business Road", BusinessNature: null,
                    PhoneHome: "021-1111111", PhoneOffice: "021-1111112", PhoneMobile: "0300-1111111", DeclarationAcceptedAt: null),
                new GuarantorEntry(null, 2, "MEM-G2", "Guarantor Two", null, null, null, guarantor2Cnic,
                    ResidentialAddress: "124 Test Street", BusinessAddress: "457 Business Road", BusinessNature: null,
                    PhoneHome: "021-2222221", PhoneOffice: "021-2222222", PhoneMobile: "0300-2222222", DeclarationAcceptedAt: null),
            ])),
            HttpStatusCode.OK);

        foreach (var guarantor in guarantors)
        {
            await RozgarCompletenessTestHelpers.UploadRequiredGuarantorDocumentsAsync(client, guarantor.Id);
        }

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));

        return application;
    }

    [Fact]
    public async Task UnresolvedConflict_BlocksApprovedTransition()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        const string conflictCnic = "60101-1111111-1";
        await CreateActiveConflictingApplicationAsync(client, conflictCnic, "60101-2222222-2");

        var application = await CreateReadyToApproveRozgarApplicationAsync(client, "60101-3333333-3", conflictCnic, "60101-4444444-4");

        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ApprovedOverride_UnblocksApprovedTransition()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        const string conflictCnic = "60102-1111111-1";
        await CreateActiveConflictingApplicationAsync(client, conflictCnic, "60102-2222222-2");

        var application = await CreateReadyToApproveRozgarApplicationAsync(client, "60102-3333333-3", conflictCnic, "60102-4444444-4");
        var guarantors = await ReadOrFailAsync<List<ApplicationGuarantorDto>>(
            await client.GetAsync($"/api/applications/{application.Id}/guarantors"), HttpStatusCode.OK);
        var conflictingGuarantor = guarantors.Single(g => g.Cnic == conflictCnic);
        Assert.NotEmpty(conflictingGuarantor.ConflictingApplicationNumbers);

        var overrideResponse = await client.PostAsJsonAsync(
            $"/api/applications/{application.Id}/guarantors/{conflictingGuarantor.Id}/conflict-override",
            new ApproveGuarantorConflictOverrideRequest("Confirmed same person, proceeding anyway."));
        var overridden = await ReadOrFailAsync<ApplicationGuarantorDto>(overrideResponse, HttpStatusCode.OK);
        Assert.NotNull(overridden.ConflictOverrideApprovedAt);

        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task EditingGuarantorCnicAfterOverride_ClearsOverride_AndReblocksApproval()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        const string firstConflictCnic = "60103-1111111-1";
        const string secondConflictCnic = "60103-5555555-5";
        const string guarantor2Cnic = "60103-4444444-4";
        await CreateActiveConflictingApplicationAsync(client, firstConflictCnic, "60103-2222222-2");
        await CreateActiveConflictingApplicationAsync(client, secondConflictCnic, "60103-6666666-6");

        var application = await CreateReadyToApproveRozgarApplicationAsync(client, "60103-3333333-3", firstConflictCnic, guarantor2Cnic);
        var guarantors = await ReadOrFailAsync<List<ApplicationGuarantorDto>>(
            await client.GetAsync($"/api/applications/{application.Id}/guarantors"), HttpStatusCode.OK);
        var conflictingGuarantor = guarantors.Single(g => g.Cnic == firstConflictCnic);
        var otherGuarantor = guarantors.Single(g => g.Cnic == guarantor2Cnic);

        await client.PostAsJsonAsync(
            $"/api/applications/{application.Id}/guarantors/{conflictingGuarantor.Id}/conflict-override",
            new ApproveGuarantorConflictOverrideRequest("Confirmed same person."));

        // Editing the overridden guarantor's CNIC to a DIFFERENT (also conflicting) value must
        // clear the override — an override approved for one person must not carry over silently.
        var edited = await ReadOrFailAsync<List<ApplicationGuarantorDto>>(
            await client.PutAsJsonAsync($"/api/applications/{application.Id}/guarantors", new ReplaceApplicationGuarantorsRequest(
            [
                new GuarantorEntry(conflictingGuarantor.Id, 1, conflictingGuarantor.MembershipNumber, conflictingGuarantor.FullName,
                    null, null, null, secondConflictCnic,
                    ResidentialAddress: "123 Test Street", BusinessAddress: "456 Business Road", BusinessNature: null,
                    PhoneHome: "021-1111111", PhoneOffice: "021-1111112", PhoneMobile: "0300-1111111", DeclarationAcceptedAt: null),
                new GuarantorEntry(otherGuarantor.Id, 2, otherGuarantor.MembershipNumber, otherGuarantor.FullName,
                    null, null, null, guarantor2Cnic,
                    ResidentialAddress: "124 Test Street", BusinessAddress: "457 Business Road", BusinessNature: null,
                    PhoneHome: "021-2222221", PhoneOffice: "021-2222222", PhoneMobile: "0300-2222222", DeclarationAcceptedAt: null),
            ])),
            HttpStatusCode.OK);
        Assert.Null(edited.Single(g => g.Id == conflictingGuarantor.Id).ConflictOverrideApprovedAt);

        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ConflictOverrideEndpoint_WithoutPermission_Returns403()
    {
        var superAdminClient = await factory.CreateAuthenticatedClientAsync();
        const string conflictCnic = "60104-1111111-1";
        await CreateActiveConflictingApplicationAsync(superAdminClient, conflictCnic, "60104-2222222-2");
        var application = await CreateReadyToApproveRozgarApplicationAsync(superAdminClient, "60104-3333333-3", conflictCnic, "60104-4444444-4");
        var guarantors = await ReadOrFailAsync<List<ApplicationGuarantorDto>>(
            await superAdminClient.GetAsync($"/api/applications/{application.Id}/guarantors"), HttpStatusCode.OK);
        var conflictingGuarantor = guarantors.Single(g => g.Cnic == conflictCnic);

        const string adminEmail = "guarantor-override-test@ngofund.local";
        const string adminPassword = "GuarantorOverride123!";
        await ReadOrFailAsync<UserSummaryDto>(
            await superAdminClient.PostAsJsonAsync("/api/users", new CreateUserRequest(adminEmail, "Guarantor Override Test", null, adminPassword, ["Admin"])),
            HttpStatusCode.Created);

        var adminClient = factory.CreateClient();
        var loginResponse = await adminClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(adminEmail, adminPassword));
        var auth = await ReadOrFailAsync<AuthResponse>(loginResponse, HttpStatusCode.OK);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await adminClient.PostAsJsonAsync(
            $"/api/applications/{application.Id}/guarantors/{conflictingGuarantor.Id}/conflict-override",
            new ApproveGuarantorConflictOverrideRequest("Trying without permission."));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
