namespace NgoFund.Contracts.Applications;

public record UpsertBusinessLoanApplicationDetailsRequest(
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
    string? EmergencyContactPhone);
