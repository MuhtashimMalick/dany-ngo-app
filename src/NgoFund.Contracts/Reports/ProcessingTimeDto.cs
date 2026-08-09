namespace NgoFund.Contracts.Reports;

/// <summary>Null averages mean <see cref="DecidedCount"/> is 0 — the UI renders "Not enough data" instead of 0/NaN.</summary>
public record ProcessingTimeDto(double? AverageDays, double? MedianDays, int DecidedCount, int WindowDays);
