using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NgoFund.Contracts.Auth;

namespace NgoFund.IntegrationTests;

public class AuthFlowTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private const string SuperAdminEmail = "admin@ngofund.local";
    private const string SuperAdminPassword = "ChangeMe123!";

    [Fact]
    public async Task Login_WithSeededSuperAdmin_Succeeds_AndTokenGrantsAccessToProtectedEndpoint()
    {
        var client = await factory.CreateSeededClientAsync();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(SuperAdminEmail, SuperAdminPassword));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Contains("SuperAdmin", auth!.User.Roles);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var usersResponse = await client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = await factory.CreateSeededClientAsync();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(SuperAdminEmail, "WrongPassword!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = await factory.CreateSeededClientAsync();

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_ThenReuseOfOldToken_Fails()
    {
        var client = await factory.CreateSeededClientAsync();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(SuperAdminEmail, SuperAdminPassword));
        var auth = (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>())!;

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        // the old (now-rotated) refresh token must be rejected
        var reuseResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
    }
}
