using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace AirTun.Core.Routing;

public sealed class WindowsRouteExecutor : IRouteExecutor
{
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int CreateIpForwardEntry(ref MIB_IPFORWARDROW pRoute);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int DeleteIpForwardEntry(ref MIB_IPFORWARDROW pRoute);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SetIpForwardEntry(ref MIB_IPFORWARDROW pRoute);

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_IPFORWARDROW
    {
        public uint dwForwardDest;
        public uint dwForwardMask;
        public uint dwForwardPolicy;
        public uint dwForwardNextHop;
        public uint dwForwardIfIndex;
        public uint dwForwardType;      // 3 = MIB_IPROUTE_TYPE_INDIRECT, 4 = MIB_IPROUTE_TYPE_DIRECT
        public uint dwForwardProto;     // 3 = MIB_IPPROTO_NETMGMT
        public uint dwForwardAge;
        public uint dwForwardNextHopAS;
        public uint dwForwardMetric1;
        public uint dwForwardMetric2;
        public uint dwForwardMetric3;
        public uint dwForwardMetric4;
        public uint dwForwardMetric5;
    }

    private const int ERROR_SUCCESS = 0;
    private const int ERROR_OBJECT_ALREADY_EXISTS = 5010;
    private const int ERROR_NOT_FOUND = 1168;

    private static uint IpToUint(string ipAddress)
    {
        var bytes = IPAddress.Parse(ipAddress.Trim()).GetAddressBytes();
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static MIB_IPFORWARDROW CreateRow(RouteEntry route)
    {
        return new MIB_IPFORWARDROW
        {
            dwForwardDest = IpToUint(route.Destination),
            dwForwardMask = IpToUint(route.Mask),
            dwForwardPolicy = 0,
            dwForwardNextHop = IpToUint(route.Gateway),
            dwForwardIfIndex = (uint)route.InterfaceIndex,
            dwForwardType = 4, // MIB_IPROUTE_TYPE_DIRECT
            dwForwardProto = 3, // MIB_IPPROTO_NETMGMT
            dwForwardAge = 0,
            dwForwardNextHopAS = 0,
            dwForwardMetric1 = (uint)route.Metric,
            dwForwardMetric2 = uint.MaxValue,
            dwForwardMetric3 = uint.MaxValue,
            dwForwardMetric4 = uint.MaxValue,
            dwForwardMetric5 = uint.MaxValue
        };
    }

    public bool AddRoute(RouteEntry route)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return true;

        try
        {
            var row = CreateRow(route);
            int ret = CreateIpForwardEntry(ref row);
            if (ret == ERROR_SUCCESS || ret == ERROR_OBJECT_ALREADY_EXISTS)
                return true;

            ret = SetIpForwardEntry(ref row);
            if (ret == ERROR_SUCCESS) return true;

            var ifArg = route.InterfaceIndex > 0 ? $" IF {route.InterfaceIndex}" : "";
            return RunRouteCommand($"ADD {route.Destination} MASK {route.Mask} {route.Gateway} METRIC {route.Metric}{ifArg}");
        }
        catch
        {
            var ifArg = route.InterfaceIndex > 0 ? $" IF {route.InterfaceIndex}" : "";
            return RunRouteCommand($"ADD {route.Destination} MASK {route.Mask} {route.Gateway} METRIC {route.Metric}{ifArg}");
        }
    }

    public bool DeleteRoute(RouteEntry route)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return true;

        try
        {
            var row = CreateRow(route);
            int ret = DeleteIpForwardEntry(ref row);
            if (ret == ERROR_SUCCESS || ret == ERROR_NOT_FOUND)
                return true;

            return RunRouteCommand($"DELETE {route.Destination} MASK {route.Mask} {route.Gateway}");
        }
        catch
        {
            return RunRouteCommand($"DELETE {route.Destination} MASK {route.Mask} {route.Gateway}");
        }
    }

    public int AddRoutes(IEnumerable<RouteEntry> routes)
    {
        int successCount = 0;
        foreach (var route in routes)
        {
            if (AddRoute(route)) successCount++;
        }
        return successCount;
    }

    public int DeleteRoutes(IEnumerable<RouteEntry> routes)
    {
        int successCount = 0;
        foreach (var route in routes)
        {
            if (DeleteRoute(route)) successCount++;
        }
        return successCount;
    }

    private static bool RunRouteCommand(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("route.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                p.WaitForExit(1000);
                return p.ExitCode == 0;
            }
        }
        catch { }
        return false;
    }
}
