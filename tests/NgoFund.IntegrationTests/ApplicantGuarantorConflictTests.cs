using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Users;
using static NgoFund.IntegrationTests.ApplicationCompletenessTestHelpers;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Mirror direction of item 4's guarantor-CNIC-conflict override (see
/// <see cref="GuarantorConflictOverrideTests"/>): here it's this application's OWN applicant whose
/// CNIC appears as a guarantor on a DIFFERENT currently-active application, not two guarantor rows
/// sharing a CNIC. See <c>FundApplication.EnsureApplicantNotActiveGuarantorElsewhere</c> and
/// <c>ApplicantGuarantorConflictLookup</c>. Kept in its own class/fixture purely to stay under the
/// shared login-rate-limit budget (see <see cref="StatusTransitionInvariantTests"/>'s doc comment).
/// </summary>
public class ApplicantGuarantorConflictTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    /// <summary>Creates an applicant + a HEALTH/Zakat application (left at its default Pending status
    /// — an "active" status) with one guarantor bearing <paramref name="guarantorCnic"/>, so that CNIC
    /// now conflicts with any other application's APPLICANT of the same CNIC. HEALTH has no
    /// completeness manifest and RequiresGuarantors == 0, so this "other" application never needs to
    /// itself reach Approved for the test.</summary>
    private static async Task<ApplicationDto> CreateActiveApplicationWithGuarantorCnicAsync(HttpClient client, string guarantorCnic, string ownerApplicantCnic)
    {
        var applicant = await CreateApplicantAsync(client, ownerApplicantCnic);
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

        return application;
    }

    /// <summary>A bare HEALTH/Zakat application for <paramref name="applicantCnic"/>, driven to
    /// UnderReview (one step before Approved) — HEALTH has no completeness manifest and no guarantor
    /// requirement, so nothing but the applicant-guarantor-conflict gate can block Approved.</summary>
    private static async Task<ApplicationDto> CreateReadyToApproveHealthApplicationAsync(HttpClient client, string applicantCnic)
    {
        var applicant = await CreateApplicantAsync(client, applicantCnic);
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["HEALTH"], zakatFundId);

        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));

        return application;
    }

    private static UpdateApplicantRequest ToUpdateRequest(ApplicantDto a, string newCnic) => new(
        a.MembershipNumber, a.FullName, a.FatherOrHusbandName, newCnic, a.Gender, a.DateOfBirth, a.MaritalStatus,
        a.Phone, a.AlternatePhone, a.Email, a.Address, a.City, a.District, a.Province, a.Occupation,
        a.MonthlyIncome, a.DependentsCount, a.HouseholdSize, a.IsBlacklisted, a.BlacklistReason, a.Notes,
        a.GrandfatherName, a.Surname, a.AncestralVillage, a.FatherMembershipNumber, a.WhatsappNumber);

    [Fact]
    public async Task ApplicantIsActiveGuarantorElsewhere_BlocksApprovedTransition()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        const string applicantCnic = "61101-1111111-1";
        await CreateActiveApplicationWithGuarantorCnicAsync(client, applicantCnic, "61101-2222222-2");

        var application = await CreateReadyToApproveHealthApplicationAsync(client, applicantCnic);

        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ApprovedOverride_UnblocksApprovedTransition()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        const string applicantCnic = "61102-1111111-1";
        var conflicting = await CreateActiveApplicationWithGuarantorCnicAsync(client, applicantCnic, "61102-2222222-2");

        var application = await CreateReadyToApproveHealthApplicationAsync(client, applicantCnic);

        var loaded = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
        Assert.Contains(conflicting.ApplicationNumber, loaded.ApplicantGuarantorConflictApplicationNumbers);

        var overrideResponse = await client.PostAsJsonAsync(
            $"/api/applications/{application.Id}/applicant-guarantor-conflict-override",
            new ApproveApplicantGuarantorConflictOverrideRequest("Confirmed same person, proceeding anyway."));
        var overridden = await ReadOrFailAsync<ApplicationDto>(overrideResponse, HttpStatusCode.OK);
        Assert.NotNull(overridden.ApplicantGuarantorConflictOverrideApprovedAt);
        Assert.Equal("Confirmed same person, proceeding anyway.", overridden.ApplicantGuarantorConflictOverrideReason);

        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetSingleAndPagedList_SurfaceConflictingApplicationNumbers()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        const string applicantCnic = "61103-1111111-1";
        var conflicting = await CreateActiveApplicationWithGuarantorCnicAsync(client, applicantCnic, "61103-2222222-2");
        var application = await CreateReadyToApproveHealthApplicationAsync(client, applicantCnic);

        var single = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
        Assert.Contains(conflicting.ApplicationNumber, single.ApplicantGuarantorConflictApplicationNumbers);

        var paged = await ReadOrFailAsync<PagedResult<ApplicationDto>>(
            await client.GetAsync($"/api/applications?search={application.ApplicationNumber}"), HttpStatusCode.OK);
        var fromList = Assert.Single(paged.Items, a => a.Id == application.Id);
        Assert.Contains(conflicting.ApplicationNumber, fromList.ApplicantGuarantorConflictApplicationNumbers);
    }

    [Fact]
    public async Task OtherApplicationRejected_ClearsFlag_ApprovedSucceedsWithoutOverride()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        const string applicantCnic = "61104-1111111-1";
        var conflicting = await CreateActiveApplicationWithGuarantorCnicAsync(client, applicantCnic, "61104-2222222-2");
        var application = await CreateReadyToApproveHealthApplicationAsync(client, applicantCnic);

        // Drive the OTHER application (where this person is a guarantor) to Rejected — a terminal,
        // non-active status. This must clear the conflict on its own (live read-side detection, not
        // a stored/cached flag), needing no override.
        var rejectResponse = await client.PostAsJsonAsync($"/api/applications/{conflicting.Id}/status",
            new ChangeApplicationStatusRequest("Rejected", null, "No longer needed."));
        Assert.Equal(HttpStatusCode.NoContent, rejectResponse.StatusCode);

        var reloaded = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
        Assert.Empty(reloaded.ApplicantGuarantorConflictApplicationNumbers);

        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task OverrideEndpoint_WithoutPermission_Returns403()
    {
        var superAdminClient = await factory.CreateAuthenticatedClientAsync();
        const string applicantCnic = "61105-1111111-1";
        await CreateActiveApplicationWithGuarantorCnicAsync(superAdminClient, applicantCnic, "61105-2222222-2");
        var application = await CreateReadyToApproveHealthApplicationAsync(superAdminClient, applicantCnic);

        const string adminEmail = "applicant-guarantor-override-test@ngofund.local";
        const string adminPassword = "ApplicantGuarantorOverride123!";
        await ReadOrFailAsync<UserSummaryDto>(
            await superAdminClient.PostAsJsonAsync("/api/users", new CreateUserRequest(adminEmail, "Applicant Guarantor Override Test", null, adminPassword, ["Admin"])),
            HttpStatusCode.Created);

        var adminClient = factory.CreateClient();
        var loginResponse = await adminClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(adminEmail, adminPassword));
        var auth = await ReadOrFailAsync<AuthResponse>(loginResponse, HttpStatusCode.OK);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await adminClient.PostAsJsonAsync(
            $"/api/applications/{application.Id}/applicant-guarantor-conflict-override",
            new ApproveApplicantGuarantorConflictOverrideRequest("Trying without permission."));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EditingApplicantCnicAfterOverride_ReblocksApprovedTransition()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        const string firstCnic = "61106-1111111-1";
        const string secondCnic = "61106-5555555-5";
        await CreateActiveApplicationWithGuarantorCnicAsync(client, firstCnic, "61106-2222222-2");
        await CreateActiveApplicationWithGuarantorCnicAsync(client, secondCnic, "61106-6666666-6");

        var applicant = await CreateApplicantAsync(client, firstCnic);
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["HEALTH"], zakatFundId);
        await client.PostAsJsonAsync($"/api/applications/{application.Id}/status", new ChangeApplicationStatusRequest("UnderReview", null, null));

        var overrideResponse = await client.PostAsJsonAsync(
            $"/api/applications/{application.Id}/applicant-guarantor-conflict-override",
            new ApproveApplicantGuarantorConflictOverrideRequest("Confirmed same person."));
        var overridden = await ReadOrFailAsync<ApplicationDto>(overrideResponse, HttpStatusCode.OK);
        Assert.NotNull(overridden.ApplicantGuarantorConflictOverrideApprovedAt);

        // Editing the applicant's CNIC to a DIFFERENT (also conflicting) value must re-block Approved:
        // the override was stamped for firstCnic and must not silently carry over to secondCnic.
        var updateResponse = await client.PutAsJsonAsync($"/api/applicants/{applicant.Id}", ToUpdateRequest(applicant, secondCnic));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var reloaded = await ReadOrFailAsync<ApplicationDto>(await client.GetAsync($"/api/applications/{application.Id}"), HttpStatusCode.OK);
        Assert.Null(reloaded.ApplicantGuarantorConflictOverrideApprovedAt);
        Assert.NotEmpty(reloaded.ApplicantGuarantorConflictApplicationNumbers);

        var response = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateApplication_Returns201_WhileConflictExists()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        const string applicantCnic = "61107-1111111-1";
        var conflicting = await CreateActiveApplicationWithGuarantorCnicAsync(client, applicantCnic, "61107-2222222-2");

        var applicant = await CreateApplicantAsync(client, applicantCnic);
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);

        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, categories["HEALTH"], zakatFundId, 10000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadOrFailAsync<ApplicationDto>(response, HttpStatusCode.Created);
        Assert.Contains(conflicting.ApplicationNumber, created.ApplicantGuarantorConflictApplicationNumbers);
    }
}
