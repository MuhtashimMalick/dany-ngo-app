using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.AuditLogs;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Donations;
using NgoFund.Contracts.Donors;
using NgoFund.Contracts.Payments;
using NgoFund.Contracts.Users;
using NgoFund.Infrastructure.Identity;
using NgoFund.Infrastructure.Persistence;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// End-to-end proof of the activity-log rebuild: the client-facing feed shows human sentences (not
/// GUIDs/C# names), login/refresh/logout stay silent, and the PasswordHash redaction actually
/// holds against a real Create — all through the real HTTP API against a real Postgres.
/// </summary>
public class ActivityLogTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static async Task<PagedResult<ActivityLogDto>> GetFeedAsync(HttpClient client, int pageSize = 5) =>
        await ReadOrFailAsync<PagedResult<ActivityLogDto>>(await client.GetAsync($"/api/audit-logs?page=1&pageSize={pageSize}"), HttpStatusCode.OK);

    [Fact]
    public async Task CreateDonor_ProducesAddedFeedRow()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var donorResponse = await client.PostAsJsonAsync("/api/donors", new CreateDonorRequest(
            "Activity Feed Donor", "Individual", null, null, null, null, null, null, null, null, null, false, null));
        var donor = await ReadOrFailAsync<DonorDto>(donorResponse, HttpStatusCode.Created);

        var feed = await GetFeedAsync(client);

        Assert.Contains(feed.Items, i => i.Summary == $"Donor \"{donor.DonorCode}\" (Activity Feed Donor) was added");
    }

    [Fact]
    public async Task VoidPayment_ProducesVoidedFeedRow()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId, _) = await LoadSeededIdsAsync(client);
        await FundAsync(client, zakatFundId, 10000m);

        var application = await CreateApprovedApplicationAsync(client, "19002-1900002-2", categoryId, zakatFundId, 2000m);

        var paymentResponse = await client.PostAsJsonAsync("/api/payments", new CreatePaymentRequest(
            application.Id, 500m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null));
        var payment = await ReadOrFailAsync<PaymentDto>(paymentResponse, HttpStatusCode.Created);

        var voidResponse = await client.PostAsJsonAsync($"/api/payments/{payment.Id}/void", new VoidPaymentRequest("Test void"));
        Assert.Equal(HttpStatusCode.NoContent, voidResponse.StatusCode);

        var feed = await GetFeedAsync(client);

        Assert.Contains(feed.Items, i => i.Summary == $"Payment \"{payment.PaymentNumber}\" was voided");
    }

    [Fact]
    public async Task ApproveApplication_ProducesExactlyOneStatusChangedFeedRow()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categoryId, zakatFundId, _) = await LoadSeededIdsAsync(client);

        var applicantResponse = await client.PostAsJsonAsync("/api/applicants", new CreateApplicantRequest(
            null, "Approval Regression Applicant", null, "19003-1900003-3", "Male", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            CnicFront: TestFiles.MinimalPngUpload, CnicBack: TestFiles.MinimalPngUpload, MembershipCard: TestFiles.MinimalPngUpload));
        var applicant = await ReadOrFailAsync<ApplicantDto>(applicantResponse, HttpStatusCode.Created);

        var createResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicant.Id, categoryId, zakatFundId, 5000m, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Regression test"));
        var application = await ReadOrFailAsync<ApplicationDto>(createResponse, HttpStatusCode.Created);

        var reviewResponse = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("UnderReview", null, null));
        Assert.Equal(HttpStatusCode.NoContent, reviewResponse.StatusCode);

        // Feedback round 3, item I: OTHER now has a real completeness manifest — satisfied (and its
        // own feed row, if any, captured) BEFORE the "before" snapshot, so the assertion below still
        // isolates exactly the Approve transition's own feed delta.
        await ApplicationCompletenessTestHelpers.CompleteOtherDocumentsAsync(client, application.Id);

        var before = await GetFeedAsync(client);

        var approveResponse = await client.PostAsJsonAsync($"/api/applications/{application.Id}/status",
            new ChangeApplicationStatusRequest("Approved", null, null, application.RequestedAmount));
        Assert.Equal(HttpStatusCode.NoContent, approveResponse.StatusCode);

        var after = await GetFeedAsync(client);

        // The regression this guards: application_status_history insert + application update
        // firing off two rows for one status change, instead of one.
        Assert.Equal(before.TotalCount + 1, after.TotalCount);
        Assert.Contains(after.Items, i => i.Summary == $"Application \"{application.ApplicationNumber}\" status changed from UnderReview to Approved");
    }

    [Fact]
    public async Task LoginRefreshLogout_ProduceNoNewActivityFeedRows()
    {
        var client = await factory.CreateSeededClientAsync();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@ngofund.local", "ChangeMe123!"));
        var auth = (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var before = await GetFeedAsync(client, pageSize: 1);

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshedAuth = (await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>())!;

        var secondRefreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(refreshedAuth.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, secondRefreshResponse.StatusCode);
        var secondRefreshedAuth = (await secondRefreshResponse.Content.ReadFromJsonAsync<AuthResponse>())!;

        var logoutResponse = await client.PostAsJsonAsync("/api/auth/logout", new RefreshTokenRequest(secondRefreshedAuth.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var after = await GetFeedAsync(client, pageSize: 1);

        Assert.Equal(before.TotalCount, after.TotalCount);
    }

    [Fact]
    public async Task ExportActivityLog_ReturnsNdjson_OneParsableLinePerFeedRow()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        await client.PostAsJsonAsync("/api/donors", new CreateDonorRequest(
            "Export Test Donor", "Individual", null, null, null, null, null, null, null, null, null, false, null));

        var feed = await GetFeedAsync(client, pageSize: 1);

        var exportResponse = await client.GetAsync("/api/audit-logs/export");
        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        Assert.Equal("application/x-ndjson", exportResponse.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(exportResponse.Content.Headers.ContentDisposition);

        var ndjson = await exportResponse.Content.ReadAsStringAsync();
        var lines = ndjson.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(feed.TotalCount, lines.Length);
        foreach (var line in lines)
        {
            using var parsed = JsonDocument.Parse(line);
            Assert.True(parsed.RootElement.TryGetProperty("summary", out var summary));
            Assert.False(string.IsNullOrWhiteSpace(summary.GetString()));
        }
    }

    [Fact]
    public async Task GetActivityLog_WithoutPermission_Returns403()
    {
        var adminClient = await factory.CreateAuthenticatedClientAsync();

        var email = $"deo-{Guid.NewGuid():N}@ngofund.local";
        var createUserResponse = await adminClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            email, "No Audit Access User", null, "TempPass123!", ["DataEntryOperator"]));
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        var deoClient = factory.CreateClient();
        var loginResponse = await deoClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "TempPass123!"));
        var auth = (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        deoClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await deoClient.GetAsync("/api/audit-logs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_RedactsPasswordHashInAuditLog()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var email = $"redaction-{Guid.NewGuid():N}@ngofund.local";
        var createUserResponse = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(
            email, "Redaction Test User", null, "TempPass123!", ["Viewer"]));
        var user = await ReadOrFailAsync<UserSummaryDto>(createUserResponse, HttpStatusCode.Created);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var realUser = await dbContext.Users.SingleAsync(u => u.Id == user.Id);
        var log = await dbContext.AuditLogs.SingleAsync(l => l.EntityName == "ApplicationUser" && l.EntityId == user.Id.ToString() && l.Action == "Create");

        Assert.NotNull(realUser.PasswordHash);

        // jsonb round-trips through Postgres's own canonical formatting (a space after each colon),
        // so parse rather than substring-match the raw text.
        using var newValues = JsonDocument.Parse(log.NewValues!);
        Assert.Equal("***", newValues.RootElement.GetProperty("PasswordHash").GetString());
        Assert.DoesNotContain(realUser.PasswordHash!, log.NewValues!);
    }

    [Fact]
    public async Task GrantRolePermission_ProducesGrantedFeedRow()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var role = await dbContext.Roles.SingleAsync(r => r.Name == "Viewer");
        var permission = await dbContext.Permissions.SingleAsync(p => p.Code == "settings.manage");

        // Viewer isn't seeded with settings.manage — grant it directly via a tracked DbContext
        // (there's no "grant permission" endpoint yet), same shape ActivityNarrator expects:
        // Role/Permission navigations already loaded, never queried from inside the interceptor.
        dbContext.RolePermissions.Add(new RolePermission { RoleId = role.Id, Role = role, PermissionId = permission.Id, Permission = permission });
        await dbContext.SaveChangesAsync();

        var feed = await GetFeedAsync(client);

        Assert.Contains(feed.Items, i => i.Summary == $"Role \"Viewer\" was granted permission \"{permission.DisplayName}\"");
    }
}
