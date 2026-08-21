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
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("soft98.ir"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("sub.bankmellat.ir"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("telewebion.com"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("any-site.ir"));

        // Foreign sites route to Proxy
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("google.com"));
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("youtube.com"));
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("github.com"));
    }

    [Fact]
    public void DomesticBypassMatchesIranGeoIpAddresses()
    {
        var manager = new RoutingManager { BypassDomestic = true };

        // Known Iranian IP blocks
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("5.160.10.5"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("37.152.1.1"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("185.10.72.1"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("2.144.10.20"));

        // Foreign IPs route to Proxy
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("8.8.8.8"));
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("1.1.1.1"));
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("142.250.190.46"));
    }

    [Fact]
    public void LanBypassMatchesRfc1918AndLocalhost()
    {
        var manager = new RoutingManager { BypassLan = true, BypassDomestic = false };

        Assert.Equal(RuleAction.Direct, manager.ResolveAction("192.168.1.1"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("10.0.0.5"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("172.20.10.5"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("127.0.0.1"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("169.254.1.2"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("<local>"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("localhost"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("mycomputer.local"));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("nas.lan"));

        // When LAN bypass is disabled
        manager.BypassLan = false;
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("192.168.1.1"));
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("10.0.0.5"));
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("mycomputer.local"));
    }

    [Fact]
    public void DisablingBypassDomesticRoutesEverythingToProxyUnlessCustomRule()
    {
        var manager = new RoutingManager { BypassDomestic = false, BypassLan = false };

        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("shaparak.ir"));
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("digikala.com"));
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("google.com"));
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("185.10.72.1"));

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

        // Custom IP CIDR rule
        manager.AddCustomRule(new RoutingRule(RuleType.IpCidr, "1.1.1.0/24", RuleAction.Direct));
        Assert.Equal(RuleAction.Direct, manager.ResolveAction("1.1.1.1"));
        Assert.Equal(RuleAction.Proxy, manager.ResolveAction("1.1.2.1"));
    }

    [Fact]
    public void BuildWinInetBypassListIncludesLocalAndIranDomains()
    {
        var manager = new RoutingManager { BypassDomestic = true, BypassLan = true };
        var bypassStr = manager.BuildWinInetBypassList();

        Assert.Contains("<local>", bypassStr);
        Assert.Contains("*.ir", bypassStr);
        Assert.Contains("*.shaparak.ir", bypassStr);
        Assert.Contains("*.digikala.com", bypassStr);
        Assert.Contains("*.soft98.ir", bypassStr);
        Assert.Contains("192.168.*", bypassStr);
        Assert.Contains("10.*", bypassStr);
        Assert.Contains("172.16.*", bypassStr);
        Assert.Contains("169.254.*", bypassStr);
    }

    [Fact]
    public void LanBypassMatchesIpv6LoopbackAndLinkLocal()
    {
        Assert.True(RoutingManager.IsLanHostOrIp("::1"));
        Assert.True(RoutingManager.IsLanHostOrIp("fe80::1"));
        Assert.True(RoutingManager.IsLanHostOrIp("fe80::215:5dff:fe00:402"));

        // Public IPv6 should not be treated as LAN
        Assert.False(RoutingManager.IsLanHostOrIp("2606:4700:4700::1111"));
        Assert.False(RoutingManager.IsLanHostOrIp("2001:4860:4860::8888"));
    }

    [Fact]
    public void RoutingRuleMatchesHostAndCidrPatterns()
    {
        var cidrRule = new RoutingRule(RuleType.IpCidr, "10.0.0.0/8", RuleAction.Direct);
        Assert.True(cidrRule.Matches("10.5.20.1"));
        Assert.True(cidrRule.Matches("10.255.255.254"));
        Assert.False(cidrRule.Matches("11.0.0.1"));
        Assert.False(cidrRule.Matches("10.0.0.0.example.com"));

        var suffixRule = new RoutingRule(RuleType.DomainSuffix, "digikala.com", RuleAction.Direct);
        Assert.True(suffixRule.Matches("digikala.com"));
        Assert.True(suffixRule.Matches("api.digikala.com"));
        Assert.True(suffixRule.Matches("sub.api.digikala.com"));
        Assert.False(suffixRule.Matches("notdigikala.com"));

        var fullRule = new RoutingRule(RuleType.DomainFull, "exact.domain.com", RuleAction.Direct);
        Assert.True(fullRule.Matches("exact.domain.com"));
        Assert.False(fullRule.Matches("sub.exact.domain.com"));

        var keywordRule = new RoutingRule(RuleType.DomainKeyword, "bank", RuleAction.Direct);
        Assert.True(keywordRule.Matches("mybank.com"));
        Assert.True(keywordRule.Matches("banking.ir"));
        Assert.False(keywordRule.Matches("google.com"));
    }
}
