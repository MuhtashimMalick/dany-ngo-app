using System.Net;
using System.Net.Http.Json;
using NgoFund.Contracts.Auth;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Each of these locks out or rate-limits the shared seeded admin account / the test host's
/// single rate-limiter partition, so each gets its own single-test class rather than sharing a
/// fixture with other login-dependent tests that would otherwise start failing afterward.
/// </summary>
public class AccountLockoutTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task FiveFailedLogins_LocksTheAccount_EvenForTheCorrectPasswordAfterward()
    {
        var client = await factory.CreateSeededClientAsync();

        for (var i = 0; i < 5; i++)
        {
            var failResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@ngofund.local", "TotallyWrongPassword!"));
            Assert.Equal(HttpStatusCode.Unauthorized, failResponse.StatusCode);
        }

        var lockedResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@ngofund.local", "ChangeMe123!"));

        Assert.Equal(HttpStatusCode.Forbidden, lockedResponse.StatusCode);
    }
}

public class LoginRateLimitTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task ExceedingTenLoginAttemptsPerMinute_Returns429()
    {
        var client = await factory.CreateSeededClientAsync();

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 11; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nonexistent@ngofund.local", "WhateverPassword1!"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }
}
