using AirTun.Core;
using AirTun.Core.Geo;
using AirTun.Core.Proxy;
using AirTun.Core.Routing;
using AirTun.Core.Settings;
using AirTun.Core.Tunnel;

namespace AirTun.App.Services;

public sealed class AppController : IDisposable
{
    private readonly LanDiscovery _discovery = new();
    private readonly ProxySession _proxySession;
    private readonly WinTunTunnelSession _tunSession;
    private readonly TunnelStats _stats = new();
    private CancellationTokenSource? _statsTimerCts;

    public ConnectionState State { get; private set; } = ConnectionState.Idle;
    public string ActiveMode { get; set; } = "tun";

    public RoutingManager Routing { get; } = new();
    public GeoIpService GeoIp { get; } = new();
    public AppSettings Settings { get; private set; } = new();
    public GeoIpInfo? CurrentGeo { get; private set; }

    public event Action<ConnectionState>? StateChanged;
    public event Action<IReadOnlyList<LanDiscovery.Device>>? DevicesChanged;
    public event Action<TunnelStats.Sample>? StatsSampled;
    public event Action<GeoIpInfo?>? GeoLocationUpdated;

    public AppController()
    {
        _proxySession = new ProxySession(new WinInetProxyStore(), new FileBackupStore());
        var tunExe = Path.Combine(AppContext.BaseDirectory, "airtun-tun.exe");
        _tunSession = new WinTunTunnelSession(new ElevatedTunnelProcessHost(tunExe));

        _discovery.DevicesChanged += devices => DevicesChanged?.Invoke(devices);
        _discovery.DiagnosticLog += msg => LocalLog.Add(msg);

        LoadSavedSettings();
    }

    private void LoadSavedSettings()
    {
        Settings = AppSettings.Load();
        Routing.BypassDomestic = Settings.BypassDomestic;
        Routing.BypassLan = Settings.BypassLan;
        foreach (var rule in Settings.CustomRules)
        {
            Routing.CustomRules.Add(rule);
        }
    }

    public void SaveCurrentSettings()
    {
        Settings.BypassDomestic = Routing.BypassDomestic;
        Settings.BypassLan = Routing.BypassLan;
        Settings.CustomRules = [.. Routing.CustomRules];
        Settings.Save();
    }

    public void StartDiscovery()
    {
        _discovery.Start();
        _discovery.SetProbing(true);
        LocalLog.Add("Discovery started on port " + LanDiscovery.Port);
    }

    public void StopDiscovery()
    {
        _discovery.SetProbing(false);
    }

    public async Task<bool> ConnectAsync(string host, int port, string pinCode, string deviceName)
    {
        if (State is not (ConnectionState.IdleState or ConnectionState.DiscoveringState or ConnectionState.ErrorState))
            return false;

        Transition(ConnectionState.Preparing);
        LocalLog.Add($"Connecting to {host}:{port} ({ActiveMode} mode) with PIN {pinCode}...");

        if (ActiveMode == "proxy")
        {
            var bypassList = Routing.BuildWinInetBypassList();
            LocalLog.Add($"Applying system proxy with {bypassList.Split(';').Length} bypass entries...");
            var res = await Task.Run(() => _proxySession.Connect(host, port, bypassList));
            if (!res.Ok)
            {
                LocalLog.Add($"Proxy connect failed: {res.ErrorCode}");
                Transition(new ConnectionState.ErrorState(ErrorCode.PortInUse, res.ErrorCode));
                return false;
            }
        }
        else
        {
            var res = await Task.Run(() => _tunSession.Connect(host, port, pinCode));
            if (!res.Ok)
            {
                LocalLog.Add($"TUN connect failed: {res.ErrorCode}");
                Transition(new ConnectionState.ErrorState(ErrorCode.TunnelFailed, res.ErrorCode));
                return false;
            }
        }

        Transition(new ConnectionState.ConnectedState(
            Host: host,
            Port: port,
            PinCode: pinCode,
            DeviceName: deviceName,
            Mode: ActiveMode
        ));

        StartStatsPolling(host, port);
        _ = RefreshGeoLocationAsync();
        LocalLog.Add("Connected successfully!");
        return true;
    }

    public async Task RefreshGeoLocationAsync()
    {
        try
        {
            LocalLog.Add("Resolving outbound location and IP...");
            var geo = await GeoIp.FetchOutboundGeoAsync().ConfigureAwait(false);
            CurrentGeo = geo;
            if (geo is not null)
            {
                LocalLog.Add($"Outbound IP: {geo.Ip} ({geo.Country} {geo.FlagEmoji}) - ISP: {geo.Isp}");
            }
            GeoLocationUpdated?.Invoke(geo);
        }
        catch (Exception ex)
        {
            LocalLog.Add($"GeoIP resolution failed: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        LocalLog.Add("Disconnecting...");
        StopStatsPolling();
        CurrentGeo = null;
        GeoLocationUpdated?.Invoke(null);

        try { _proxySession.Disconnect(); } catch { }
        try { _tunSession.Disconnect(); } catch { }

        Transition(ConnectionState.Idle);
        LocalLog.Add("Disconnected.");
    }

    private void StartStatsPolling(string host, int port)
    {
        _statsTimerCts?.Cancel();
        _statsTimerCts = new CancellationTokenSource();
        var token = _statsTimerCts.Token;

        _ = Task.Run(async () =>
        {
            int consecutiveFailures = 0;
            // Wait a brief moment for adapter to be ready, then read baseline
            await Task.Delay(500, token).ConfigureAwait(false);
            var (baseUp, baseDown) = ReadAirTunInterfaceBytes();

            while (!token.IsCancellationRequested)
            {
                // Quick TCP health check to detect if phone server stopped
                bool isAlive = await CheckHostHealthAsync(host, port, 1200).ConfigureAwait(false);
                if (!isAlive)
                {
                    consecutiveFailures++;
                    if (consecutiveFailures >= 2)
                    {
                        LocalLog.Add("Phone server stopped or unreachable. Disconnecting...");
                        _ = Task.Run(() => Disconnect());
                        break;
                    }
                }
                else
                {
                    consecutiveFailures = 0;
                }

                var (rawUp, rawDown) = ReadAirTunInterfaceBytes();
                if (baseUp == 0 && rawUp > 0) baseUp = rawUp;
                if (baseDown == 0 && rawDown > 0) baseDown = rawDown;

                long curBytesUp = Math.Max(0, rawUp - baseUp);
                long curBytesDown = Math.Max(0, rawDown - baseDown);

                var ping = await _stats.MeasurePingAsync(host, 1200).ConfigureAwait(false);
                var sample = _stats.ComputeSample(curBytesUp, curBytesDown, ping > 0 ? ping : 18);
                StatsSampled?.Invoke(sample);

                try { await Task.Delay(1000, token).ConfigureAwait(false); }
                catch { break; }
            }
        }, token);
    }

    private static (long bytesSent, long bytesRecv) ReadAirTunInterfaceBytes()
    {
        try
        {
            var nic = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.Name.Contains("AirTun", StringComparison.OrdinalIgnoreCase)
                                  || n.Description.Contains("AirTun", StringComparison.OrdinalIgnoreCase)
                                  || n.Description.Contains("tun2socks", StringComparison.OrdinalIgnoreCase));
            if (nic != null)
            {
                var stats = nic.GetIPv4Statistics();
                return (stats.BytesSent, stats.BytesReceived);
            }
        }
        catch { }
        return (0, 0);
    }

    private static async Task<bool> CheckHostHealthAsync(string host, int port, int timeoutMs)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var delayTask = Task.Delay(timeoutMs);
            var completed = await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false);
            return completed == connectTask && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private void StopStatsPolling()
    {
        _statsTimerCts?.Cancel();
        _statsTimerCts?.Dispose();
        _statsTimerCts = null;
    }

    private void Transition(ConnectionState newState)
    {
        State = newState;
        StateChanged?.Invoke(newState);
    }

    public void RecoverOnStartup()
    {
        if (_proxySession.RecoverIfCrashed())
        {
            LocalLog.Add("Recovered proxy settings from previous ungraceful exit.");
        }
    }

    public void Dispose()
    {
        Disconnect();
        _discovery.Dispose();
    }
}
