namespace NgoFund.Domain.Entities;

/// <summary>
/// Category-specific fields for a ROZGAR application, re-keyed from the Business Loan Google Form.
/// Shares its primary key with <see cref="FundApplication"/> (true 1:1). The "Loan Amount Needed"
/// field from the form is deliberately NOT duplicated here — it is
/// <see cref="FundApplication.RequestedAmount"/>. Only the supporting capital context
/// (<see cref="CapitalRequired"/>/<see cref="CapitalAlreadyAvailable"/>) lives on this table.
/// </summary>
public class BusinessLoanApplicationDetails
{
    public Guid ApplicationId { get; set; }
    public FundApplication Application { get; set; } = null!;

    public string? PaperFormNumber { get; set; }
    public string? BusinessPhone { get; set; }
    public string? Education { get; set; }
    public string? Skill { get; set; }
    public string? Experience { get; set; }
    public string? OtherIncomeSources { get; set; }
    public decimal? TotalMonthlyExpenses { get; set; }

    public string ProposedBusinessDescription { get; set; } = null!;
    public string? ProposedBusinessLocation { get; set; }

    public decimal? CapitalRequired { get; set; }
    public decimal? CapitalAlreadyAvailable { get; set; }

    public bool HasPriorBusinessExperience { get; set; }
    public string? PriorBusinessDetails { get; set; }

    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactCnic { get; set; }
    public string? EmergencyContactPhone { get; set; }
}
