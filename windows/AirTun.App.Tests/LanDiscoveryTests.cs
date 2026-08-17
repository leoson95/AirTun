using System.Text;
using AirTun.Core;
using Xunit;

namespace AirTun.App.Tests;

public class LanDiscoveryTests
{
    [Fact]
    public void ProbeDatagramMatchesProtocolSpecification()
    {
        var probeBytes = LanDiscovery.ProbeDatagram();
        var json = Encoding.UTF8.GetString(probeBytes);

        Assert.Contains("\"app\":\"airtun\"", json);
        Assert.Contains("\"v\":1", json);
        Assert.Contains("\"probe\":1", json);
    }

    [Fact]
    public void TryParseBeaconParsesValidAndroidBeacon()
    {
        var json = """{"app":"airtun","v":1,"device":"Samsung Galaxy S24","port":10808,"pin_required":true}""";
        var bytes = Encoding.UTF8.GetBytes(json);
        var seen = DateTimeOffset.UtcNow;

        var success = LanDiscovery.TryParseBeacon(bytes, seen, "192.168.43.1", out var device);

        Assert.True(success);
        Assert.NotNull(device);
        Assert.Equal("192.168.43.1", device.Host);
        Assert.Equal(10808, device.PortNumber);
        Assert.Equal("Samsung Galaxy S24", device.DeviceName);
        Assert.True(device.PinRequired);
    }

    [Fact]
    public void TryParseBeaconRejectsForeignApp()
    {
        var json = """{"app":"otherapp","v":1,"device":"Samsung","port":10808}""";
        var bytes = Encoding.UTF8.GetBytes(json);

        var success = LanDiscovery.TryParseBeacon(bytes, DateTimeOffset.UtcNow, "192.168.43.1", out var device);

        Assert.False(success);
        Assert.Null(device);
    }

    [Fact]
    public void ObserveAddsAndUpdatesDevices()
    {
        var discovery = new LanDiscovery();
        var json = """{"app":"airtun","v":1,"device":"Pixel 8","port":10808,"pin_required":true}""";
        var bytes = Encoding.UTF8.GetBytes(json);

        discovery.Observe(bytes, "192.168.43.1");

        var devices = discovery.Devices;
        Assert.Single(devices);
        Assert.Equal("Pixel 8", devices[0].DeviceName);
        Assert.Equal("192.168.43.1", devices[0].Host);
    }
}
