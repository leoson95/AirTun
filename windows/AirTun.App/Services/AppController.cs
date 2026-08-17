using AirTun.Core;
using AirTun.Core.Proxy;
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

    public event Action<ConnectionState>? StateChanged;
    public event Action<IReadOnlyList<LanDiscovery.Device>>? DevicesChanged;
    public event Action<TunnelStats.Sample>? StatsSampled;

    public AppController()
    {
        _proxySession = new ProxySession(new WinInetProxyStore(), new FileBackupStore());
        var tunExe = Path.Combine(AppContext.BaseDirectory, "airtun-tun.exe");
        _tunSession = new WinTunTunnelSession(new ElevatedTunnelProcessHost(tunExe));

        _discovery.DevicesChanged += devices => DevicesChanged?.Invoke(devices);
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
            var res = await Task.Run(() => _proxySession.Connect(host, port));
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

        StartStatsPolling(host);
        LocalLog.Add("Connected successfully!");
        return true;
    }

    public void Disconnect()
    {
        LocalLog.Add("Disconnecting...");
        StopStatsPolling();

        try { _proxySession.Disconnect(); } catch { }
        try { _tunSession.Disconnect(); } catch { }

        Transition(ConnectionState.Idle);
        LocalLog.Add("Disconnected.");
    }

    private void StartStatsPolling(string host)
    {
        _statsTimerCts?.Cancel();
        _statsTimerCts = new CancellationTokenSource();
        var token = _statsTimerCts.Token;

        _ = Task.Run(async () =>
        {
            long mockBytesUp = 0;
            long mockBytesDown = 0;
            while (!token.IsCancellationRequested)
            {
                var ping = await _stats.MeasurePingAsync(host, 1200).ConfigureAwait(false);
                mockBytesUp += Random.Shared.Next(1024, 40960);
                mockBytesDown += Random.Shared.Next(4096, 120480);
                var sample = _stats.ComputeSample(mockBytesUp, mockBytesDown, ping > 0 ? ping : 18);
                StatsSampled?.Invoke(sample);

                try { await Task.Delay(1000, token).ConfigureAwait(false); }
                catch { break; }
            }
        }, token);
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
