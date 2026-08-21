using System.Net;
using AirTun.Core.Routing;
using Xunit;

namespace AirTun.App.Tests;

public class RouteEntryTests
{
    [Theory]
    [InlineData("10.0.0.0/8", "10.0.0.0", "255.0.0.0")]
    [InlineData("10.5.20.30/8", "10.0.0.0", "255.0.0.0")]
    [InlineData("172.16.0.0/12", "172.16.0.0", "255.240.0.0")]
    [InlineData("172.20.10.5/12", "172.16.0.0", "255.240.0.0")]
    [InlineData("2.144.0.0/14", "2.144.0.0", "255.252.0.0")]
    [InlineData("192.168.0.0/16", "192.168.0.0", "255.255.0.0")]
    [InlineData("192.168.1.0/24", "192.168.1.0", "255.255.255.0")]
    [InlineData("192.168.1.150/24", "192.168.1.0", "255.255.255.0")]
    [InlineData("1.1.1.1/32", "1.1.1.1", "255.255.255.255")]
    [InlineData("8.8.8.8", "8.8.8.8", "255.255.255.255")]
    public void ParseCidrCalculatesCorrectDestinationAndMask(string cidr, string expectedDest, string expectedMask)
    {
        var (dest, mask) = RouteEntry.ParseCidr(cidr);
        Assert.Equal(expectedDest, dest);
        Assert.Equal(expectedMask, mask);
    }

    [Fact]
    public void FromCidrCreatesValidRouteEntry()
    {
        var route = RouteEntry.FromCidr("192.168.0.0/16", "192.168.43.1", 5, 10, "LAN");

        Assert.Equal("192.168.0.0", route.Destination);
        Assert.Equal("255.255.0.0", route.Mask);
        Assert.Equal("192.168.43.1", route.Gateway);
        Assert.Equal(5, route.InterfaceIndex);
        Assert.Equal(10, route.Metric);
        Assert.Equal("LAN", route.Tag);
    }

    [Fact]
    public void ForHostCreates32BitHostRoute()
    {
        var route = RouteEntry.ForHost("1.2.3.4", "192.168.43.1", 7, 5);

        Assert.Equal("1.2.3.4", route.Destination);
        Assert.Equal("255.255.255.255", route.Mask);
        Assert.Equal("192.168.43.1", route.Gateway);
        Assert.Equal(7, route.InterfaceIndex);
        Assert.Equal(5, route.Metric);
    }

    [Theory]
    [InlineData("192.168.0.0/16", "192.168.1.1", true)]
    [InlineData("192.168.0.0/16", "192.168.254.254", true)]
    [InlineData("192.168.0.0/16", "192.169.1.1", false)]
    [InlineData("10.0.0.0/8", "10.254.1.2", true)]
    [InlineData("10.0.0.0/8", "11.0.0.1", false)]
    [InlineData("5.160.0.0/13", "5.167.255.254", true)]
    [InlineData("5.160.0.0/13", "5.168.0.1", false)]
    [InlineData("1.1.1.1/32", "1.1.1.1", true)]
    [InlineData("1.1.1.1/32", "1.1.1.2", false)]
    [InlineData("0.0.0.0/0", "8.8.8.8", true)]
    [InlineData("0.0.0.0/0", "192.168.1.1", true)]
    public void ContainsIpEvaluatesSubnetMembershipAccurately(string cidr, string testIp, bool expectedResult)
    {
        var route = RouteEntry.FromCidr(cidr, "192.168.1.1");
        Assert.Equal(expectedResult, route.ContainsIp(testIp));
    }

    [Fact]
    public void ContainsIpHandlesInvalidAndIpv6AddressesGracefully()
    {
        var route = RouteEntry.FromCidr("192.168.0.0/16", "192.168.1.1");
        Assert.False(route.ContainsIp(""));
        Assert.False(route.ContainsIp("   "));
        Assert.False(route.ContainsIp((string)null!));
        Assert.False(route.ContainsIp((IPAddress)null!));
        Assert.False(route.ContainsIp("not-an-ip"));
        Assert.False(route.ContainsIp("2001:db8::1"));
        Assert.False(route.ContainsIp("::1"));
    }

    [Theory]
    [InlineData(0, "0.0.0.0")]
    [InlineData(8, "255.0.0.0")]
    [InlineData(16, "255.255.0.0")]
    [InlineData(24, "255.255.255.0")]
    [InlineData(32, "255.255.255.255")]
    public void PrefixToMaskCalculatesValidSubnetMasks(int prefix, string expectedMask)
    {
        Assert.Equal(expectedMask, RouteEntry.PrefixToMask(prefix));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(33)]
    [InlineData(100)]
    public void PrefixToMaskThrowsOnOutOfRangePrefix(int prefix)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RouteEntry.PrefixToMask(prefix));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ParseCidrThrowsOnEmptyOrNullCidr(string? cidr)
    {
        Assert.Throws<ArgumentException>(() => RouteEntry.ParseCidr(cidr!));
    }
}
