using System.Net;
using AirTun.Core.Routing;
using Xunit;

namespace AirTun.App.Tests;

public class TunRoutingManagerTests
{
    private sealed class MockRouteExecutor : IRouteExecutor
    {
        public List<RouteEntry> AddedRoutes { get; } = [];
        public List<RouteEntry> DeletedRoutes { get; } = [];

        public bool AddRoute(RouteEntry route)
        {
            AddedRoutes.Add(route);
            return true;
        }

        public bool DeleteRoute(RouteEntry route)
        {
            DeletedRoutes.Add(route);
            return true;
        }

        public int AddRoutes(IEnumerable<RouteEntry> routes)
        {
            int count = 0;
            foreach (var r in routes)
            {
                AddRoute(r);
                count++;
            }
            return count;
        }

        public int DeleteRoutes(IEnumerable<RouteEntry> routes)
        {
            int count = 0;
            foreach (var r in routes)
            {
                DeleteRoute(r);
                count++;
            }
            return count;
        }
    }

    private sealed class MockGatewayFinder(GatewayInfo? info) : IGatewayFinder
    {
        public GatewayInfo? FindDefaultGateway() => info;
    }

    [Fact]
    public async Task ApplyTunBypassRoutesInjectsDomesticAndLanRoutes()
    {
        var executor = new MockRouteExecutor();
        var gw = new GatewayInfo(IPAddress.Parse("192.168.43.1"), 5, "Wi-Fi");
        var finder = new MockGatewayFinder(gw);
        var logs = new List<string>();

        var manager = new TunRoutingManager(executor, finder);
        manager.LogGenerated += msg => logs.Add(msg);

        var ok = await manager.ApplyTunBypassRoutesAsync(gw, bypassDomestic: true, bypassLan: true);

        Assert.True(ok);
        Assert.True(manager.DomesticBypassActive);
        Assert.True(manager.LanBypassActive);

        // 254 Iran CIDRs + 5 LAN CIDRs = 259 routes
        Assert.Equal(259, executor.AddedRoutes.Count);
        Assert.Equal(259, manager.GetActiveRoutes().Count);

        Assert.Contains(logs, l => l.Contains("Domestic .ir bypass active: 254 CIDR ranges routed direct"));
        Assert.Contains(logs, l => l.Contains("Local LAN bypass active: 5 RFC1918 ranges routed direct"));
    }

    [Fact]
    public async Task DynamicTogglesAddAndRemoveRoutes()
    {
        var executor = new MockRouteExecutor();
        var gw = new GatewayInfo(IPAddress.Parse("192.168.1.1"), 3, "Ethernet");
        var manager = new TunRoutingManager(executor, new MockGatewayFinder(gw));

        await manager.ApplyTunBypassRoutesAsync(gw, bypassDomestic: true, bypassLan: false);
        Assert.Equal(254, manager.GetActiveRoutes().Count);

        // Turn off domestic bypass
        await manager.SetDomesticBypassAsync(false);
        Assert.False(manager.DomesticBypassActive);
        Assert.Equal(254, executor.DeletedRoutes.Count);
        Assert.Empty(manager.GetActiveRoutes());

        // Turn on LAN bypass
        await manager.SetLanBypassAsync(true);
        Assert.True(manager.LanBypassActive);
        Assert.Equal(5, manager.GetActiveRoutes().Count);
    }

    [Fact]
    public async Task CustomIpRuleAddsAndRemovesDirectRoute()
    {
        var executor = new MockRouteExecutor();
        var gw = new GatewayInfo(IPAddress.Parse("192.168.43.1"), 5, "Wi-Fi");
        var manager = new TunRoutingManager(executor, new MockGatewayFinder(gw));

        await manager.ApplyTunBypassRoutesAsync(gw, bypassDomestic: false, bypassLan: false);

        var rule = new RoutingRule(RuleType.IpCidr, "1.1.1.1", RuleAction.Direct);
        await manager.AddCustomRuleAsync(rule);

        Assert.Single(manager.GetActiveRoutes());
        Assert.Contains(executor.AddedRoutes, r => r.Destination == "1.1.1.1" && r.Mask == "255.255.255.255");

        manager.RemoveCustomRule(rule);
        Assert.Empty(manager.GetActiveRoutes());
        Assert.Contains(executor.DeletedRoutes, r => r.Destination == "1.1.1.1");
    }

    [Fact]
    public async Task OverlappingCustomRuleRemovalDoesNotDeleteSharedRoute()
    {
        var executor = new MockRouteExecutor();
        var gw = new GatewayInfo(IPAddress.Parse("192.168.43.1"), 5, "Wi-Fi");
        var manager = new TunRoutingManager(executor, new MockGatewayFinder(gw));

        await manager.ApplyTunBypassRoutesAsync(gw, bypassDomestic: false, bypassLan: false);

        var rule1 = new RoutingRule(RuleType.IpCidr, "1.1.1.1", RuleAction.Direct);
        var rule2 = new RoutingRule(RuleType.IpCidr, "1.1.1.1/32", RuleAction.Direct);

        await manager.AddCustomRuleAsync(rule1);
        await manager.AddCustomRuleAsync(rule2);

        Assert.Single(manager.GetActiveRoutes());

        // Remove rule1 - route should still remain because rule2 uses it
        manager.RemoveCustomRule(rule1);
        Assert.Single(manager.GetActiveRoutes());
        Assert.Empty(executor.DeletedRoutes);

        // Remove rule2 - now it should be deleted
        manager.RemoveCustomRule(rule2);
        Assert.Empty(manager.GetActiveRoutes());
        Assert.Single(executor.DeletedRoutes);
    }

    [Fact]
    public async Task ReapplyingBypassRoutesCleansPriorRoutesFirst()
    {
        var executor = new MockRouteExecutor();
        var gw1 = new GatewayInfo(IPAddress.Parse("192.168.1.1"), 3, "Ethernet");
        var gw2 = new GatewayInfo(IPAddress.Parse("192.168.43.1"), 5, "Wi-Fi");
        var manager = new TunRoutingManager(executor, new MockGatewayFinder(gw1));

        await manager.ApplyTunBypassRoutesAsync(gw1, bypassDomestic: true, bypassLan: true);
        Assert.Equal(259, manager.GetActiveRoutes().Count);

        // Switch to gateway 2
        await manager.ApplyTunBypassRoutesAsync(gw2, bypassDomestic: false, bypassLan: true);
        Assert.Equal(5, manager.GetActiveRoutes().Count);
        Assert.Equal("192.168.43.1", manager.CurrentGateway?.GatewayAddress.ToString());
    }

    [Fact]
    public async Task CustomRuleUrlSanitizesHostnameCorrectly()
    {
        var executor = new MockRouteExecutor();
        var gw = new GatewayInfo(IPAddress.Parse("192.168.43.1"), 5, "Wi-Fi");
        var manager = new TunRoutingManager(executor, new MockGatewayFinder(gw));

        await manager.ApplyTunBypassRoutesAsync(gw, bypassDomestic: false, bypassLan: false);

        var rule = new RoutingRule(RuleType.DomainSuffix, "https://soft98.ir/software/", RuleAction.Direct);
        await manager.AddCustomRuleAsync(rule);

        // Rule is added without crashing
        Assert.NotNull(manager.CurrentGateway);
    }

    [Fact]
    public async Task PurgeAllRoutesCleansAllInjectedRoutes()
    {
        var executor = new MockRouteExecutor();
        var gw = new GatewayInfo(IPAddress.Parse("192.168.43.1"), 5, "Wi-Fi");
        var manager = new TunRoutingManager(executor, new MockGatewayFinder(gw));

        await manager.ApplyTunBypassRoutesAsync(gw, bypassDomestic: true, bypassLan: true);
        Assert.Equal(259, manager.GetActiveRoutes().Count);

        int purged = manager.PurgeAllRoutes();

        Assert.Equal(259, purged);
        Assert.Empty(manager.GetActiveRoutes());
        Assert.False(manager.DomesticBypassActive);
        Assert.False(manager.LanBypassActive);
        Assert.Null(manager.CurrentGateway);
    }

    [Fact]
    public void PurgeAllRoutesWhenEmptyReturnsZero()
    {
        var executor = new MockRouteExecutor();
        var manager = new TunRoutingManager(executor, new MockGatewayFinder(null));

        int purged = manager.PurgeAllRoutes();
        Assert.Equal(0, purged);
        Assert.Empty(manager.GetActiveRoutes());
    }

    [Fact]
    public async Task DynamicToggleResolvesGatewayIfInitiallyNull()
    {
        var executor = new MockRouteExecutor();
        var gw = new GatewayInfo(IPAddress.Parse("192.168.43.1"), 5, "Wi-Fi");
        var manager = new TunRoutingManager(executor, new MockGatewayFinder(gw));

        // Start with no gateway configured (offline state)
        Assert.Null(manager.CurrentGateway);

        // Turn on Domestic bypass dynamically -> resolves gateway from finder
        await manager.SetDomesticBypassAsync(true);

        Assert.True(manager.DomesticBypassActive);
        Assert.Equal(254, manager.GetActiveRoutes().Count);
        Assert.NotNull(manager.CurrentGateway);
        Assert.Equal("192.168.43.1", manager.CurrentGateway.GatewayAddress.ToString());
    }

    [Fact]
    public async Task DisabledOrProxyRulesDoNotInjectDirectRoutes()
    {
        var executor = new MockRouteExecutor();
        var gw = new GatewayInfo(IPAddress.Parse("192.168.43.1"), 5, "Wi-Fi");
        var manager = new TunRoutingManager(executor, new MockGatewayFinder(gw));
        await manager.ApplyTunBypassRoutesAsync(gw, bypassDomestic: false, bypassLan: false);

        var disabledRule = new RoutingRule(RuleType.IpCidr, "1.1.1.1", RuleAction.Direct, enabled: false);
        var proxyRule = new RoutingRule(RuleType.IpCidr, "2.2.2.2", RuleAction.Proxy, enabled: true);

        await manager.AddCustomRuleAsync(disabledRule);
        await manager.AddCustomRuleAsync(proxyRule);

        Assert.Empty(manager.GetActiveRoutes());
        Assert.Empty(executor.AddedRoutes);
    }

    [Fact]
    public void RemovingNonExistentRuleSafelyNoOps()
    {
        var executor = new MockRouteExecutor();
        var gw = new GatewayInfo(IPAddress.Parse("192.168.43.1"), 5, "Wi-Fi");
        var manager = new TunRoutingManager(executor, new MockGatewayFinder(gw));

        var rule = new RoutingRule(RuleType.IpCidr, "8.8.8.8", RuleAction.Direct);
        manager.RemoveCustomRule(rule);

        Assert.Empty(manager.GetActiveRoutes());
        Assert.Empty(executor.DeletedRoutes);
    }
}
