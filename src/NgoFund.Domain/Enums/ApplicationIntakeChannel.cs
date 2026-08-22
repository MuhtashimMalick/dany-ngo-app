namespace NgoFund.Domain.Enums;

/// <summary>How an application originally reached the org — staff re-keying a Google Form
/// submission or a paper form is a first-class case, not an afterthought (see <c>docs/schema.md</c>).</summary>
public enum ApplicationIntakeChannel
{
    InApp,
    GoogleForm,
    Paper
}
