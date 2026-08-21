using System.Collections.Concurrent;

namespace AirTun.App.Services;

public static class LocalLog
{
    private const int Capacity = 500;

    public readonly record struct Entry(DateTime Timestamp, string Tag, string Message)
    {
        public override string ToString() => $"[{Timestamp:HH:mm:ss}] [{Tag}] {Message}";
    }

    private static readonly ConcurrentQueue<Entry> Buffer = new();

    public static event Action? Changed;

    public static void Add(string message, string tag = "INFO")
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        var (cleanTag, cleanMsg) = ParseTagAndMessage(message, tag);
        Buffer.Enqueue(new Entry(DateTime.Now, cleanTag, cleanMsg));
        while (Buffer.Count > Capacity) Buffer.TryDequeue(out _);
        Changed?.Invoke();
    }

    public static void Info(string message) => Add(message, "INFO");
    public static void Routing(string message) => Add(message, "ROUTING");
    public static void Tun(string message) => Add(message, "TUN");
    public static void Discovery(string message) => Add(message, "DISCOVERY");
    public static void Error(string message) => Add(message, "ERROR");

    public static IReadOnlyList<Entry> Snapshot() => Buffer.ToArray();

    public static string GetFormattedLogText()
    {
        var entries = Snapshot();
        return string.Join(Environment.NewLine, entries.Select(e => e.ToString()));
    }

    public static void Clear()
    {
        while (Buffer.TryDequeue(out _)) { }
        Changed?.Invoke();
    }

    private static (string Tag, string Message) ParseTagAndMessage(string rawMessage, string defaultTag)
    {
        var trimmed = rawMessage.Trim();
        if (trimmed.StartsWith('[') && trimmed.IndexOf(']') is > 1 and int closeIdx)
        {
            var potentialTag = trimmed[1..closeIdx].Trim();
            if (potentialTag.Length > 0 && potentialTag.All(char.IsLetter))
            {
                return (potentialTag.ToUpperInvariant(), trimmed[(closeIdx + 1)..].TrimStart());
            }
        }

        return (defaultTag.ToUpperInvariant(), trimmed);
    }
}
