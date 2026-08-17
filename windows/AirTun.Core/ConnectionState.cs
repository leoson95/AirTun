namespace AirTun.Core;

public enum ErrorCode
{
    HotspotOff,
    HotspotLost,
    PortInUse,
    ServiceFailed,
    InvalidPin,
    ConnectionRefused,
    TunnelElevationDeclined,
    TunnelFailed,
}

public enum WarningCode
{
    NoVpnActive,
    VpnCapturesLocal,
}

public abstract record ConnectionState(string StateName)
{
    public sealed record IdleState() : ConnectionState("Idle");
    public sealed record PreparingState() : ConnectionState("Preparing");
    public sealed record DiscoveringState() : ConnectionState("Discovering");
    public sealed record ConnectedState(
        string Host,
        int Port,
        string PinCode,
        string DeviceName,
        string Mode,
        long BytesUp = 0,
        long BytesDown = 0,
        int LatencyMs = -1,
        bool Reconnecting = false
    ) : ConnectionState("Connected");

    public sealed record ErrorState(ErrorCode Code, string? Message = null) : ConnectionState("Error");

    public static readonly ConnectionState Idle = new IdleState();
    public static readonly ConnectionState Preparing = new PreparingState();
    public static readonly ConnectionState Discovering = new DiscoveringState();
}
