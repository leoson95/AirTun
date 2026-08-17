namespace AirTun.Core;

public static class ConnectionRules
{
    public static readonly HashSet<string> States = ["Idle", "Preparing", "Discovering", "Connected", "Error"];
    public const string Initial = "Idle";

    public static readonly IReadOnlyDictionary<(string From, string Event), string> Transitions =
        new Dictionary<(string From, string Event), string>
        {
            [("Idle", "start")] = "Preparing",
            [("Idle", "discover")] = "Discovering",
            [("Discovering", "start")] = "Preparing",
            [("Discovering", "stop")] = "Idle",
            [("Preparing", "ready")] = "Connected",
            [("Preparing", "failure")] = "Error",
            [("Preparing", "stop")] = "Idle",
            [("Connected", "statsUpdated")] = "Connected",
            [("Connected", "stop")] = "Idle",
            [("Connected", "failure")] = "Error",
            [("Error", "dismiss")] = "Idle",
            [("Error", "retry")] = "Preparing",
        };

    public static bool CanTransition(string from, string evt) =>
        Transitions.ContainsKey((from, evt));

    public static string? Target(string from, string evt) =>
        Transitions.TryGetValue((from, evt), out var target) ? target : null;
}
