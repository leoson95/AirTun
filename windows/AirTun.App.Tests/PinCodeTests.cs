using AirTun.Core;
using Xunit;

namespace AirTun.App.Tests;

public class PinCodeTests
{
    [Theory]
    [InlineData("1234", true)]
    [InlineData("9999", true)]
    [InlineData("1000", true)]
    [InlineData("0123", true)]
    [InlineData("123", false)]
    [InlineData("12345", false)]
    [InlineData("12a4", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsValidValidatesFourDigits(string? input, bool expected)
    {
        Assert.Equal(expected, PinCode.IsValid(input));
    }

    [Fact]
    public void NormalizeStripsWhitespace()
    {
        Assert.Equal("1234", PinCode.Normalize(" 1 2 3 4 "));
        Assert.Equal("9876", PinCode.Normalize("  9876 "));
        Assert.Null(PinCode.Normalize("12 3"));
        Assert.Null(PinCode.Normalize("abcd"));
    }
}
