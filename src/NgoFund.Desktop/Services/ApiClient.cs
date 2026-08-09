using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.ApplicationCategories;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.AuditLogs;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Documents;
using NgoFund.Contracts.Donations;
using NgoFund.Contracts.Donors;
using NgoFund.Contracts.FundCategories;
using NgoFund.Contracts.Payments;
using NgoFund.Contracts.Reports;
using NgoFund.Contracts.Roles;
using NgoFund.Contracts.Settings;
using NgoFund.Contracts.Users;

namespace NgoFund.Desktop.Services;

/// <summary>
/// The single typed client every screen goes through to reach the API — no screen ever creates
/// its own <see cref="HttpClient"/> call. Proactively refreshes the access token before it
/// expires (rather than reactively retrying on a 401), which keeps this class simple: every
/// public method is a straight-line request, no retry/backoff branching.
/// </summary>
public class ApiClient(HttpClient httpClient, AuthState authState)
{
    public async Task<AuthResponse> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password), cancellationToken);
        var result = await ReadOrThrowAsync<AuthResponse>(response, cancellationToken);
        authState.SetSession(result);
        return result;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (authState.RefreshToken is { } refreshToken)
        {
            await httpClient.PostAsJsonAsync("api/auth/logout", new RefreshTokenRequest(refreshToken), cancellationToken);
        }

        authState.Clear();
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "api/auth/change-password", cancellationToken);
        request.Content = JsonContent.Create(new ChangePasswordRequest(currentPassword, newPassword));

        var response = await httpClient.SendAsync(request, cancellationToken);
        await ThrowIfErrorAsync(response);

        authState.MarkPasswordChanged();
    }

    public async Task<PagedResult<UserSummaryDto>> GetUsersAsync(int page = 1, int pageSize = 25, string? search = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/users?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }

        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url, cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<PagedResult<UserSummaryDto>>(response, cancellationToken);
    }

    public async Task<UserSummaryDto> CreateUserAsync(CreateUserRequest createRequest, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "api/users", cancellationToken);
        request.Content = JsonContent.Create(createRequest);

        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<UserSummaryDto>(response, cancellationToken);
    }

    public async Task<UserSummaryDto> UpdateUserAsync(Guid id, UpdateUserRequest updateRequest, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Put, $"api/users/{id}", cancellationToken);
        request.Content = JsonContent.Create(updateRequest);

        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<UserSummaryDto>(response, cancellationToken);
    }

    public async Task DeactivateUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Delete, $"api/users/{id}", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ThrowIfErrorAsync(response);
    }

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "api/roles", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<List<RoleDto>>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<FundCategoryDto>> GetFundCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "api/fund-categories", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<List<FundCategoryDto>>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<FundBalanceDto>> GetFundBalancesAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "api/fund-categories/balances", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<List<FundBalanceDto>>(response, cancellationToken);
    }

    public async Task<FundCategoryDto> CreateFundCategoryAsync(CreateFundCategoryRequest createRequest, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "api/fund-categories", cancellationToken);
        request.Content = JsonContent.Create(createRequest);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<FundCategoryDto>(response, cancellationToken);
    }

    public async Task<FundCategoryDto> UpdateFundCategoryAsync(Guid id, UpdateFundCategoryRequest updateRequest, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Put, $"api/fund-categories/{id}", cancellationToken);
        request.Content = JsonContent.Create(updateRequest);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<FundCategoryDto>(response, cancellationToken);
    }

    public async Task<PagedResult<DonorDto>> GetDonorsAsync(int page = 1, int pageSize = 25, string? search = null, bool? activeOnly = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/donors?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }

        if (activeOnly is not null)
        {
            url += $"&activeOnly={activeOnly.Value}";
        }

        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url, cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<PagedResult<DonorDto>>(response, cancellationToken);
    }

    public async Task<DonorDto> CreateDonorAsync(CreateDonorRequest createRequest, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "api/donors", cancellationToken);
        request.Content = JsonContent.Create(createRequest);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<DonorDto>(response, cancellationToken);
    }

    public async Task<DonorDto> UpdateDonorAsync(Guid id, UpdateDonorRequest updateRequest, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Put, $"api/donors/{id}", cancellationToken);
        request.Content = JsonContent.Create(updateRequest);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<DonorDto>(response, cancellationToken);
    }

    public async Task DeactivateDonorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Delete, $"api/donors/{id}", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ThrowIfErrorAsync(response);
    }

    public async Task<PagedResult<DonationDto>> GetDonationsAsync(int page = 1, int pageSize = 25, string? search = null, Guid? donorId = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/donations?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }
        if (donorId is not null)
        {
            url += $"&donorId={donorId}";
        }

        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url, cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<PagedResult<DonationDto>>(response, cancellationToken);
    }

    public async Task<DonationDto> CreateDonationAsync(CreateDonationRequest createRequest, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "api/donations", cancellationToken);
        request.Content = JsonContent.Create(createRequest);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<DonationDto>(response, cancellationToken);
    }

    public async Task VoidDonationAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, $"api/donations/{id}/void", cancellationToken);
        request.Content = JsonContent.Create(new VoidDonationRequest(reason));
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ThrowIfErrorAsync(response);
    }

    public async Task<IReadOnlyList<ApplicationCategoryDto>> GetApplicationCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "api/application-categories", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<List<ApplicationCategoryDto>>(response, cancellationToken);
    }

    public async Task<PagedResult<ApplicantDto>> GetApplicantsAsync(int page = 1, int pageSize = 25, string? search = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/applicants?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }

        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url, cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<PagedResult<ApplicantDto>>(response, cancellationToken);
    }

    public async Task<ApplicantDto> CreateApplicantAsync(CreateApplicantRequest createRequest, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "api/applicants", cancellationToken);
        request.Content = JsonContent.Create(createRequest);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<ApplicantDto>(response, cancellationToken);
    }

    public async Task<ApplicantDto> UpdateApplicantAsync(Guid id, UpdateApplicantRequest updateRequest, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Put, $"api/applicants/{id}", cancellationToken);
        request.Content = JsonContent.Create(updateRequest);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<ApplicantDto>(response, cancellationToken);
    }

    public async Task DeactivateApplicantAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Delete, $"api/applicants/{id}", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ThrowIfErrorAsync(response);
    }

    public async Task SetApplicantPhotoAsync(Guid applicantId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Put, $"api/applicants/{applicantId}/photo?documentId={documentId}", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ThrowIfErrorAsync(response);
    }

    public async Task<PagedResult<ApplicationDto>> GetApplicationsAsync(
        int page = 1, int pageSize = 25, string? search = null, string? status = null, Guid? applicantId = null,
        Guid? categoryId = null, DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/applications?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            url += $"&status={status}";
        }
        if (applicantId is not null)
        {
            url += $"&applicantId={applicantId}";
        }
        if (categoryId is not null)
        {
            url += $"&categoryId={categoryId}";
        }
        if (dateFrom is not null)
        {
            url += $"&dateFrom={dateFrom:yyyy-MM-dd}";
        }
        if (dateTo is not null)
        {
            url += $"&dateTo={dateTo:yyyy-MM-dd}";
        }

        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url, cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<PagedResult<ApplicationDto>>(response, cancellationToken);
    }

    public async Task<ApplicationDto> GetApplicationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, $"api/applications/{id}", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<ApplicationDto>(response, cancellationToken);
    }

    public async Task<ApplicationDto> CreateApplicationAsync(CreateApplicationRequest createRequest, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "api/applications", cancellationToken);
        request.Content = JsonContent.Create(createRequest);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<ApplicationDto>(response, cancellationToken);
    }

    public async Task<ApplicationDto> UpdateApplicationAsync(Guid id, UpdateApplicationRequest updateRequest, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Put, $"api/applications/{id}", cancellationToken);
        request.Content = JsonContent.Create(updateRequest);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<ApplicationDto>(response, cancellationToken);
    }

    public async Task ChangeApplicationStatusAsync(Guid id, string newStatus, string? remarks, string? rejectionReason, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, $"api/applications/{id}/status", cancellationToken);
        request.Content = JsonContent.Create(new ChangeApplicationStatusRequest(newStatus, remarks, rejectionReason));
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ThrowIfErrorAsync(response);
    }

    public async Task<IReadOnlyList<ApplicationStatusHistoryDto>> GetApplicationHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, $"api/applications/{id}/history", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<List<ApplicationStatusHistoryDto>>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationRemarkDto>> GetApplicationRemarksAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, $"api/applications/{id}/remarks", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<List<ApplicationRemarkDto>>(response, cancellationToken);
    }

    public async Task<ApplicationRemarkDto> AddApplicationRemarkAsync(Guid id, string remark, bool isInternal, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, $"api/applications/{id}/remarks", cancellationToken);
        request.Content = JsonContent.Create(new AddRemarkRequest(remark, isInternal));
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<ApplicationRemarkDto>(response, cancellationToken);
    }

    public async Task<DocumentDto> UploadDocumentAsync(
        byte[] fileBytes, string fileName, string contentType, string documentType,
        Guid? applicantId = null, Guid? applicationId = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/documents?documentType={documentType}";
        if (applicantId is not null)
        {
            url += $"&applicantId={applicantId}";
        }
        if (applicationId is not null)
        {
            url += $"&applicationId={applicationId}";
        }

        var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, url, cancellationToken);

        var form = new MultipartFormDataContent();
        var byteContent = new ByteArrayContent(fileBytes);
        byteContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(byteContent, "file", fileName);
        request.Content = form;

        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<DocumentDto>(response, cancellationToken);
    }

    public async Task<(byte[] Bytes, string ContentType)> DownloadDocumentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, $"api/documents/{id}", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ThrowIfErrorAsync(response);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return (bytes, response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
    }

    public async Task<IReadOnlyList<DocumentDto>> GetDocumentsForApplicantAsync(Guid applicantId, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, $"api/documents/by-applicant/{applicantId}", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<List<DocumentDto>>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentDto>> GetDocumentsForApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, $"api/documents/by-application/{applicationId}", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<List<DocumentDto>>(response, cancellationToken);
    }

    public async Task<PagedResult<PaymentDto>> GetPaymentsAsync(
        int page = 1, int pageSize = 25, string? search = null, Guid? applicationId = null, string? status = null,
        string? paymentMethod = null, Guid? fundCategoryId = null, DateOnly? dateFrom = null, DateOnly? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/payments?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }
        if (applicationId is not null)
        {
            url += $"&applicationId={applicationId}";
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            url += $"&status={status}";
        }
        if (!string.IsNullOrWhiteSpace(paymentMethod))
        {
            url += $"&paymentMethod={paymentMethod}";
        }
        if (fundCategoryId is not null)
        {
            url += $"&fundCategoryId={fundCategoryId}";
        }
        if (dateFrom is not null)
        {
            url += $"&dateFrom={dateFrom:yyyy-MM-dd}";
        }
        if (dateTo is not null)
        {
            url += $"&dateTo={dateTo:yyyy-MM-dd}";
        }

        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url, cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<PagedResult<PaymentDto>>(response, cancellationToken);
    }

    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest createRequest, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "api/payments", cancellationToken);
        request.Content = JsonContent.Create(createRequest);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<PaymentDto>(response, cancellationToken);
    }

    public async Task VoidPaymentAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, $"api/payments/{id}/void", cancellationToken);
        request.Content = JsonContent.Create(new VoidPaymentRequest(reason));
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ThrowIfErrorAsync(response);
    }

    public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(int page = 1, int pageSize = 25, string? search = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/audit-logs?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }

        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url, cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<PagedResult<AuditLogDto>>(response, cancellationToken);
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "api/reports/dashboard", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<DashboardSummaryDto>(response, cancellationToken);
    }

    public async Task<DashboardInsightsDto> GetDashboardInsightsAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "api/reports/dashboard/insights", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<DashboardInsightsDto>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<MonthlySummaryRowDto>> GetMonthlyDonationsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, $"api/reports/donations/monthly?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<List<MonthlySummaryRowDto>>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<MonthlySummaryRowDto>> GetMonthlyPaymentsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, $"api/reports/payments/monthly?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<List<MonthlySummaryRowDto>>(response, cancellationToken);
    }

    public async Task<(byte[] Bytes, string FileName)> ExportMonthlyDonationsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, $"api/reports/donations/monthly/export?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ThrowIfErrorAsync(response);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return (bytes, response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "donations.csv");
    }

    public async Task<(byte[] Bytes, string FileName)> ExportMonthlyPaymentsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, $"api/reports/payments/monthly/export?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ThrowIfErrorAsync(response);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return (bytes, response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "payments.csv");
    }

    public async Task<IReadOnlyList<AppSettingDto>> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "api/settings", cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<List<AppSettingDto>>(response, cancellationToken);
    }

    public async Task<AppSettingDto> UpdateSettingAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Put, $"api/settings/{Uri.EscapeDataString(key)}", cancellationToken);
        request.Content = JsonContent.Create(new UpdateAppSettingRequest(value));
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<AppSettingDto>(response, cancellationToken);
    }

    private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(HttpMethod method, string url, CancellationToken cancellationToken)
    {
        await EnsureFreshTokenAsync(cancellationToken);

        var request = new HttpRequestMessage(method, url);
        if (authState.AccessToken is { } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private async Task EnsureFreshTokenAsync(CancellationToken cancellationToken)
    {
        if (authState.AccessToken is null)
        {
            return; // not logged in — let the call fail naturally with 401
        }

        if (authState.AccessTokenExpiresAt is { } expiresAt && expiresAt > DateTimeOffset.UtcNow.AddSeconds(30))
        {
            return; // still valid for at least 30 more seconds
        }

        if (authState.RefreshToken is not { } refreshToken)
        {
            authState.Clear();
            return;
        }

        var response = await httpClient.PostAsJsonAsync("api/auth/refresh", new RefreshTokenRequest(refreshToken), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            authState.Clear();
            return;
        }

        var result = (await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken))!;
        authState.SetSession(result);
    }

    private static async Task<T> ReadOrThrowAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await ThrowIfErrorAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken))!;
    }

    private static async Task ThrowIfErrorAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? detail = null;
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsLite>();
            detail = problem?.Detail ?? problem?.Title;
        }
        catch
        {
            // response body wasn't ProblemDetails JSON — fall back to the status reason phrase below
        }

        throw new ApiException(response.StatusCode, detail ?? response.ReasonPhrase ?? "Request failed.");
    }
}
