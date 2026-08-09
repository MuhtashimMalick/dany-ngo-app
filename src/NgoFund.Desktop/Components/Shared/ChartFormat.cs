namespace NgoFund.Desktop.Components.Shared;

/// <summary>
/// Tiny formatting helper shared by the Dashboard and Reports hand-built SVG charts — abbreviated
/// axis labels ("1.2M" instead of "1234567.00") so a Y-axis doesn't force the chart wider than its
/// card. Not a general number-formatting library, just the one thing both charts need.
/// </summary>
public static class ChartFormat
{
    public static string Abbreviate(decimal value)
    {
        var abs = Math.Abs(value);
        return abs switch
        {
            >= 1_000_000 => $"{value / 1_000_000:0.#}M",
            >= 1_000 => $"{value / 1_000:0.#}K",
            _ => value.ToString("0"),
        };
    }
}
