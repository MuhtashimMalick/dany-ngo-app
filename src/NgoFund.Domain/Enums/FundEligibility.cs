namespace NgoFund.Domain.Enums;

/// <summary>
/// Which fund type(s) an <see cref="Entities.ApplicationCategory"/> may be paid from — the
/// three-state replacement for the old <c>IsZakatEligible</c> boolean, which couldn't express
/// "must be Zakat" separately from "may be Zakat". See
/// <see cref="Entities.FundApplication.EnsureFundIsCompatible"/> for the rule.
/// </summary>
public enum FundEligibility
{
    /// <summary>May only be paid from a Zakat fund.</summary>
    ZakatOnly,

    /// <summary>May only be paid from a non-Zakat (General) fund.</summary>
    GeneralOnly,

    /// <summary>May be paid from either fund type.</summary>
    Either
}
