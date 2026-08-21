using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace AirTun.Core.Routing;

public sealed class TunRoutingManager
{
    private readonly IRouteExecutor _executor;
    private readonly IGatewayFinder _gatewayFinder;

    public static readonly IReadOnlyList<string> LanCidrs =
    [
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
        "127.0.0.0/8",
        "169.254.0.0/16"
    ];

    private readonly ConcurrentDictionary<string, RouteEntry> _activeInjectedRoutes = new();
    private readonly ConcurrentDictionary<string, List<RouteEntry>> _customRuleRoutes = new();
    private readonly List<RouteEntry> _domesticRoutes = [];
    private readonly List<RouteEntry> _lanRoutes = [];

    public GatewayInfo? CurrentGateway { get; private set; }
    public bool IsActive => CurrentGateway != null;
    public bool DomesticBypassActive { get; private set; }
    public bool LanBypassActive { get; private set; }

    public event Action<string>? LogGenerated;

    public TunRoutingManager(IRouteExecutor? executor = null, IGatewayFinder? gatewayFinder = null)
    {
        _executor = executor ?? new WindowsRouteExecutor();
        _gatewayFinder = gatewayFinder ?? new WindowsGatewayFinder();
    }

    public void Log(string message) => LogGenerated?.Invoke(message);

    public async Task<bool> ApplyTunBypassRoutesAsync(
        GatewayInfo? gateway,
        bool bypassDomestic,
        bool bypassLan,
        IEnumerable<RoutingRule>? customRules = null)
    {
        if (_activeInjectedRoutes.Count > 0)
        {
            PurgeAllRoutes();
        }

        gateway ??= _gatewayFinder.FindDefaultGateway();
        if (gateway == null)
        {
            Log("[ROUTING] Warning: No physical network default gateway found. Skipping direct bypass routes.");
            return false;
        }

        CurrentGateway = gateway;
        var gwStr = gateway.GatewayAddress.ToString();
        var ifIndex = gateway.InterfaceIndex;

        Log($"[ROUTING] Detected physical gateway: {gwStr} on {gateway.InterfaceName} (Index: {ifIndex})");

        if (bypassLan)
        {
            await ApplyLanBypassInternalAsync(gwStr, ifIndex).ConfigureAwait(false);
        }

        if (bypassDomestic)
        {
            await ApplyDomesticBypassInternalAsync(gwStr, ifIndex).ConfigureAwait(false);
        }

        if (customRules != null)
        {
            foreach (var rule in customRules.Where(r => r.Enabled && r.Action == RuleAction.Direct))
            {
                await AddCustomRuleInternalAsync(rule, gwStr, ifIndex).ConfigureAwait(false);
            }
        }

        return true;
    }

    public async Task SetDomesticBypassAsync(bool enabled)
    {
        if (DomesticBypassActive == enabled) return;

        if (CurrentGateway == null)
        {
            CurrentGateway = _gatewayFinder.FindDefaultGateway();
            if (CurrentGateway == null)
            {
                DomesticBypassActive = enabled;
                return;
            }
        }

        var gwStr = CurrentGateway.GatewayAddress.ToString();
        var ifIndex = CurrentGateway.InterfaceIndex;

        if (enabled)
        {
            await ApplyDomesticBypassInternalAsync(gwStr, ifIndex).ConfigureAwait(false);
        }
        else
        {
            RemoveDomesticBypassInternal();
        }
    }

    public async Task SetLanBypassAsync(bool enabled)
    {
        if (LanBypassActive == enabled) return;

        if (CurrentGateway == null)
        {
            CurrentGateway = _gatewayFinder.FindDefaultGateway();
            if (CurrentGateway == null)
            {
                LanBypassActive = enabled;
                return;
            }
        }

        var gwStr = CurrentGateway.GatewayAddress.ToString();
        var ifIndex = CurrentGateway.InterfaceIndex;

        if (enabled)
        {
            await ApplyLanBypassInternalAsync(gwStr, ifIndex).ConfigureAwait(false);
        }
        else
        {
            RemoveLanBypassInternal();
        }
    }

    public async Task AddCustomRuleAsync(RoutingRule rule)
    {
        if (!rule.Enabled || rule.Action != RuleAction.Direct) return;
        if (CurrentGateway == null)
        {
            CurrentGateway = _gatewayFinder.FindDefaultGateway();
            if (CurrentGateway == null) return;
        }

        var gwStr = CurrentGateway.GatewayAddress.ToString();
        var ifIndex = CurrentGateway.InterfaceIndex;

        await AddCustomRuleInternalAsync(rule, gwStr, ifIndex).ConfigureAwait(false);
    }

    public void RemoveCustomRule(RoutingRule rule)
    {
        var key = rule.Pattern.Trim().ToLowerInvariant();
        if (_customRuleRoutes.TryRemove(key, out var routes))
        {
            foreach (var r in routes)
            {
                var routeKey = $"{r.Destination}/{r.Mask}";
                bool stillNeeded = _customRuleRoutes.Values.Any(list => list.Any(entry => $"{entry.Destination}/{entry.Mask}" == routeKey));

                if (!stillNeeded && DomesticBypassActive)
                {
                    lock (_domesticRoutes)
                    {
                        if (_domesticRoutes.Any(entry => $"{entry.Destination}/{entry.Mask}" == routeKey))
                            stillNeeded = true;
                    }
                }

                if (!stillNeeded && LanBypassActive)
                {
                    lock (_lanRoutes)
                    {
                        if (_lanRoutes.Any(entry => $"{entry.Destination}/{entry.Mask}" == routeKey))
                            stillNeeded = true;
                    }
                }

                if (!stillNeeded)
                {
                    _executor.DeleteRoute(r);
                    _activeInjectedRoutes.TryRemove(routeKey, out _);
                }
            }
            Log($"[ROUTING] Custom bypass removed: {rule.Pattern}");
        }
    }

    public int PurgeAllRoutes()
    {
        var allRoutes = _activeInjectedRoutes.Values.ToList();
        int count = _executor.DeleteRoutes(allRoutes);

        _activeInjectedRoutes.Clear();
        _customRuleRoutes.Clear();
        lock (_domesticRoutes) _domesticRoutes.Clear();
        lock (_lanRoutes) _lanRoutes.Clear();

        DomesticBypassActive = false;
        LanBypassActive = false;
        CurrentGateway = null;

        if (allRoutes.Count > 0)
        {
            Log($"[ROUTING] Purged {allRoutes.Count} injected bypass routes");
        }

        return count;
    }

    private Task ApplyDomesticBypassInternalAsync(string gwStr, int ifIndex)
    {
        lock (_domesticRoutes)
        {
            _domesticRoutes.Clear();
            var irRoutes = IranGeoIp.CreateIranRoutes(gwStr, ifIndex, metric: 10);
            foreach (var r in irRoutes)
            {
                _domesticRoutes.Add(r);
                _activeInjectedRoutes[$"{r.Destination}/{r.Mask}"] = r;
            }
            _executor.AddRoutes(irRoutes);
        }

        DomesticBypassActive = true;
        Log($"[ROUTING] Domestic .ir bypass active: {IranGeoIp.IranCidrs.Count} CIDR ranges routed direct");
        return Task.CompletedTask;
    }

    private void RemoveDomesticBypassInternal()
    {
        lock (_domesticRoutes)
        {
            foreach (var r in _domesticRoutes)
            {
                _executor.DeleteRoute(r);
                _activeInjectedRoutes.TryRemove($"{r.Destination}/{r.Mask}", out _);
            }
            _domesticRoutes.Clear();
        }

        DomesticBypassActive = false;
        Log("[ROUTING] Domestic .ir bypass deactivated");
    }

    private Task ApplyLanBypassInternalAsync(string gwStr, int ifIndex)
    {
        lock (_lanRoutes)
        {
            _lanRoutes.Clear();
            foreach (var cidr in LanCidrs)
            {
                var r = RouteEntry.FromCidr(cidr, gwStr, ifIndex, metric: 10, $"LAN:{cidr}");
                _lanRoutes.Add(r);
                _activeInjectedRoutes[$"{r.Destination}/{r.Mask}"] = r;
            }
            _executor.AddRoutes(_lanRoutes);
        }

        LanBypassActive = true;
        Log($"[ROUTING] Local LAN bypass active: {LanCidrs.Count} RFC1918 ranges routed direct");
        return Task.CompletedTask;
    }

    private void RemoveLanBypassInternal()
    {
        lock (_lanRoutes)
        {
            foreach (var r in _lanRoutes)
            {
                _executor.DeleteRoute(r);
                _activeInjectedRoutes.TryRemove($"{r.Destination}/{r.Mask}", out _);
            }
            _lanRoutes.Clear();
        }

        LanBypassActive = false;
        Log("[ROUTING] Local LAN bypass deactivated");
    }

    private async Task AddCustomRuleInternalAsync(RoutingRule rule, string gwStr, int ifIndex)
    {
        var pattern = rule.Pattern.Trim().ToLowerInvariant();
        var key = pattern;
        var routes = new List<RouteEntry>();

        if (rule.Type == RuleType.IpCidr || (IPAddress.TryParse(pattern, out _) && !pattern.Contains('/')))
        {
            var route = pattern.Contains('/')
                ? RouteEntry.FromCidr(pattern, gwStr, ifIndex, metric: 10, $"Custom:{pattern}")
                : RouteEntry.ForHost(pattern, gwStr, ifIndex, metric: 10, $"Custom:{pattern}");

            routes.Add(route);
            _executor.AddRoute(route);
            _activeInjectedRoutes[$"{route.Destination}/{route.Mask}"] = route;
            Log($"[ROUTING] Custom bypass added: {rule.Pattern} -> Direct");
        }
        else
        {
            // Domain rule - sanitize host and resolve hostnames
            var host = pattern.TrimStart('*', '.');
            if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(host, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
                {
                    host = uri.Host.TrimStart('*', '.');
                }
            }
            else if (host.Contains('/'))
            {
                host = host.Split('/')[0].Trim();
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var ips = await Dns.GetHostAddressesAsync(host, cts.Token).ConfigureAwait(false);
                var ipv4s = ips.Where(a => a.AddressFamily == AddressFamily.InterNetwork).Distinct().ToList();

                foreach (var ip in ipv4s)
                {
                    var r = RouteEntry.ForHost(ip.ToString(), gwStr, ifIndex, metric: 10, $"Custom:{host}:{ip}");
                    routes.Add(r);
                    _executor.AddRoute(r);
                    _activeInjectedRoutes[$"{r.Destination}/{r.Mask}"] = r;
                }

                if (ipv4s.Count > 0)
                {
                    Log($"[ROUTING] Custom bypass added: {rule.Pattern} -> Direct ({string.Join(", ", ipv4s)})");
                }
                else
                {
                    Log($"[ROUTING] Custom bypass added: {rule.Pattern} -> Direct");
                }
            }
            catch (Exception ex)
            {
                Log($"[ROUTING] Custom bypass registered: {rule.Pattern} (DNS lookup: {ex.Message})");
            }
        }

        _customRuleRoutes[key] = routes;
    }

    public IReadOnlyList<RouteEntry> GetActiveRoutes() => _activeInjectedRoutes.Values.ToList();
}
