using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Applications;

/// <summary>
/// Maps the Marriage Google Form's bride/groom marital-status labels onto the one shared
/// <see cref="MaritalStatus"/> enum (First Marriage -&gt; Single, Widow -&gt; Widowed, Divorced -&gt;
/// Divorced) rather than introducing a second, form-specific enum. Also accepts the enum's own
/// member names directly, so a UI dropdown built straight off <see cref="MaritalStatus"/> works
/// without going through the form-label synonyms.
/// </summary>
public static class MaritalStatusMapper
{
    public static MaritalStatus? MapFormLabel(string? formLabel)
    {
        if (string.IsNullOrWhiteSpace(formLabel))
        {
            return null;
        }

        var trimmed = formLabel.Trim();

        return trimmed switch
        {
            "First Marriage" => MaritalStatus.Single,
            "Widow" or "Widower" => MaritalStatus.Widowed,
            "Divorced" => MaritalStatus.Divorced,
            _ when Enum.TryParse<MaritalStatus>(trimmed, ignoreCase: true, out var parsed) => parsed,
            _ => throw new ArgumentException($"'{formLabel}' is not a recognized marital status.", nameof(formLabel)),
        };
    }
}
