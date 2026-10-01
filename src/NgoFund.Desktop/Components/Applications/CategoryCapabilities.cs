namespace NgoFund.Desktop.Components.Applications;

/// <summary>Which category-detail extension a category has, if any — drives which *DetailsSection
/// the manage modal's Details tab renders. <see cref="None"/> covers EMERGENCY (still no manifest/
/// extension at all) and any future category added without a details table.</summary>
public enum CategoryDetailsSectionKind
{
    None,
    Housing,
    Marriage,
    BusinessLoan,
    Education,
    Health,
}

/// <summary>
/// Single source of truth for "what does this application category code support" on the Desktop —
/// replaces three separate hardcoded <c>is "HOUSE_RENT" or "SHAADI" or "ROZGAR"</c>-style checks
/// (ApplicationWizard's/Applications' own <c>CategoryHasManifest</c>, and the manage modal's
/// category-detail-section switch) with one place to add a category. Mirrors the server-side
/// manifest membership in <c>NgoFund.Domain.Applications.ApplicationRequirements.SlotsByCategory</c>
/// — <see cref="HasManifest"/> must stay in sync with which categories that dictionary keys on,
/// since it's what drives the completeness checklist and the Approve-gate mirror client-side.
/// </summary>
public static class CategoryCapabilities
{
    /// <summary>Categories with a document/field completeness manifest — every category except
    /// EMERGENCY as of the Google Form intake milestone (EDUCATION/HEALTH/OTHER joined
    /// HOUSE_RENT/SHAADI/ROZGAR, which already had one).</summary>
    public static bool HasManifest(string? categoryCode) => categoryCode is
        "HOUSE_RENT" or "SHAADI" or "ROZGAR" or "EDUCATION" or "HEALTH" or "OTHER";

    /// <summary>Which *DetailsSection component (if any) the manage modal's Details tab renders for
    /// this category code. OTHER has a manifest (<see cref="HasManifest"/>) but no extension table
    /// of its own — <see cref="CategoryDetailsSectionKind.None"/>, same as EMERGENCY.</summary>
    public static CategoryDetailsSectionKind DetailsSectionKind(string? categoryCode) => categoryCode switch
    {
        "HOUSE_RENT" => CategoryDetailsSectionKind.Housing,
        "SHAADI" => CategoryDetailsSectionKind.Marriage,
        "ROZGAR" => CategoryDetailsSectionKind.BusinessLoan,
        "EDUCATION" => CategoryDetailsSectionKind.Education,
        "HEALTH" => CategoryDetailsSectionKind.Health,
        _ => CategoryDetailsSectionKind.None,
    };
}
