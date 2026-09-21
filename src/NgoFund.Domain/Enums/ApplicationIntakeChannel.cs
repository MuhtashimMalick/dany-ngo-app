namespace NgoFund.Domain.Enums;

/// <summary>How an application originally reached the org. <see cref="InApp"/> is the only value the
/// desktop wizard produces. <see cref="GoogleForm"/> is set by the external Google Form integration
/// posting directly to <c>POST /api/applications</c>. <see cref="Paper"/> is retained for historical
/// records only — no writer produces it anymore (see <c>docs/schema.md</c>).</summary>
public enum ApplicationIntakeChannel
{
    InApp,
    GoogleForm,
    Paper
}
