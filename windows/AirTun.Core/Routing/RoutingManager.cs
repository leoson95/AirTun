using System.Collections.Concurrent;
using System.Text.Json;

namespace AirTun.Core.Routing;

public sealed class RoutingManager
{
    public bool BypassDomestic { get; set; } = true;
    public bool BypassLan { get; set; } = true;

    private readonly List<RoutingRule> _builtInIranRules =
    [
        new(RuleType.DomainSuffix, "ir", RuleAction.Direct, "All .ir top-level domains"),
        new(RuleType.DomainSuffix, "xn--mgba3a4f16a", RuleAction.Direct, "All .ایران top-level domains"),
        new(RuleType.DomainSuffix, "shaparak.ir", RuleAction.Direct, "Shaparak Banking Gateways"),
        new(RuleType.DomainSuffix, "aparat.com", RuleAction.Direct, "Aparat Video"),
        new(RuleType.DomainSuffix, "telewebion.com", RuleAction.Direct, "Telewebion"),
        new(RuleType.DomainSuffix, "digikala.com", RuleAction.Direct, "Digikala"),
        new(RuleType.DomainSuffix, "snapp.ir", RuleAction.Direct, "Snapp"),
        new(RuleType.DomainSuffix, "divar.ir", RuleAction.Direct, "Divar"),
        new(RuleType.DomainSuffix, "torob.com", RuleAction.Direct, "Torob"),
        new(RuleType.DomainSuffix, "varzesh3.com", RuleAction.Direct, "Varzesh3"),
        new(RuleType.DomainSuffix, "cafebazaar.ir", RuleAction.Direct, "CafeBazaar"),
        new(RuleType.DomainSuffix, "tamin.ir", RuleAction.Direct, "Tamin Ejtemaei"),
        new(RuleType.DomainSuffix, "my.gov.ir", RuleAction.Direct, "National Government Services"),
        new(RuleType.DomainSuffix, "mci.ir", RuleAction.Direct, "Hamrah-e Aval"),
        new(RuleType.DomainSuffix, "irancell.ir", RuleAction.Direct, "MTN Irancell"),
        new(RuleType.DomainSuffix, "rightel.ir", RuleAction.Direct, "Rightel"),
        new(RuleType.DomainSuffix, "shatel.ir", RuleAction.Direct, "Shatel ISP"),
        new(RuleType.DomainSuffix, "mokhaberat.ir", RuleAction.Direct, "TCI Telecom"),
        new(RuleType.DomainSuffix, "bmi.ir", RuleAction.Direct, "Bank Melli"),
        new(RuleType.DomainSuffix, "bankmellat.ir", RuleAction.Direct, "Bank Mellat"),
        new(RuleType.DomainSuffix, "tejaratbank.ir", RuleAction.Direct, "Bank Tejarat"),
        new(RuleType.DomainSuffix, "banksepah.ir", RuleAction.Direct, "Bank Sepah"),
        new(RuleType.DomainSuffix, "bsi.ir", RuleAction.Direct, "Bank Saderat"),
        new(RuleType.DomainSuffix, "bpi.ir", RuleAction.Direct, "Bank Pasargad"),
        new(RuleType.DomainSuffix, "parsian-bank.ir", RuleAction.Direct, "Bank Parsian"),
        new(RuleType.DomainSuffix, "bki.ir", RuleAction.Direct, "Bank Keshavarzi"),
        new(RuleType.DomainSuffix, "refah-bank.ir", RuleAction.Direct, "Bank Refah"),
        new(RuleType.DomainSuffix, "shahr-bank.ir", RuleAction.Direct, "Bank Shahr"),
        new(RuleType.DomainSuffix, "enbank.ir", RuleAction.Direct, "Bank Eghtesad Novin"),
    ];

    public List<RoutingRule> CustomRules { get; } = [];

    public event Action? RulesChanged;

    public RuleAction ResolveAction(string hostOrIp)
    {
        if (string.IsNullOrWhiteSpace(hostOrIp)) return RuleAction.Proxy;

        foreach (var rule in CustomRules.Where(r => r.Enabled))
        {
            if (rule.Matches(hostOrIp))
            {
                return rule.Action;
            }
        }

        if (BypassDomestic)
        {
            foreach (var rule in _builtInIranRules)
            {
                if (rule.Matches(hostOrIp))
                {
                    return RuleAction.Direct;
                }
            }
        }

        return RuleAction.Proxy;
    }

    public bool ShouldBypass(string hostOrIp) =>
        ResolveAction(hostOrIp) == RuleAction.Direct;

    public void AddCustomRule(RoutingRule rule)
    {
        CustomRules.Add(rule);
        RulesChanged?.Invoke();
    }

    public void RemoveCustomRule(RoutingRule rule)
    {
        CustomRules.Remove(rule);
        RulesChanged?.Invoke();
    }

    public string BuildWinInetBypassList()
    {
        var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (BypassLan)
        {
            entries.Add("<local>");
            entries.Add("127.*");
            entries.Add("10.*");
            entries.Add("172.16.*"); entries.Add("172.17.*"); entries.Add("172.18.*"); entries.Add("172.19.*");
            entries.Add("172.20.*"); entries.Add("172.21.*"); entries.Add("172.22.*"); entries.Add("172.23.*");
            entries.Add("172.24.*"); entries.Add("172.25.*"); entries.Add("172.26.*"); entries.Add("172.27.*");
            entries.Add("172.28.*"); entries.Add("172.29.*"); entries.Add("172.30.*"); entries.Add("172.31.*");
            entries.Add("192.168.*");
        }

        if (BypassDomestic)
        {
            foreach (var rule in _builtInIranRules)
            {
                entries.Add(rule.Type switch
                {
                    RuleType.DomainSuffix => $"*.{rule.Pattern.TrimStart('.')}",
                    RuleType.DomainFull => rule.Pattern,
                    RuleType.DomainKeyword => $"*{rule.Pattern}*",
                    _ => rule.Pattern,
                });
            }
        }

        foreach (var rule in CustomRules.Where(r => r.Enabled && r.Action == RuleAction.Direct))
        {
            entries.Add(rule.Type switch
            {
                RuleType.DomainSuffix => $"*.{rule.Pattern.TrimStart('.')}",
                RuleType.DomainFull => rule.Pattern,
                RuleType.DomainKeyword => $"*{rule.Pattern}*",
                _ => rule.Pattern,
            });
        }

        return string.Join(";", entries);
    }
}
