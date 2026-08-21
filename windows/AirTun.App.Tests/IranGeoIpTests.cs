using System.Net;
using AirTun.Core.Routing;
using Xunit;

namespace AirTun.App.Tests;

public class IranGeoIpTests
{
    [Fact]
    public void IranCidrsContainsExactly254UniqueValidPrefixes()
    {
        Assert.Equal(254, IranGeoIp.IranCidrs.Count);

        var uniqueSet = new HashSet<string>(IranGeoIp.IranCidrs, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(254, uniqueSet.Count);

        foreach (var cidr in IranGeoIp.IranCidrs)
        {
            var parts = cidr.Split('/');
            Assert.True(IPAddress.TryParse(parts[0], out var ip), $"Invalid IP in CIDR {cidr}");
            Assert.True(int.TryParse(parts[1], out var prefix), $"Invalid prefix in CIDR {cidr}");
            Assert.InRange(prefix, 1, 32);
        }
    }

    [Theory]
    [InlineData("2.144.10.1", true)]
    [InlineData("5.160.20.30", true)]
    [InlineData("31.2.128.1", true)]
    [InlineData("37.152.0.1", true)]
    [InlineData("46.100.5.5", true)]
    [InlineData("185.10.72.10", true)]
    [InlineData("217.218.0.1", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("1.1.1.1", false)]
    [InlineData("142.250.190.46", false)]
    [InlineData("104.244.42.1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsIranIpIdentifiesDomesticIpsCorrectly(string? ip, bool expectedResult)
    {
        Assert.Equal(expectedResult, IranGeoIp.IsIranIp(ip!));
    }

    [Fact]
    public void IsIranIpWithIPAddressObjectMatchesCorrectly()
    {
        Assert.True(IranGeoIp.IsIranIp(IPAddress.Parse("5.160.1.1")));
        Assert.True(IranGeoIp.IsIranIp(IPAddress.Parse("185.10.72.1")));
        Assert.False(IranGeoIp.IsIranIp(IPAddress.Parse("8.8.8.8")));
        Assert.False(IranGeoIp.IsIranIp(IPAddress.Parse("127.0.0.1")));
    }

    [Fact]
    public void IsIranIpHighThroughputBenchmark()
    {
        var testIp = IPAddress.Parse("185.10.72.10");
        for (int i = 0; i < 10000; i++)
        {
            Assert.True(IranGeoIp.IsIranIp(testIp));
        }
    }

    [Fact]
    public void CreateIranRoutesGenerates254ConfiguredRouteEntries()
    {
        var routes = IranGeoIp.CreateIranRoutes("192.168.43.1", interfaceIndex: 12, metric: 10);

        Assert.Equal(254, routes.Count);
        Assert.All(routes, r =>
        {
            Assert.Equal("192.168.43.1", r.Gateway);
            Assert.Equal(12, r.InterfaceIndex);
            Assert.Equal(10, r.Metric);
            Assert.StartsWith("IR:", r.Tag);
        });
    }
}
