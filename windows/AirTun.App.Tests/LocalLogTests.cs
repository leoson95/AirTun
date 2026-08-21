using AirTun.App.Services;
using Xunit;

namespace AirTun.App.Tests;

public class LocalLogTests
{
    [Fact]
    public void LogEntriesContainFormattedClockTimestampAndTag()
    {
        LocalLog.Clear();

        LocalLog.Add("System starting", "INFO");
        LocalLog.Routing("Domestic .ir bypass active: 254 CIDR ranges routed direct");
        LocalLog.Tun("Wintun adapter initialized");
        LocalLog.Discovery("Phone beacon found on 192.168.43.1");
        LocalLog.Error("Connection timeout");

        var snapshot = LocalLog.Snapshot();
        Assert.Equal(5, snapshot.Count);

        Assert.Equal("INFO", snapshot[0].Tag);
        Assert.Equal("System starting", snapshot[0].Message);

        Assert.Equal("ROUTING", snapshot[1].Tag);
        Assert.Equal("Domestic .ir bypass active: 254 CIDR ranges routed direct", snapshot[1].Message);

        Assert.Equal("TUN", snapshot[2].Tag);
        Assert.Equal("Wintun adapter initialized", snapshot[2].Message);

        Assert.Equal("DISCOVERY", snapshot[3].Tag);
        Assert.Equal("Phone beacon found on 192.168.43.1", snapshot[3].Message);

        Assert.Equal("ERROR", snapshot[4].Tag);
        Assert.Equal("Connection timeout", snapshot[4].Message);

        foreach (var entry in snapshot)
        {
            var str = entry.ToString();
            // Matches [HH:mm:ss] [TAG] Message
            Assert.Matches(@"^\[\d{2}:\d{2}:\d{2}\] \[[A-Z]+\] .+$", str);
        }
    }

    [Fact]
    public void AutoDetectsEmbeddedTagsInRawMessages()
    {
        LocalLog.Clear();

        LocalLog.Add("[ROUTING] Custom bypass added: soft98.ir -> Direct");
        LocalLog.Add("[TUN] Session established");
        LocalLog.Add("[DISCOVERY] Probing subnet");
        LocalLog.Add("[ERROR] DNS resolution failed");

        var snapshot = LocalLog.Snapshot();
        Assert.Equal(4, snapshot.Count);

        Assert.Equal("ROUTING", snapshot[0].Tag);
        Assert.Equal("Custom bypass added: soft98.ir -> Direct", snapshot[0].Message);

        Assert.Equal("TUN", snapshot[1].Tag);
        Assert.Equal("Session established", snapshot[1].Message);

        Assert.Equal("DISCOVERY", snapshot[2].Tag);
        Assert.Equal("Probing subnet", snapshot[2].Message);

        Assert.Equal("ERROR", snapshot[3].Tag);
        Assert.Equal("DNS resolution failed", snapshot[3].Message);
    }

    [Fact]
    public void ClearEmptiesBufferAndTriggersEvent()
    {
        LocalLog.Clear();
        LocalLog.Add("Entry 1");
        LocalLog.Add("Entry 2");

        Assert.Equal(2, LocalLog.Snapshot().Count);

        bool changedFired = false;
        LocalLog.Changed += () => changedFired = true;

        LocalLog.Clear();

        Assert.Empty(LocalLog.Snapshot());
        Assert.True(changedFired);
        Assert.Empty(LocalLog.GetFormattedLogText());
    }

    [Fact]
    public void ConcurrentLogAdditionsAreThreadSafe()
    {
        LocalLog.Clear();

        Parallel.For(0, 200, i =>
        {
            LocalLog.Info($"Concurrent message {i}");
        });

        Assert.Equal(200, LocalLog.Snapshot().Count);
    }

    [Fact]
    public void EmptyAndWhitespaceMessagesAreIgnored()
    {
        LocalLog.Clear();

        LocalLog.Add("");
        LocalLog.Add("   ");
        LocalLog.Add(null!);

        Assert.Empty(LocalLog.Snapshot());
    }

    [Fact]
    public void CapacityIsCappedAt500EntriesFifo()
    {
        LocalLog.Clear();

        for (int i = 1; i <= 600; i++)
        {
            LocalLog.Info($"Message #{i}");
        }

        var snapshot = LocalLog.Snapshot();
        Assert.Equal(500, snapshot.Count);

        // First entry should be #101, last should be #600
        Assert.Equal("Message #101", snapshot[0].Message);
        Assert.Equal("Message #600", snapshot[^1].Message);
    }

    [Fact]
    public void NumericBracketsArePreservedAsMessageText()
    {
        LocalLog.Clear();
        LocalLog.Add("[12.4s] Connection established", "INFO");

        var snapshot = LocalLog.Snapshot();
        Assert.Single(snapshot);
        Assert.Equal("INFO", snapshot[0].Tag);
        Assert.Equal("[12.4s] Connection established", snapshot[0].Message);
    }

    [Fact]
    public void GetFormattedLogTextProducesCorrectMultiLineString()
    {
        LocalLog.Clear();
        LocalLog.Info("Line 1");
        LocalLog.Routing("Line 2");

        var text = LocalLog.GetFormattedLogText();
        var lines = text.Split(Environment.NewLine);

        Assert.Equal(2, lines.Length);
        Assert.Contains("[INFO] Line 1", lines[0]);
        Assert.Contains("[ROUTING] Line 2", lines[1]);
    }
}
