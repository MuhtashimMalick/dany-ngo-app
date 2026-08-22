namespace NgoFund.Contracts.Applications;

public record BusinessLoanApplicationDetailsDto(
    Guid ApplicationId,
    string? PaperFormNumber,
    string? BusinessPhone,
    string? Education,
    string? Skill,
    string? Experience,
    string? OtherIncomeSources,
    decimal? TotalMonthlyExpenses,
    string ProposedBusinessDescription,
    string? ProposedBusinessLocation,
    decimal? CapitalRequired,
    decimal? CapitalAlreadyAvailable,
    bool HasPriorBusinessExperience,
    string? PriorBusinessDetails,
    string? EmergencyContactName,
    string? EmergencyContactCnic,
    string? EmergencyContactPhone,
    /// <summary>
    /// Non-fatal: set when <c>CapitalRequired - CapitalAlreadyAvailable</c> doesn't roughly match
    /// the application's <c>RequestedAmount</c>. Staff must be able to re-key the form faithfully
    /// even when the applicant's own arithmetic doesn't add up, so this is a warning string on the
    /// response, never a validation error. Null when there's nothing to flag (including when
    /// either capital figure is missing).
    /// </summary>
    string? AmountMismatchWarning);
