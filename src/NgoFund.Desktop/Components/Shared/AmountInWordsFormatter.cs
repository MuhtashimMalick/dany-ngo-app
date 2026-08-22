namespace NgoFund.Desktop.Components.Shared;

/// <summary>
/// Small, dependency-free number-to-words formatter for printable receipts' "amount in words"
/// line — good enough for typical disbursement/repayment amounts (up to hundreds of millions),
/// not a general currency-in-words library. Shared between the Payment receipt (Payments.razor)
/// and the Loan repayment receipt (LoanSchedulePanel.razor) so the two receipts read identically
/// instead of each page carrying its own copy ('s DRY rule).
/// </summary>
public static class AmountInWordsFormatter
{
    private static readonly string[] Ones =
    [
        "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
        "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen",
    ];

    private static readonly string[] Tens =
        ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

    public static string Convert(decimal amount)
    {
        var whole = (long)Math.Floor(amount);
        if (whole == 0)
        {
            return "Zero";
        }

        var segments = new List<string>();
        long crore = whole / 10_000_000; whole %= 10_000_000;
        long lakh = whole / 100_000; whole %= 100_000;
        long thousand = whole / 1_000; whole %= 1_000;
        long remainder = whole;

        if (crore > 0) segments.Add($"{ConvertThreeDigit(crore)} Crore");
        if (lakh > 0) segments.Add($"{ConvertThreeDigit(lakh)} Lakh");
        if (thousand > 0) segments.Add($"{ConvertThreeDigit(thousand)} Thousand");
        if (remainder > 0) segments.Add(ConvertThreeDigit(remainder));

        return string.Join(" ", segments);
    }

    private static string ConvertThreeDigit(long n)
    {
        var parts = new List<string>();
        if (n >= 100) { parts.Add(Ones[n / 100] + " Hundred"); n %= 100; }
        if (n >= 20) { parts.Add(Tens[n / 10]); n %= 10; }
        if (n > 0) { parts.Add(Ones[n]); }
        return string.Join(" ", parts);
    }
}
