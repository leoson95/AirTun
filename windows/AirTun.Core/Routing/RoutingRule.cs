using System.Net;

namespace AirTun.Core.Routing;

public enum RuleType
{
    DomainSuffix,
    DomainFull,
    DomainKeyword,
    IpCidr,
}

public enum RuleAction
{
    Direct,
    Proxy,
    Block,
}

public sealed class RoutingRule
{
    public RuleType Type { get; set; } = RuleType.DomainSuffix;
    public string Pattern { get; set; } = string.Empty;
    public RuleAction Action { get; set; } = RuleAction.Direct;
    public bool Enabled { get; set; } = true;
    public string? Description { get; set; }

    public RoutingRule() { }

    public RoutingRule(RuleType type, string pattern, RuleAction action, string? description = null, bool enabled = true)
    {
        Type = type;
        Pattern = pattern.Trim().ToLowerInvariant();
        Action = action;
        Description = description;
        Enabled = enabled;
    }

    public bool Matches(string hostOrIp)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(hostOrIp) || string.IsNullOrWhiteSpace(Pattern))
            return false;

        var target = hostOrIp.Trim().ToLowerInvariant();

        return Type switch
        {
            RuleType.DomainSuffix => target.EndsWith(Pattern.StartsWith('.') ? Pattern : $".{Pattern}", StringComparison.OrdinalIgnoreCase)
                                     || target.Equals(Pattern.TrimStart('.'), StringComparison.OrdinalIgnoreCase),
            RuleType.DomainFull => target.Equals(Pattern, StringComparison.OrdinalIgnoreCase),
            RuleType.DomainKeyword => target.Contains(Pattern, StringComparison.OrdinalIgnoreCase),
            RuleType.IpCidr => TryMatchCidr(Pattern, target),
            _ => false,
        };
    }

    private static bool TryMatchCidr(string pattern, string target)
    {
        if (IPAddress.TryParse(target, out var targetIp) && targetIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            try
            {
                var route = pattern.Contains('/')
                    ? RouteEntry.FromCidr(pattern, "0.0.0.0")
                    : RouteEntry.ForHost(pattern, "0.0.0.0");
                return route.ContainsIp(targetIp);
            }
            catch
            {
                return false;
            }
        }
        return false;
    }
}
