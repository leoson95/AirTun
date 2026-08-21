using System.Net;
using System.Net.Sockets;

namespace AirTun.Core.Routing;

public sealed record RouteEntry
{
    public string Destination { get; init; }
    public string Mask { get; init; }
    public string Gateway { get; init; }
    public int InterfaceIndex { get; init; }
    public int Metric { get; init; }
    public string? Tag { get; init; }

    public uint DestUint { get; }
    public uint MaskUint { get; }

    public RouteEntry(
        string destination,
        string mask,
        string gateway,
        int interfaceIndex = 0,
        int metric = 10,
        string? tag = null)
    {
        Destination = destination;
        Mask = mask;
        Gateway = gateway;
        InterfaceIndex = interfaceIndex;
        Metric = metric;
        Tag = tag;

        if (IPAddress.TryParse(destination, out var destIp) && destIp.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = destIp.GetAddressBytes();
            DestUint = BitConverter.ToUInt32(b, 0);
        }

        if (IPAddress.TryParse(mask, out var maskIp) && maskIp.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = maskIp.GetAddressBytes();
            MaskUint = BitConverter.ToUInt32(b, 0);
        }
    }

    public static RouteEntry FromCidr(string cidr, string gateway, int interfaceIndex = 0, int metric = 10, string? tag = null)
    {
        var (dest, mask) = ParseCidr(cidr);
        return new RouteEntry(dest, mask, gateway, interfaceIndex, metric, tag ?? cidr);
    }

    public static RouteEntry ForHost(string ip, string gateway, int interfaceIndex = 0, int metric = 10, string? tag = null)
    {
        var cleanIp = ip.Trim();
        return new RouteEntry(cleanIp, "255.255.255.255", gateway, interfaceIndex, metric, tag ?? cleanIp);
    }

    public static (string destination, string mask) ParseCidr(string cidr)
    {
        if (string.IsNullOrWhiteSpace(cidr))
            throw new ArgumentException("CIDR cannot be empty", nameof(cidr));

        var parts = cidr.Trim().Split('/');
        var ipStr = parts[0].Trim();
        int prefix = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 32;
        var maskStr = PrefixToMask(prefix);

        if (IPAddress.TryParse(ipStr, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var mask = IPAddress.Parse(maskStr);
            var ipBytes = ip.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();
            var netBytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                netBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
            }
            return (new IPAddress(netBytes).ToString(), maskStr);
        }

        return (ipStr, maskStr);
    }

    public static string PrefixToMask(int prefixLength)
    {
        if (prefixLength < 0 || prefixLength > 32)
            throw new ArgumentOutOfRangeException(nameof(prefixLength), "Prefix length must be between 0 and 32");

        if (prefixLength == 0) return "0.0.0.0";

        uint maskInt = uint.MaxValue << (32 - prefixLength);
        byte[] maskBytes =
        [
            (byte)((maskInt >> 24) & 0xFF),
            (byte)((maskInt >> 16) & 0xFF),
            (byte)((maskInt >> 8) & 0xFF),
            (byte)(maskInt & 0xFF)
        ];
        return new IPAddress(maskBytes).ToString();
    }

    public bool ContainsIp(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress)) return false;
        if (!IPAddress.TryParse(ipAddress, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            return false;
        return ContainsIp(ip);
    }

    public bool ContainsIp(IPAddress? ip)
    {
        if (ip == null || ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        uint ipUint = BitConverter.ToUInt32(b, 0);
        return ContainsIp(ipUint);
    }

    public bool ContainsIp(uint ipUint)
    {
        return (ipUint & MaskUint) == (DestUint & MaskUint);
    }
}
