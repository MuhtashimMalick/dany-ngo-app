using NgoFund.Domain.Applications;
using NgoFund.Domain.Enums;

namespace NgoFund.UnitTests.Domain;

public class MaritalStatusMapperTests
{
    [Theory]
    [InlineData("First Marriage", MaritalStatus.Single)]
    [InlineData("Widow", MaritalStatus.Widowed)]
    [InlineData("Widower", MaritalStatus.Widowed)]
    [InlineData("Divorced", MaritalStatus.Divorced)]
    public void MapFormLabel_KnownFormLabel_MapsToExpectedStatus(string label, MaritalStatus expected)
    {
        Assert.Equal(expected, MaritalStatusMapper.MapFormLabel(label));
    }

    [Theory]
    [InlineData("Single", MaritalStatus.Single)]
    [InlineData("Married", MaritalStatus.Married)]
    [InlineData("Widowed", MaritalStatus.Widowed)]
    [InlineData("Divorced", MaritalStatus.Divorced)]
    [InlineData("married", MaritalStatus.Married)]
    public void MapFormLabel_EnumMemberName_MapsDirectly(string label, MaritalStatus expected)
    {
        Assert.Equal(expected, MaritalStatusMapper.MapFormLabel(label));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MapFormLabel_NullOrBlank_ReturnsNull(string? label)
    {
        Assert.Null(MaritalStatusMapper.MapFormLabel(label));
    }

    [Fact]
    public void MapFormLabel_UnrecognizedLabel_Throws()
    {
        Assert.Throws<ArgumentException>(() => MaritalStatusMapper.MapFormLabel("Engaged"));
    }
}
