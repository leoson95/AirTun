using System.Net.NetworkInformation;

namespace AirTun.Core;

public sealed class TunnelStats
{
    public sealed record Sample(
        long BytesUp,
        long BytesDown,
        double UploadRateBps,
        double DownloadRateBps,
        int LatencyMs,
        DateTimeOffset Timestamp
    );

    private long _lastBytesUp;
    private long _lastBytesDown;
    private DateTimeOffset _lastSampleTime = DateTimeOffset.MinValue;
    private readonly Ping _ping = new();

    public Sample ComputeSample(long currentBytesUp, long currentBytesDown, int latencyMs)
    {
        var now = DateTimeOffset.UtcNow;
        double upRate = 0;
        double downRate = 0;

        if (_lastSampleTime != DateTimeOffset.MinValue)
        {
            var elapsedSeconds = (now - _lastSampleTime).TotalSeconds;
            if (elapsedSeconds > 0.1)
            {
                var deltaUp = Math.Max(0, currentBytesUp - _lastBytesUp);
                var deltaDown = Math.Max(0, currentBytesDown - _lastBytesDown);
                upRate = deltaUp / elapsedSeconds;
                downRate = deltaDown / elapsedSeconds;
            }
        }

        _lastBytesUp = currentBytesUp;
        _lastBytesDown = currentBytesDown;
        _lastSampleTime = now;

        return new Sample(
            BytesUp: currentBytesUp,
            BytesDown: currentBytesDown,
            UploadRateBps: upRate,
            DownloadRateBps: downRate,
            LatencyMs: latencyMs,
            Timestamp: now
        );
    }

    public async Task<int> MeasurePingAsync(string host, int timeoutMs = 1500)
    {
        try
        {
            var reply = await _ping.SendPingAsync(host, timeoutMs).ConfigureAwait(false);
            if (reply.Status == IPStatus.Success)
            {
                return (int)reply.RoundtripTime;
            }
        }
        catch
        {
        }
        return -1;
    }

    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_000_000_000 => $"{bytes / 1_000_000_000.0:F1} GB",
        >= 1_000_000 => $"{bytes / 1_000_000.0:F1} MB",
        >= 1_000 => $"{bytes / 1_000.0:F1} KB",
        _ => $"{bytes} B",
    };

    public static string FormatRate(double bytesPerSec) => bytesPerSec switch
    {
        >= 1_000_000 => $"{bytesPerSec / 1_000_000.0:F1} MB/s",
        >= 1_000 => $"{bytesPerSec / 1_000.0:F1} KB/s",
        _ => $"{(long)bytesPerSec} B/s",
    };
}
