using System.Globalization;
using System.Text.RegularExpressions;

namespace NgoFund.Application.Intake;

/// <summary>
/// Pure, EF-free parsing helpers for Google Form answer text — lenient by design (C5): a value
/// that doesn't parse becomes null, never an exception, so one malformed answer never blocks the
/// rest of a submission. The caller is responsible for adding a note when a parse fails on a
/// non-empty value (<see cref="GoogleFormSubmissionMapper"/> does this centrally).
/// </summary>
public static class FormValueParser
{
    /// <summary>Trims, collapses internal whitespace, and unifies em/en dash variants to a plain
    /// hyphen — the noise visible in the actual form question titles. Case is left alone; callers
    /// compare titles case-insensitively.</summary>
    public static string NormalizeTitle(string title) =>
        Regex.Replace(title.Trim().Replace('—', '-').Replace('–', '-'), @"\s+", " ");

    /// <summary>Strips a trailing " (...)" parenthetical from a radio/checkbox OPTION value only —
    /// e.g. "Katchi (temporary structure)" -&gt; "Katchi", "First Marriage (کنوارہ)" -&gt; "First Marriage".
    /// Never applied to free-text answers or question titles.</summary>
    public static string StripOptionParenthetical(string value)
    {
        var trimmed = value.Trim();
        var parenIndex = trimmed.IndexOf(" (", StringComparison.Ordinal);
        return parenIndex > 0 ? trimmed[..parenIndex].Trim() : trimmed;
    }

    /// <summary>13 digits -&gt; 5-7-1 dashed CNIC format; anything else -&gt; null.</summary>
    public static string? NormalizeCnicOrNull(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return digits.Length == 13 ? $"{digits[..5]}-{digits[5..12]}-{digits[12..]}" : null;
    }

    /// <summary>Normalizes to the "03XX-XXXXXXX" mobile pattern when the digits fit (11 digits,
    /// optionally with a leading country code stripped); otherwise null.</summary>
    public static string? NormalizePhoneOrNull(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var digits = new string(raw.Where(char.IsDigit).ToArray());

        // Strip a leading "92" country code (e.g. "923332909639" -> "03332909639").
        if (digits.Length == 12 && digits.StartsWith("92", StringComparison.Ordinal))
        {
            digits = "0" + digits[2..];
        }

        return digits.Length == 11 && digits.StartsWith('0') ? $"{digits[..4]}-{digits[4..]}" : null;
    }

    /// <summary>Strips "Rs", commas, "%" and surrounding whitespace before parsing — lenient money/percent input.</summary>
    public static decimal? ParseDecimalOrNull(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cleaned = Regex.Replace(raw, "(?i)rs\\.?|,|%|\\s", "");
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    public static int? ParseIntOrNull(string? raw)
    {
        var value = ParseDecimalOrNull(raw);
        return value is null ? null : (int)Math.Round(value.Value, MidpointRounding.AwayFromZero);
    }

    /// <summary>The form's own date-answer format, "yyyy-MM-dd".</summary>
    public static DateOnly? ParseDateOrNull(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && DateOnly.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    /// <summary>"Yes"/"No" (case-insensitive) -&gt; bool; anything else -&gt; false (the "None"
    /// checkbox and an absent answer both correctly read as false).</summary>
    public static bool ParseYesNo(string? raw) => string.Equals(raw?.Trim(), "Yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>A single-option checkbox (e.g. a "Declaration"/"Applicant's Declaration" oath) whose
    /// option text is the full oath sentence, not "Yes" — any non-empty selected value counts as
    /// ticked. <see cref="ParseYesNo"/> does not apply here since the option is never literally "Yes".</summary>
    public static bool IsTicked(string? raw) => !string.IsNullOrWhiteSpace(raw);
}
