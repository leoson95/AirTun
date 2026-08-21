using System.Net;

namespace AirTun.Core.Routing;

public sealed record GatewayInfo(IPAddress GatewayAddress, int InterfaceIndex, string InterfaceName);

public interface IGatewayFinder
{
    GatewayInfo? FindDefaultGateway();
}
