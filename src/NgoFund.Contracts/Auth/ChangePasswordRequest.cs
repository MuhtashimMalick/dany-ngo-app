namespace NgoFund.Contracts.Auth;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
