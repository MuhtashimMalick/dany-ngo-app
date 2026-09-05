namespace NgoFund.Contracts.Common;

/// <summary>
/// Pakistani-format identifier rules shared between Application-layer FluentValidation validators
/// (server) and Desktop: regex patterns for CNIC and mobile phone, used by both the server
/// validators and Desktop's live-format validation (<c>FormatField</c>); plus the Jamaat membership
/// number's max length, a plain length cap (no regex — see <see cref="JamaatMembershipMaxLength"/>)
/// shared by the server validators, the EF configurations, and Desktop's <c>maxlength</c> attribute.
/// Lives in Contracts, not Domain, because Desktop never references Domain directly.
/// </summary>
public static class PakistaniFormats
{
    /// <summary>e.g. "42101-3704613-7" (length 15).</summary>
    public const string CnicPattern = @"^\d{5}-\d{7}-\d{1}$";

    /// <summary>e.g. "0333-2909639" (length 12).</summary>
    public const string MobilePhonePattern = @"^03\d{2}-\d{7}$";

    /// <summary>Digit count of a CNIC once dashes are stripped (5+7+1) — the cap Desktop's live
    /// input mask (<c>FormatField</c>) enforces while auto-inserting dashes as the user types.</summary>
    public const int CnicDigitCount = 13;

    /// <summary>Digit count of a mobile number once dashes are stripped (4+7) — the cap Desktop's
    /// live input mask (<c>FormatField</c>) enforces while auto-inserting dashes as the user types.</summary>
    public const int MobilePhoneDigitCount = 11;

    /// <summary>
    /// Jamaat membership numbers are deliberately free text with no prefix/format requirement — the
    /// client dropped the "J-" prefix format in 2026-08 feedback, so only a length cap remains. This
    /// is the single source shared by server validators, EF configs and the Desktop `maxlength`
    /// attribute for every Jamaat membership number field (applicant, applicant's father, donor,
    /// guarantor — all four are the same real-world identifier).
    /// </summary>
    public const int JamaatMembershipMaxLength = 10;

    public const string CnicExample = "42101-3704613-7";
    public const string MobilePhoneExample = "0333-2909639";

    public const string CnicMessage = "CNIC must be in the format 42101-3704613-7.";
    public const string MobilePhoneMessage = "Mobile number must be in the format 0333-2909639.";
}
