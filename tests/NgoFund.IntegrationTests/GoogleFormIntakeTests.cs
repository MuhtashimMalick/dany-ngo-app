using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Documents;
using NgoFund.Contracts.Intake;
using NgoFund.Contracts.Users;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Google Form intake (the whole feature this test file's PR adds): the "GoogleFormIntake" auth
/// scheme's isolation from the JWT scheme, submission idempotency, applicant find-or-create, and
/// the document-upload path — all through the real HTTP API against a real, freshly migrated
/// Postgres, same pattern as <see cref="CategoryApplicationDetailsTests"/>.
/// </summary>
public class GoogleFormIntakeTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private const string IntakeKey = "test-intake-key-0123456789";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private async Task<HttpClient> CreateIntakeClientAsync()
    {
        factory.GoogleFormIntakeApiKey = IntakeKey;
        var client = await factory.CreateSeededClientAsync();
        client.DefaultRequestHeaders.Add("X-Intake-Key", IntakeKey);
        return client;
    }

    // Cached (not re-logged-in) per test: this class now makes enough staff-client requests that
    // logging in fresh every time trips the API's real 10/min "login" rate-limit policy
    // (Program.cs) within the single AuthApiFactory instance IClassFixture gives the whole class —
    // that's correct production behavior (see LoginRateLimitTests), so the fix belongs here, not
    // there. The token (15 min lifetime) easily outlives this class's run.
    private static string? _cachedStaffAccessToken;

    private async Task<HttpClient> CreateStaffClientAsync()
    {
        var client = await factory.CreateSeededClientAsync();
        if (_cachedStaffAccessToken is null)
        {
            var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@ngofund.local", "ChangeMe123!"));
            var auth = (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
            _cachedStaffAccessToken = auth.AccessToken;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _cachedStaffAccessToken);
        return client;
    }

    private static async Task<T> ReadOrFailAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {expected}, got {response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, JsonOptions)!;
    }

    private static GoogleFormSubmissionRequest HousingSubmission(
        string formResponseId, string cnic = "42101-1000001-1", string membershipNumber = "M9001", DateTimeOffset? submittedAt = null) => new(
        formResponseId, submittedAt ?? DateTimeOffset.UtcNow, "someone@example.com", "Housing Assistance",
        [
            new GoogleFormAnswer("Bhavnagar Jamaat Membership Number", null, [membershipNumber]),
            new GoogleFormAnswer("Full Name", null, ["Intake Applicant"]),
            new GoogleFormAnswer("CNIC Number", null, [cnic]),
            new GoogleFormAnswer("Age", null, ["40"]),
            new GoogleFormAnswer("Current Residential Address", null, ["Street 1"]),
            new GoogleFormAnswer("Monthly Rent", null, ["12000"]),
            new GoogleFormAnswer("Advance Paid", null, ["20000"]),
            new GoogleFormAnswer("Years Living at Current Address", null, ["2"]),
        ],
        []);

    // ---------- auth isolation ----------

    [Fact]
    public async Task Intake_NoKeyHeader_Returns401()
    {
        factory.GoogleFormIntakeApiKey = IntakeKey;
        var client = await factory.CreateSeededClientAsync();

        var response = await client.GetAsync("/api/intake/google-form/ping");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Intake_WrongKey_Returns401()
    {
        factory.GoogleFormIntakeApiKey = IntakeKey;
        var client = await factory.CreateSeededClientAsync();
        client.DefaultRequestHeaders.Add("X-Intake-Key", "wrong-key");

        var response = await client.GetAsync("/api/intake/google-form/ping");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Intake_UnconfiguredKey_AlwaysFails()
    {
        // GoogleFormIntakeApiKey left null — same as an unset GOOGLE_FORM_INTAKE_API_KEY in prod.
        var client = await factory.CreateSeededClientAsync();
        client.DefaultRequestHeaders.Add("X-Intake-Key", "anything");

        var response = await client.GetAsync("/api/intake/google-form/ping");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Intake_JwtToken_NotAcceptedOnIntakeEndpoint()
    {
        factory.GoogleFormIntakeApiKey = IntakeKey;
        var staffClient = await CreateStaffClientAsync();

        var response = await staffClient.GetAsync("/api/intake/google-form/ping");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StaffEndpoint_IntakeKey_NotAcceptedInPlaceOfJwt()
    {
        var intakeClient = await CreateIntakeClientAsync();

        var response = await intakeClient.GetAsync("/api/applications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Intake_ValidKey_PingReturns200()
    {
        var client = await CreateIntakeClientAsync();

        var response = await client.GetAsync("/api/intake/google-form/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------- submission happy path + idempotency ----------

    [Fact]
    public async Task Submit_Housing_CreatesApplicantAndApplication_AttributedToIntakeUser()
    {
        var intakeClient = await CreateIntakeClientAsync();
        var formResponseId = $"resp-{Guid.NewGuid():N}";

        var response = await intakeClient.PostAsJsonAsync("/api/intake/google-form/submissions", HousingSubmission(formResponseId));
        var result = await ReadOrFailAsync<GoogleFormSubmissionResponse>(response, HttpStatusCode.Created);

        Assert.True(result.Created);
        Assert.StartsWith("APP-", result.ApplicationNumber);

        var staffClient = await CreateStaffClientAsync();
        var application = await staffClient.GetFromJsonAsync<ApplicationDto>($"/api/applications/{result.ApplicationId}");
        Assert.Equal("GoogleForm", application!.IntakeChannel);
        Assert.Equal(formResponseId, application.ExternalFormReference);
        Assert.Equal("HOUSE_RENT", application.ApplicationCategoryCode);
        Assert.Equal("Zakat Fund", application.FundCategoryName);
    }

    [Fact]
    public async Task Submit_SameFormResponseIdTwice_ReturnsSameApplication_NoDuplicate()
    {
        var intakeClient = await CreateIntakeClientAsync();
        var formResponseId = $"resp-{Guid.NewGuid():N}";
        var submission = HousingSubmission(formResponseId, "42101-1000002-2");

        var first = await ReadOrFailAsync<GoogleFormSubmissionResponse>(
            await intakeClient.PostAsJsonAsync("/api/intake/google-form/submissions", submission), HttpStatusCode.Created);
        var second = await ReadOrFailAsync<GoogleFormSubmissionResponse>(
            await intakeClient.PostAsJsonAsync("/api/intake/google-form/submissions", submission), HttpStatusCode.OK);

        Assert.Equal(first.ApplicationId, second.ApplicationId);
        Assert.False(second.Created);
    }

    [Fact]
    public async Task Submit_InvalidApplicantCnic_Returns422_NothingPersisted()
    {
        var intakeClient = await CreateIntakeClientAsync();
        var formResponseId = $"resp-{Guid.NewGuid():N}";
        var submission = HousingSubmission(formResponseId, cnic: "not-a-cnic");

        var response = await intakeClient.PostAsJsonAsync("/api/intake/google-form/submissions", submission);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var staffClient = await CreateStaffClientAsync();
        var applications = await staffClient.GetFromJsonAsync<JsonElement>("/api/applications?page=1&pageSize=5");
        // Nothing with this formResponseId should exist — the whole write rolled back together.
        var search = await staffClient.GetFromJsonAsync<JsonElement>($"/api/applicants?search=not-a-cnic");
        Assert.Equal(0, search.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Submit_ExistingApplicantCnic_ReusesApplicant_FillsBlanksOnly()
    {
        var staffClient = await CreateStaffClientAsync();
        var applicant = await staffClient.PostAsJsonAsync("/api/applicants", new NgoFund.Contracts.Applicants.CreateApplicantRequest(
            null, "Existing Applicant", null, "42101-1000003-3", "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        var existing = await ReadOrFailAsync<NgoFund.Contracts.Applicants.ApplicantDto>(applicant, HttpStatusCode.Created);

        var intakeClient = await CreateIntakeClientAsync();
        var formResponseId = $"resp-{Guid.NewGuid():N}";
        var response = await intakeClient.PostAsJsonAsync(
            "/api/intake/google-form/submissions", HousingSubmission(formResponseId, "42101-1000003-3"));
        var result = await ReadOrFailAsync<GoogleFormSubmissionResponse>(response, HttpStatusCode.Created);

        var reloaded = await staffClient.GetFromJsonAsync<NgoFund.Contracts.Applicants.ApplicantDto>($"/api/applicants/{existing.Id}");
        // Name mismatch ("Existing Applicant" vs "Intake Applicant") -> the existing profile name
        // must be KEPT, never overwritten.
        Assert.Equal("Existing Applicant", reloaded!.FullName);

        var application = await staffClient.GetFromJsonAsync<ApplicationDto>($"/api/applications/{result.ApplicationId}");
        Assert.Equal(existing.Id, application!.ApplicantId);

        var remarks = await staffClient.GetFromJsonAsync<List<ApplicationRemarkDto>>($"/api/applications/{result.ApplicationId}/remarks");
        Assert.Contains(remarks!, r => r.IsInternal && r.Remark.Contains("differs from applicant profile name"));
    }

    // ---------- intake summary + filter ----------

    private static async Task<IntakeSummaryDto> GetIntakeSummaryAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<IntakeSummaryDto>("/api/applications/intake-summary"))!;

    private static async Task<IntakeSeenDto> MarkIntakeSeenAsync(HttpClient client) =>
        await ReadOrFailAsync<IntakeSeenDto>(await client.PostAsync("/api/me/intake-seen", null), HttpStatusCode.OK);

    [Fact]
    public async Task IntakeSummary_UnreadCount_ClearsAfterSeen()
    {
        var staffClient = await CreateStaffClientAsync();
        await MarkIntakeSeenAsync(staffClient); // baseline reset

        var intakeClient = await CreateIntakeClientAsync();
        await intakeClient.PostAsJsonAsync("/api/intake/google-form/submissions", HousingSubmission($"resp-{Guid.NewGuid():N}", "42101-1000020-1"));
        await intakeClient.PostAsJsonAsync("/api/intake/google-form/submissions", HousingSubmission($"resp-{Guid.NewGuid():N}", "42101-1000020-2"));

        var afterSubmit = await GetIntakeSummaryAsync(staffClient);
        Assert.Equal(2, afterSubmit.UnreadGoogleFormCount);
        var pendingBefore = afterSubmit.PendingGoogleFormCount;

        var seen = await MarkIntakeSeenAsync(staffClient);
        Assert.Equal(2, seen.ClearedUnreadCount);
        Assert.NotEqual(default, seen.SeenAt);

        // The seen call never touches the separate Pending work-queue count.
        var afterSeen = await GetIntakeSummaryAsync(staffClient);
        Assert.Equal(0, afterSeen.UnreadGoogleFormCount);
        Assert.Equal(pendingBefore, afterSeen.PendingGoogleFormCount);
    }

    [Fact]
    public async Task IntakeSummary_UnreadCount_IgnoresStatus()
    {
        var staffClient = await CreateStaffClientAsync();
        await MarkIntakeSeenAsync(staffClient); // baseline reset

        var intakeClient = await CreateIntakeClientAsync();
        var formResponseId = $"resp-{Guid.NewGuid():N}";
        var submitResponse = await intakeClient.PostAsJsonAsync(
            "/api/intake/google-form/submissions", HousingSubmission(formResponseId, "42101-1000021-1"));
        var submitResult = await ReadOrFailAsync<GoogleFormSubmissionResponse>(submitResponse, HttpStatusCode.Created);

        var before = await GetIntakeSummaryAsync(staffClient);
        Assert.Equal(1, before.UnreadGoogleFormCount);
        var pendingBefore = before.PendingGoogleFormCount;

        var statusResponse = await staffClient.PostAsJsonAsync($"/api/applications/{submitResult.ApplicationId}/status",
            new ChangeApplicationStatusRequest("UnderReview", null, null));
        Assert.Equal(HttpStatusCode.NoContent, statusResponse.StatusCode);

        // Moving it out of Pending must not clear the unread count — only status changes.
        var after = await GetIntakeSummaryAsync(staffClient);
        Assert.Equal(1, after.UnreadGoogleFormCount);
        Assert.Equal(pendingBefore - 1, after.PendingGoogleFormCount);
    }

    [Fact]
    public async Task IntakeSummary_UnreadCount_UsesArrivalTimeNotSubmittedAt()
    {
        var staffClient = await CreateStaffClientAsync();
        await MarkIntakeSeenAsync(staffClient); // baseline reset

        var intakeClient = await CreateIntakeClientAsync();
        await intakeClient.PostAsJsonAsync("/api/intake/google-form/submissions",
            HousingSubmission($"resp-{Guid.NewGuid():N}", "42101-1000022-1", submittedAt: DateTimeOffset.UtcNow.AddDays(-2)));

        // Regression guard: a form submission stamped days ago by Google must still count as
        // unread, because retryPending can deliver it hours (or longer) after the fact — arrival
        // (CreatedAt, server receive time) drives the badge, never the form's own SubmittedAt.
        var summary = await GetIntakeSummaryAsync(staffClient);
        Assert.Equal(1, summary.UnreadGoogleFormCount);
    }

    [Fact]
    public async Task IntakeSummary_UnreadCount_IsPerUser()
    {
        var staffClient = await CreateStaffClientAsync();

        var email = $"intake-badge-{Guid.NewGuid():N}@ngofund.local";
        var createUserResponse = await staffClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            email, "Intake Badge Test User", null, "TempPass123!", ["DataEntryOperator"]));
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        var secondClient = factory.CreateClient();
        var loginResponse = await secondClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "TempPass123!"));
        var auth = (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        secondClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Brand-new user, nothing arrived since their account was created -> starts at 0.
        var baseline = await GetIntakeSummaryAsync(secondClient);
        Assert.Equal(0, baseline.UnreadGoogleFormCount);

        var intakeClient = await CreateIntakeClientAsync();
        await intakeClient.PostAsJsonAsync("/api/intake/google-form/submissions", HousingSubmission($"resp-{Guid.NewGuid():N}", "42101-1000023-1"));

        var secondUnread = await GetIntakeSummaryAsync(secondClient);
        Assert.Equal(1, secondUnread.UnreadGoogleFormCount);

        await MarkIntakeSeenAsync(staffClient);

        // The admin's seen call must not clear a different user's marker.
        var secondStill = await GetIntakeSummaryAsync(secondClient);
        Assert.Equal(1, secondStill.UnreadGoogleFormCount);
    }

    [Fact]
    public async Task MarkIntakeSeen_DoesNotWriteAuditLog()
    {
        var staffClient = await CreateStaffClientAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adminUser = await dbContext.Users.SingleAsync(u => u.Email == "admin@ngofund.local");

        var before = await dbContext.AuditLogs.CountAsync(l => l.EntityName == "ApplicationUser" && l.EntityId == adminUser.Id.ToString());

        await MarkIntakeSeenAsync(staffClient);

        // ExecuteUpdateAsync never flows through AuditSaveChangesInterceptor, so this writes no row.
        var after = await dbContext.AuditLogs.CountAsync(l => l.EntityName == "ApplicationUser" && l.EntityId == adminUser.Id.ToString());
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task MarkIntakeSeen_Unauthenticated_Returns401()
    {
        var client = await factory.CreateSeededClientAsync();

        var response = await client.PostAsync("/api/me/intake-seen", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetApplications_IntakeChannelFilter_ReturnsOnlyGoogleForm()
    {
        var intakeClient = await CreateIntakeClientAsync();
        await intakeClient.PostAsJsonAsync(
            "/api/intake/google-form/submissions", HousingSubmission($"resp-{Guid.NewGuid():N}", "42101-1000005-5"));

        var staffClient = await CreateStaffClientAsync();
        var response = await staffClient.GetFromJsonAsync<JsonElement>("/api/applications?page=1&pageSize=50&intakeChannel=GoogleForm");

        var items = response.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal("GoogleForm", i.GetProperty("intakeChannel").GetString()));
    }

    // ---------- document upload ----------

    [Fact]
    public async Task UploadDocument_HappyPath_LandsInApplicantMembershipCardSlot()
    {
        var intakeClient = await CreateIntakeClientAsync();
        var formResponseId = $"resp-{Guid.NewGuid():N}";
        var submitResponse = await ReadOrFailAsync<GoogleFormSubmissionResponse>(
            await intakeClient.PostAsJsonAsync("/api/intake/google-form/submissions", HousingSubmission(formResponseId, "42101-1000006-6")),
            HttpStatusCode.Created);

        using var form = new MultipartFormDataContent
        {
            { new StringContent("Attach: Bhavnagar Jamaat membership card"), "questionTitle" },
            { new StringContent("drive-file-1"), "driveFileId" },
        };
        var fileContent = new ByteArrayContent(TestFiles.MinimalPngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", "card.png");

        var uploadResponse = await intakeClient.PostAsync($"/api/intake/google-form/submissions/{formResponseId}/documents", form);
        var document = await ReadOrFailAsync<DocumentDto>(uploadResponse, HttpStatusCode.Created);
        Assert.Equal("MembershipCard", document.DocumentType);

        // Duplicate driveFileId -> 200, no second row.
        using var form2 = new MultipartFormDataContent
        {
            { new StringContent("Attach: Bhavnagar Jamaat membership card"), "questionTitle" },
            { new StringContent("drive-file-1"), "driveFileId" },
        };
        var fileContent2 = new ByteArrayContent(TestFiles.MinimalPngBytes);
        fileContent2.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form2.Add(fileContent2, "file", "card.png");
        var dupResponse = await intakeClient.PostAsync($"/api/intake/google-form/submissions/{formResponseId}/documents", form2);
        var dupDocument = await ReadOrFailAsync<DocumentDto>(dupResponse, HttpStatusCode.OK);
        Assert.Equal(document.Id, dupDocument.Id);

        var staffClient = await CreateStaffClientAsync();
        var applicant = await staffClient.GetFromJsonAsync<ApplicationDto>($"/api/applications/{submitResponse.ApplicationId}");
        var docs = await staffClient.GetFromJsonAsync<List<DocumentDto>>($"/api/documents/by-applicant/{applicant!.ApplicantId}");
        Assert.Single(docs!, d => d.Id == document.Id);
    }

    // Feedback round 3, item H, could NOT be reproduced as described — see the final report:
    // `Document` does not implement `ISoftDeletable` (confirmed against `src/NgoFund.Domain/
    // Entities/Document.cs`), so `DocumentService.DeleteAsync`'s `Remove()` is a genuine hard
    // delete (verified empirically: a GET after delete 404s because the row is gone, and a retry
    // upload with the same driveFileId succeeds as a brand-new row with a NEW id, never hitting
    // ix_documents_external_file_reference's unique constraint). There is no soft-delete query
    // filter on Documents for IgnoreQueryFilters to bypass, so the described 500/infinite-retry
    // scenario cannot occur today. The defensive `IgnoreQueryFilters()` call was still added to
    // the dedupe lookup in GoogleFormIntakeService (harmless now, correct if Document ever gains
    // soft-delete later) but no regression test is written against a scenario that cannot exist.

    [Fact]
    public async Task UploadDocument_UnknownFormResponseId_Returns404()
    {
        var intakeClient = await CreateIntakeClientAsync();
        using var form = new MultipartFormDataContent
        {
            { new StringContent("Attach: photocopy of CNIC"), "questionTitle" },
            { new StringContent("drive-nope"), "driveFileId" },
        };
        var fileContent = new ByteArrayContent(TestFiles.MinimalPngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", "x.png");

        var response = await intakeClient.PostAsync("/api/intake/google-form/submissions/does-not-exist/documents", form);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- staff-path invariants unaffected by nullable RequestedAmount/Gender ----------

    [Fact]
    public async Task StaffCreate_NullRequestedAmount_StillRejected()
    {
        var staffClient = await CreateStaffClientAsync();
        var categories = await staffClient.GetFromJsonAsync<List<NgoFund.Contracts.ApplicationCategories.ApplicationCategoryDto>>("/api/application-categories");
        var funds = await staffClient.GetFromJsonAsync<List<NgoFund.Contracts.FundCategories.FundCategoryDto>>("/api/fund-categories");
        var houseRent = categories!.Single(c => c.Code == "HOUSE_RENT");
        var zakat = funds!.Single(f => f.Code == "ZAKAT");

        var applicantResponse = await staffClient.PostAsJsonAsync("/api/applicants", new NgoFund.Contracts.Applicants.CreateApplicantRequest(
            null, "Null Amount Applicant", null, "42101-1000007-7", "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        var applicant = await ReadOrFailAsync<NgoFund.Contracts.Applicants.ApplicantDto>(applicantResponse, HttpStatusCode.Created);

        var payload = new
        {
            applicantId = applicant.Id, applicationCategoryId = houseRent.Id, fundCategoryId = zakat.Id,
            requestedAmount = (decimal?)null, priority = "Normal", applicationDate = DateOnly.FromDateTime(DateTime.UtcNow), purpose = "Test",
        };

        var response = await staffClient.PostAsJsonAsync("/api/applications", payload);

        // FluentValidation failures map to 400 (ValidationExceptionHandler), not 422 — distinct
        // from the domain-rule 422s elsewhere in this file.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- soft-deleted applicant CNIC/membership-number reuse ----------

    [Fact]
    public async Task Submit_FirstTimeCnic_NoFiles_CreatesApplicantWithoutDocuments_AndLinksApplication()
    {
        var intakeClient = await CreateIntakeClientAsync();
        var formResponseId = $"resp-{Guid.NewGuid():N}";
        const string cnic = "42101-1000011-1";

        var response = await intakeClient.PostAsJsonAsync("/api/intake/google-form/submissions", HousingSubmission(formResponseId, cnic, "M9011"));
        var result = await ReadOrFailAsync<GoogleFormSubmissionResponse>(response, HttpStatusCode.Created);

        var staffClient = await CreateStaffClientAsync();
        var search = await staffClient.GetFromJsonAsync<JsonElement>($"/api/applicants?search={cnic}");
        Assert.Equal(1, search.GetProperty("totalCount").GetInt32());
        var applicantId = search.GetProperty("items")[0].GetProperty("id").GetGuid();

        var application = await staffClient.GetFromJsonAsync<ApplicationDto>($"/api/applications/{result.ApplicationId}");
        Assert.Equal(applicantId, application!.ApplicantId);

        var docs = await staffClient.GetFromJsonAsync<List<DocumentDto>>($"/api/documents/by-applicant/{applicantId}");
        Assert.Empty(docs!);
    }

    [Fact]
    public async Task Submit_CnicOfSoftDeletedApplicant_Returns422_NoApplicationCreated()
    {
        const string cnic = "42101-1000012-2";
        var staffClient = await CreateStaffClientAsync();
        var applicantResponse = await staffClient.PostAsJsonAsync("/api/applicants", new NgoFund.Contracts.Applicants.CreateApplicantRequest(
            null, "Deleted Applicant", null, cnic, "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        var applicant = await ReadOrFailAsync<NgoFund.Contracts.Applicants.ApplicantDto>(applicantResponse, HttpStatusCode.Created);

        var deleteResponse = await staffClient.DeleteAsync($"/api/applicants/{applicant.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var intakeClient = await CreateIntakeClientAsync();
        var formResponseId = $"resp-{Guid.NewGuid():N}";
        var submission = HousingSubmission(formResponseId, cnic, "M9012");

        var first = await intakeClient.PostAsJsonAsync("/api/intake/google-form/submissions", submission);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, first.StatusCode);
        var firstBody = JsonSerializer.Deserialize<JsonElement>(await first.Content.ReadAsStringAsync());
        Assert.Equal(422, firstBody.GetProperty("status").GetInt32());

        // No application was created for this formResponseId — the idempotency check would have
        // found it and returned 200 on this identical resubmit, instead it 422s again.
        var second = await intakeClient.PostAsJsonAsync("/api/intake/google-form/submissions", submission);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
    }

    [Fact]
    public async Task Submit_MembershipNumberOfSoftDeletedApplicant_LeftBlankWithNote()
    {
        const string membershipNumber = "M9013";
        var staffClient = await CreateStaffClientAsync();
        var applicantResponse = await staffClient.PostAsJsonAsync("/api/applicants", new NgoFund.Contracts.Applicants.CreateApplicantRequest(
            membershipNumber, "Deleted Membership Holder", null, "42101-1000013-3", "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        var applicant = await ReadOrFailAsync<NgoFund.Contracts.Applicants.ApplicantDto>(applicantResponse, HttpStatusCode.Created);

        var deleteResponse = await staffClient.DeleteAsync($"/api/applicants/{applicant.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var intakeClient = await CreateIntakeClientAsync();
        var formResponseId = $"resp-{Guid.NewGuid():N}";
        var response = await intakeClient.PostAsJsonAsync(
            "/api/intake/google-form/submissions", HousingSubmission(formResponseId, "42101-1000014-4", membershipNumber));
        var result = await ReadOrFailAsync<GoogleFormSubmissionResponse>(response, HttpStatusCode.Created);

        var application = await staffClient.GetFromJsonAsync<ApplicationDto>($"/api/applications/{result.ApplicationId}");
        var newApplicant = await staffClient.GetFromJsonAsync<NgoFund.Contracts.Applicants.ApplicantDto>($"/api/applicants/{application!.ApplicantId}");
        Assert.Null(newApplicant!.MembershipNumber);

        var remarks = await staffClient.GetFromJsonAsync<List<ApplicationRemarkDto>>($"/api/applications/{result.ApplicationId}/remarks");
        Assert.Contains(remarks!, r => r.IsInternal && r.Remark.Contains("already used by a different applicant"));
    }

    [Fact]
    public async Task StaffCreate_CnicOfSoftDeletedApplicant_Returns422()
    {
        const string cnic = "42101-1000015-5";
        var staffClient = await CreateStaffClientAsync();
        var firstResponse = await staffClient.PostAsJsonAsync("/api/applicants", new NgoFund.Contracts.Applicants.CreateApplicantRequest(
            null, "Original Holder", null, cnic, "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        var first = await ReadOrFailAsync<NgoFund.Contracts.Applicants.ApplicantDto>(firstResponse, HttpStatusCode.Created);

        var deleteResponse = await staffClient.DeleteAsync($"/api/applicants/{first.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var secondResponse = await staffClient.PostAsJsonAsync("/api/applicants", new NgoFund.Contracts.Applicants.CreateApplicantRequest(
            null, "Second Applicant", null, cnic, "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondResponse.StatusCode);
    }
}
