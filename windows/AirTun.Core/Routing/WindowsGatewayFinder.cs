using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace AirTun.Core.Routing;

public sealed class WindowsGatewayFinder : IGatewayFinder
{
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int GetBestRoute(uint dwDestAddr, uint dwSourceAddr, out MIB_IPFORWARDROW pBestRoute);

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_IPFORWARDROW
    {
        public uint dwForwardDest;
        public uint dwForwardMask;
        public uint dwForwardPolicy;
        public uint dwForwardNextHop;
        public uint dwForwardIfIndex;
        public uint dwForwardType;
        public uint dwForwardProto;
        public uint dwForwardAge;
        public uint dwForwardNextHopAS;
        public uint dwForwardMetric1;
        public uint dwForwardMetric2;
        public uint dwForwardMetric3;
        public uint dwForwardMetric4;
        public uint dwForwardMetric5;
    }

    private static readonly string[] VirtualKeywords =
    [
        "airtun", "wintun", "tun2socks", "wireguard", "openvpn",
        "tap-windows", "tap0", "virtualbox", "vmware", "vethernet",
        "hyper-v", "loopback", "nordlynx", "tailscale", "zerotier",
        "sing-box", "clash", "v2ray", "nekoray", "xray", "wsl"
    ];

    public static bool IsVirtualOrTunnelInterface(NetworkInterface nic)
    {
        if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel ||
            nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
            nic.NetworkInterfaceType == NetworkInterfaceType.Ppp)
            return true;

        var name = nic.Name;
        var desc = nic.Description;

        foreach (var kw in VirtualKeywords)
        {
            if (name.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                desc.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public GatewayInfo? FindDefaultGateway()
    {
        // 1. Primary: Query Win32 GetBestRoute to active internet destination (8.8.8.8)
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                uint dest = BitConverter.ToUInt32(IPAddress.Parse("8.8.8.8").GetAddressBytes(), 0);
                if (GetBestRoute(dest, 0, out var bestRoute) == 0 && bestRoute.dwForwardNextHop != 0)
                {
                    int ifIndex = (int)bestRoute.dwForwardIfIndex;
                    var matchingNic = NetworkInterface.GetAllNetworkInterfaces()
                        .FirstOrDefault(nic =>
                        {
                            var ipv4 = nic.GetIPProperties()?.GetIPv4Properties();
                            return ipv4 != null && ipv4.Index == ifIndex;
                        });

                    // Ensure GetBestRoute didn't return a virtual/tunnel adapter (e.g. WinTun when tunnel is active)
                    if (matchingNic != null &&
                        matchingNic.OperationalStatus == OperationalStatus.Up &&
                        !IsVirtualOrTunnelInterface(matchingNic))
                    {
                        var gwBytes = BitConverter.GetBytes(bestRoute.dwForwardNextHop);
                        var gwIp = new IPAddress(gwBytes);

                        if (!gwIp.Equals(IPAddress.Any) && !gwIp.Equals(IPAddress.None) && !IPAddress.IsLoopback(gwIp))
                        {
                            return new GatewayInfo(gwIp, ifIndex, matchingNic.Name);
                        }
                    }
                }
            }
        }
        catch { }

        // 2. Fallback: Search NetworkInterface list for active physical adapters with default gateways
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up && !IsVirtualOrTunnelInterface(nic))
                .OrderByDescending(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                                          nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .ToList();

            foreach (var nic in interfaces)
            {
                var ipProps = nic.GetIPProperties();
                var ipv4 = ipProps.GetIPv4Properties();
                if (ipv4 == null) continue;

                var gw = ipProps.GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork &&
                                         !g.Address.Equals(IPAddress.Any) &&
                                         !g.Address.Equals(IPAddress.None) &&
                                         !IPAddress.IsLoopback(g.Address));

                if (gw != null)
                {
                    return new GatewayInfo(gw.Address, ipv4.Index, nic.Name);
                }
            }
        }
        catch { }

        return null;
    }
}
