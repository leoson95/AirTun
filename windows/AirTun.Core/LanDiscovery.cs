using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace AirTun.Core;

public sealed class LanDiscovery : IDisposable
{
    public const int Port = AirTunConfig.DefaultBeaconPort;
    public const int Version = AirTunConfig.ProtocolVersion;

    public static readonly TimeSpan StaleTimeout = AirTunConfig.BeaconStaleTimeout;
    public static readonly TimeSpan ProbeInterval = AirTunConfig.ProbeInterval;

    /// <summary>Fired with diagnostic messages for the Logs tab.</summary>
    public event Action<string>? DiagnosticLog;

    public sealed record Device(
        string Host,
        int PortNumber,
        string DeviceName,
        string? Pin,
        bool PinRequired,
        DateTimeOffset Seen
    )
    {
        public string Key => $"{Host}:{PortNumber}";
    }

    private readonly Dictionary<string, Device> _devices = new();
    private readonly object _lock = new();
    private readonly Func<DateTimeOffset> _clock;
    private UdpClient? _socket;
    private CancellationTokenSource? _cancellation;
    private CancellationTokenSource? _probing;

    public LanDiscovery(Func<DateTimeOffset>? clock = null) => _clock = clock ?? (() => DateTimeOffset.UtcNow);

    public event Action<IReadOnlyList<Device>>? DevicesChanged;

    public void Start()
    {
        if (_socket is not null) return;

        var socket = new UdpClient();
        socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
        socket.EnableBroadcast = true;
        _socket = socket;
        _cancellation = new CancellationTokenSource();
        _ = ReceiveLoopAsync(socket, _cancellation.Token);
    }

    public void SetProbing(bool on)
    {
        if (on)
        {
            if (_probing is not null) return;
            _probing = new CancellationTokenSource();
            _ = ProbeLoopAsync(_probing.Token);
        }
        else
        {
            _probing?.Cancel();
            _probing?.Dispose();
            _probing = null;
        }
    }

    private async Task ProbeLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Probe();
            try { await Task.Delay(ProbeInterval, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
        }
    }

    public void Probe()
    {
        var socket = _socket;
        if (socket is null) return;

        var datagram = ProbeDatagram();
        foreach (var address in BroadcastAddresses())
        {
            try
            {
                socket.Send(datagram, datagram.Length, new IPEndPoint(address, Port));
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException) { return; }
        }
    }

    public static byte[] ProbeDatagram() =>
        Encoding.UTF8.GetBytes($$"""{"app":"{{AirTunConfig.AppId}}","v":{{Version}},"probe":1}""");

    private static IEnumerable<IPAddress> BroadcastAddresses()
    {
        var seen = new HashSet<string> { IPAddress.Broadcast.ToString() };
        yield return IPAddress.Broadcast;

        NetworkInterface[] interfaces;
        try { interfaces = NetworkInterface.GetAllNetworkInterfaces(); }
        catch (NetworkInformationException) { yield break; }

        foreach (var nic in interfaces)
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            IPInterfaceProperties properties;
            try { properties = nic.GetIPProperties(); }
            catch (NetworkInformationException) { continue; }

            foreach (var unicast in properties.UnicastAddresses)
            {
                var broadcast = BroadcastFor(unicast.Address, unicast.IPv4Mask);
                if (broadcast is not null && seen.Add(broadcast.ToString())) yield return broadcast;
            }

            foreach (var gateway in properties.GatewayAddresses)
            {
                if (gateway.Address.AddressFamily == AddressFamily.InterNetwork && seen.Add(gateway.Address.ToString()))
                {
                    yield return gateway.Address;
                }
            }
        }
    }

    public static IPAddress? BroadcastFor(IPAddress? address, IPAddress? mask)
    {
        if (address is null || mask is null) return null;
        if (address.AddressFamily != AddressFamily.InterNetwork) return null;
        if (mask.AddressFamily != AddressFamily.InterNetwork) return null;

        var host = address.GetAddressBytes();
        var bits = mask.GetAddressBytes();
        if (bits.All(b => b == 0) || bits.All(b => b == 0xFF)) return null;

        var result = new byte[4];
        for (var i = 0; i < 4; i++) result[i] = (byte)(host[i] | (byte)~bits[i]);
        return new IPAddress(result);
    }

    private async Task ReceiveLoopAsync(UdpClient socket, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await socket.ReceiveAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { continue; }

            var from = result.RemoteEndPoint.Address.ToString();
            Observe(result.Buffer, from);
            Expire();
        }
    }

    public bool Observe(byte[] datagram, string? senderIp = null)
    {
        if (!TryParseBeacon(datagram, _clock(), senderIp, out var device))
        {
            return false;
        }
        Add(device!);
        return true;
    }

    public static bool TryParseBeacon(byte[] bytes, DateTimeOffset seen, string? senderIp, out Device? device)
    {
        device = null;
        try
        {
            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            if (root.TryGetProperty("app", out var appProp) && appProp.GetString() != AirTunConfig.AppId)
                return false;

            // If this is a probe request datagram, ignore it (do not parse as a device beacon)
            if (root.TryGetProperty("probe", out _))
                return false;

            if (!root.TryGetProperty("v", out var vProp) || vProp.GetInt32() != Version)
                return false;

            // A valid device beacon MUST have a device name
            if (!root.TryGetProperty("device", out var dProp) || string.IsNullOrWhiteSpace(dProp.GetString()))
                return false;

            var deviceName = dProp.GetString()!;
            var portNumber = root.TryGetProperty("port", out var pProp) ? pProp.GetInt32() : AirTunConfig.DefaultSocksPort;
            var pinRequired = !root.TryGetProperty("pin_required", out var pinProp) || pinProp.GetBoolean();
            var pin = root.TryGetProperty("pin", out var pinValProp) ? pinValProp.GetString() : null;

            // Always prefer senderIp because it represents the actual physical LAN route where the UDP packet arrived
            var host = !string.IsNullOrWhiteSpace(senderIp) ? senderIp : (root.TryGetProperty("host", out var hostProp) ? hostProp.GetString() : null);

            if (string.IsNullOrWhiteSpace(host)) return false;

            // Ignore local machine's own IP addresses so the PC never discovers itself
            if (IsLocalMachineAddress(host)) return false;

            device = new Device(
                Host: host,
                PortNumber: portNumber,
                DeviceName: deviceName ?? "Android Device",
                Pin: pin,
                PinRequired: pinRequired,
                Seen: seen
            );
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void Add(Device device)
    {
        bool isNew;
        lock (_lock)
        {
            isNew = !_devices.TryGetValue(device.Key, out var known) || !SameToUser(known, device);
            _devices[device.Key] = device;
        }
        if (isNew)
        {
            DiagnosticLog?.Invoke($"[DISCOVERY] Discovered device: {device.DeviceName} ({device.Host}:{device.PortNumber})");
            Notify();
        }
    }

    private static bool IsLocalMachineAddress(string ip)
    {
        if (ip == "127.0.0.1" || ip == "localhost" || ip == "::1") return true;
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var nic in interfaces)
            {
                var props = nic.GetIPProperties();
                foreach (var unicast in props.UnicastAddresses)
                {
                    if (unicast.Address.ToString() == ip) return true;
                }
            }
        }
        catch { }
        return false;
    }

    private static bool SameToUser(Device a, Device b) =>
        a.Host == b.Host && a.PortNumber == b.PortNumber && a.DeviceName == b.DeviceName && a.Pin == b.Pin && a.PinRequired == b.PinRequired;


    internal void Expire()
    {
        var now = _clock();
        bool changed;
        lock (_lock)
        {
            var dead = _devices.Where(kv => now - kv.Value.Seen > StaleTimeout).Select(kv => kv.Key).ToList();
            foreach (var key in dead) _devices.Remove(key);
            changed = dead.Count > 0;
        }
        if (changed) Notify();
    }

    private void Notify() => DevicesChanged?.Invoke(Devices);

    public IReadOnlyList<Device> Devices
    {
        get { lock (_lock) return _devices.Values.OrderBy(d => d.DeviceName).ToList(); }
    }

    public void Dispose()
    {
        SetProbing(false);
        _cancellation?.Cancel();
        _socket?.Dispose();
        _socket = null;
        _cancellation?.Dispose();
        _cancellation = null;
    }
}
