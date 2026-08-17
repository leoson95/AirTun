namespace AirTun.Core.Proxy;

public sealed record ProxySnapshot(
    bool Enabled,
    string? Server,
    string? Override,
    string? AutoConfigUrl
);

public sealed record ProxyBackup(
    ProxySnapshot Original,
    string AppliedServer
);
