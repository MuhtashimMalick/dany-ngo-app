using NgoFund.Application.Intake;

namespace NgoFund.UnitTests.Intake;

public class FormValueParserTests
{
    [Theory]
    [InlineData("Groom's Jamaat / Community", "Groom's Jamaat / Community")]
    [InlineData("Groom's Jamaat  /  Community", "Groom's Jamaat / Community")]
    [InlineData("Groom’s Jamaat — Community", "Groom’s Jamaat - Community")]
    [InlineData("  Father's Name  ", "Father's Name")]
    public void NormalizeTitle_CollapsesWhitespaceAndUnifiesDashes(string raw, string expected) =>
        Assert.Equal(expected, FormValueParser.NormalizeTitle(raw));

    [Theory]
    [InlineData("Katchi (temporary structure)", "Katchi")]
    [InlineData("First Marriage (کنوارہ)", "First Marriage")]
    [InlineData("Housing Assistance (رہائشی مکان مدد)", "Housing Assistance")]
    [InlineData("Owned", "Owned")]
    public void StripOptionParenthetical_RemovesTrailingParenOnly(string raw, string expected) =>
        Assert.Equal(expected, FormValueParser.StripOptionParenthetical(raw));

    [Theory]
    [InlineData("42101-3704613-7", "42101-3704613-7")]
    [InlineData("4210137046137", "42101-3704613-7")]
    [InlineData("42101 3704613 7", "42101-3704613-7")]
    public void NormalizeCnicOrNull_13Digits_Formats(string raw, string expected) =>
        Assert.Equal(expected, FormValueParser.NormalizeCnicOrNull(raw));

    [Theory]
    [InlineData("123")]
    [InlineData("not a cnic")]
    [InlineData(null)]
    public void NormalizeCnicOrNull_NotThirteenDigits_ReturnsNull(string? raw) =>
        Assert.Null(FormValueParser.NormalizeCnicOrNull(raw));

    [Theory]
    [InlineData("0333-2909639", "0333-2909639")]
    [InlineData("03332909639", "0333-2909639")]
    [InlineData("923332909639", "0333-2909639")]
    public void NormalizePhoneOrNull_ValidDigits_Formats(string raw, string expected) =>
        Assert.Equal(expected, FormValueParser.NormalizePhoneOrNull(raw));

    [Theory]
    [InlineData("Rs. 50,000", 50000)]
    [InlineData("50000", 50000)]
    [InlineData(" 12,345.50 ", 12345.50)]
    [InlineData("75%", 75)]
    public void ParseDecimalOrNull_LenientFormats_Parse(string raw, decimal expected) =>
        Assert.Equal(expected, FormValueParser.ParseDecimalOrNull(raw));

    [Fact]
    public void ParseDecimalOrNull_Garbage_ReturnsNull() => Assert.Null(FormValueParser.ParseDecimalOrNull("not a number"));

    [Fact]
    public void ParseDateOrNull_IsoFormat_Parses() => Assert.Equal(new DateOnly(2026, 6, 15), FormValueParser.ParseDateOrNull("2026-06-15"));

    [Fact]
    public void ParseDateOrNull_OtherFormat_ReturnsNull() => Assert.Null(FormValueParser.ParseDateOrNull("15/06/2026"));

    [Theory]
    [InlineData("Yes", true)]
    [InlineData("yes", true)]
    [InlineData("No", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void ParseYesNo_MapsCorrectly(string? raw, bool expected) => Assert.Equal(expected, FormValueParser.ParseYesNo(raw));
}
