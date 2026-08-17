using AirTun.Core.Geo;
using Xunit;

namespace AirTun.App.Tests;

public class GeoIpServiceTests
{
    [Theory]
    [InlineData("DE", "🇩🇪")]
    [InlineData("IR", "🇮🇷")]
    [InlineData("FI", "🇫🇮")]
    [InlineData("US", "🇺🇸")]
    [InlineData("NL", "🇳🇱")]
    [InlineData("GB", "🇬🇧")]
    [InlineData("", "🌐")]
    [InlineData(null, "🌐")]
    public void CountryCodeToEmojiConvertsCorrectly(string? code, string expectedEmoji)
    {
        Assert.Equal(expectedEmoji, GeoIpService.CountryCodeToEmoji(code));
    }
}
