namespace NgoFund.Domain.Enums;

/// <summary>
/// Shared by both <c>Donation</c> (money in) and <c>Payment</c> (money out) — one enum instead
/// of two near-identical ones. <see cref="InKind"/> only applies to donations; the application
/// layer validator rejects it for payments rather than the type system carrying two enums.
/// </summary>
public enum PaymentMethod
{
    Cash,
    BankTransfer,
    Cheque,
    Online,
    InKind
}
