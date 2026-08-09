using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Settings;

namespace NgoFund.IntegrationTests;

/// <summary>Proves the M6 settings endpoints over the already-seeded AppSetting rows.</summary>
public class SettingsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = await factory.CreateSeededClientAsync();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@ngofund.local", "ChangeMe123!"));
        var auth = (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return client;
    }

    [Fact]
    public async Task GetSettings_ReturnsSeededRows()
    {
        var client = await CreateAuthenticatedClientAsync();

        var settings = (await client.GetFromJsonAsync<List<AppSettingDto>>("/api/settings"))!;

        Assert.Contains(settings, s => s.Key == "OrgName" && s.Value == "NGO Fund Management");
        Assert.Contains(settings, s => s.Key == "Currency");
    }

    [Fact]
    public async Task UpdateSetting_ChangesValue()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/settings/ReceiptFooter", new UpdateAppSettingRequest("Thank you for your generosity."));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected OK, got {response.StatusCode}: {body}");

        var settings = (await client.GetFromJsonAsync<List<AppSettingDto>>("/api/settings"))!;
        Assert.Equal("Thank you for your generosity.", settings.Single(s => s.Key == "ReceiptFooter").Value);
    }

    [Fact]
    public async Task UpdateSetting_UnknownKey_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/settings/DoesNotExist", new UpdateAppSettingRequest("x"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
