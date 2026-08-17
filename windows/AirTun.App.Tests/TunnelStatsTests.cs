using AirTun.Core;
using Xunit;

namespace AirTun.App.Tests;

public class TunnelStatsTests
{
    [Fact]
    public void FormatBytesProducesHumanReadableUnits()
    {
        Assert.Equal("500 B", TunnelStats.FormatBytes(500));
        Assert.Equal("1.5 KB", TunnelStats.FormatBytes(1536));
        Assert.Equal("10.0 MB", TunnelStats.FormatBytes(10_000_000));
        Assert.Equal("2.5 GB", TunnelStats.FormatBytes(2_500_000_000));
    }

    [Fact]
    public void FormatRateProducesHumanReadableRates()
    {
        Assert.Equal("500 B/s", TunnelStats.FormatRate(500));
        Assert.Equal("2.5 KB/s", TunnelStats.FormatRate(2500));
        Assert.Equal("12.5 MB/s", TunnelStats.FormatRate(12_500_000));
    }

    [Fact]
    public void ComputeSampleCalculatesBandwidthRate()
    {
        var stats = new TunnelStats();
        var s1 = stats.ComputeSample(0, 0, 15);
        Assert.Equal(0, s1.UploadRateBps);
        Assert.Equal(0, s1.DownloadRateBps);

        Thread.Sleep(200);
        var s2 = stats.ComputeSample(200_000, 1_000_000, 20);
        Assert.True(s2.UploadRateBps > 0);
        Assert.True(s2.DownloadRateBps > 0);
        Assert.Equal(20, s2.LatencyMs);
    }
}
