using Microsoft.AspNetCore.Http;
using NgoFund.Api.Services;

namespace NgoFund.IntegrationTests;

/// <summary>Feedback round 3, item F: the two-way intake/staff port gate is a pure predicate with
/// no DB dependency — covered here (not spun up against a real host) purely because this project
/// already references <c>NgoFund.Api</c>; nothing here touches Testcontainers.</summary>
public class IntakePortGateTests
{
    private const int IntakePort = 8081;
    private const int StaffPort = 8080;

    [Fact]
    public void IntakePortWithIntakePath_IsAllowed() =>
        Assert.False(IntakePortGate.ShouldBlock(IntakePort, IntakePort, "/api/intake/google-form/ping"));

    [Fact]
    public void IntakePortWithNonIntakePath_IsBlocked() =>
        Assert.True(IntakePortGate.ShouldBlock(IntakePort, IntakePort, "/api/auth/login"));

    [Fact]
    public void NonIntakePortWithIntakePath_IsBlocked() =>
        Assert.True(IntakePortGate.ShouldBlock(IntakePort, StaffPort, "/api/intake/google-form/ping"));

    [Fact]
    public void NonIntakePortWithNonIntakePath_IsAllowed() =>
        Assert.False(IntakePortGate.ShouldBlock(IntakePort, StaffPort, "/api/auth/login"));

    [Fact]
    public void NotConfigured_NeverBlocks() =>
        Assert.False(IntakePortGate.ShouldBlock(null, StaffPort, "/api/intake/google-form/ping"));
}
