using AirTun.Core.Routing;
using Xunit;

namespace AirTun.App.Tests;

public class RoutingManagerTests
{
    [Fact]
    public void BuiltInIranRulesBypassDomesticDomainsWhenEnabled()
    {
        var manager = new RoutingManager { BypassDomestic = true };

        Assert.Equal(RuleAction.Direct, manager.ResolveAction("varzesh3.com"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("digikala.com"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("shaparak.ir"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("sub.bankmellat.ir"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("telewebion.com"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("any-site.ir"));

        // Foreign sites route to Proxy
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("google.com"));
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("youtube.com"));
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("github.com"));
    }

    [Fact]
    public void DisablingBypassDomesticRoutesEverythingToProxyUnlessCustomRule()
    {
        var manager = new RoutingManager { BypassDomestic = false };

        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("shaparak.ir"));
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("digikala.com"));
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("google.com"));

        // Add custom rule
        manager.AddCustomRule(new RoutingRule(RuleType.DomainFull, "shaparak.ir", RuleAction.Direct));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("shaparak.ir"));
    }

    [Fact]
    public void CustomRuleOverridesBuiltInRules()
    {
        var manager = new RoutingManager { BypassDomestic = true };

        // Force google.com to Direct
        manager.AddCustomRule(new RoutingRule(RuleType.DomainSuffix, "google.com", RuleAction.Direct));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("mail.google.com"));

        // Force digikala.com to Block
        manager.AddCustomRule(new RoutingRule(RuleType.DomainFull, "digikala.com", RuleAction.Block));
        Assert.Equal(RuleAction.Block, manager.ResolveAction("digikala.com"));
    }

    [Fact]
    public void BuildWinInetBypassListIncludesLocalAndIranDomains()
    {
        var manager = new RoutingManager { BypassDomestic = true };
        var bypassStr = manager.BuildWinInetBypassList();

        Assert.Contains("<local>", bypassStr);
        Assert.Contains("*.ir", bypassStr);
        Assert.Contains("*.shaparak.ir", bypassStr);
        Assert.Contains("*.digikala.com", bypassStr);
        Assert.Contains("192.168.*", bypassStr);
    }
}
