namespace AirTun.Core.Routing;

public interface IRouteExecutor
{
    bool AddRoute(RouteEntry route);
    bool DeleteRoute(RouteEntry route);
    int AddRoutes(IEnumerable<RouteEntry> routes);
    int DeleteRoutes(IEnumerable<RouteEntry> routes);
}
