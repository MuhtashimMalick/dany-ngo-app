using Microsoft.AspNetCore.Http;

namespace NgoFund.Api.Services;

/// <summary>
/// C7 (feedback round 3, item F): the two-way exposure gate between the intake port (ngrok's
/// public tunnel target) and every other port (the staff-facing API). A request is blocked when
/// the port it arrived on and the path it asked for disagree about which side of that boundary
/// they belong to — pulled out of <c>Program.cs</c> as a pure static predicate so it's unit
/// testable without spinning up a host.
/// </summary>
public static class IntakePortGate
{
    private const string IntakePathPrefix = "/api/intake";

    /// <summary>True if the request must be rejected (404): the intake port was used for a
    /// non-intake path, OR a non-intake port was used for an intake path. When
    /// <paramref name="intakePort"/> is not configured, nothing is gated.</summary>
    public static bool ShouldBlock(int? intakePort, int localPort, PathString path)
    {
        if (intakePort is not int port)
        {
            return false;
        }

        var isIntakePath = path.StartsWithSegments(IntakePathPrefix);
        var isIntakePortConnection = localPort == port;
        return isIntakePortConnection != isIntakePath;
    }
}
