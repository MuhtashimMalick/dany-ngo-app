namespace NgoFund.Contracts.Donors;

public record CreateDonorRequest(
    string FullName,
    string DonorType,
    string? Cnic,
    string? Ntn,
    string? MembershipNumber,
    string? Phone,
    string? AlternatePhone,
    string? Email,
    string? Address,
    string? City,
    string? Country,
    bool IsAnonymous,
    string? Notes);
