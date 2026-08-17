using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using AirTun.App.Services;
using AirTun.Core;
using H.NotifyIcon;

namespace AirTun.App;

public sealed partial class MainWindow : Window
{
    private readonly AppController _controller = new();
    private readonly DispatcherTimer _durationTimer = new();
    private DateTimeOffset _connectedStart = DateTimeOffset.MinValue;
    private AppWindow? _appWindow;
    private TaskbarIcon? _trayIcon;

    public MainWindow()
    {
        this.InitializeComponent();
        CenterAndSizeWindow(460, 680);
        InitializeTray();

        _controller.StateChanged += OnStateChanged;
        _controller.DevicesChanged += OnDevicesChanged;
        _controller.StatsSampled += OnStatsSampled;
        LocalLog.Changed += OnLogChanged;

        _durationTimer.Interval = TimeSpan.FromSeconds(1);
        _durationTimer.Tick += (_, _) => UpdateDuration();

        _controller.RecoverOnStartup();
        _controller.StartDiscovery();
        ApplyStrings();
    }

    private void InitializeTray()
    {
        try
        {
            var openCommand = new XamlUICommand();
            openCommand.ExecuteRequested += (_, _) => this.Activate();

            _trayIcon = new TaskbarIcon
            {
                ToolTipText = "AirTun",
                LeftClickCommand = openCommand,
                NoLeftClickDelay = true,
            };
            _trayIcon.ForceCreate(enablesEfficiencyMode: false);
        }
        catch
        {
        }
    }

    private void CenterAndSizeWindow(int width, int height)
    {
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        if (_appWindow is not null)
        {
            _appWindow.Resize(new SizeInt32(width, height));
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            if (displayArea is not null)
            {
                var centeredPosition = new PointInt32(
                    (displayArea.WorkArea.Width - width) / 2,
                    (displayArea.WorkArea.Height - height) / 2
                );
                _appWindow.Move(centeredPosition);
            }
        }
    }

    private void ApplyStrings()
    {
        TextAppName.Text = Strings.AppName;
        TextTagline.Text = Strings.Tagline;
        BtnLangToggle.Content = Strings.IsPersian ? "EN" : "FA";

        TextModeHeader.Text = Strings.IsPersian ? "انتخاب حالت تونل:" : "Select Tunneling Mode:";
        RadioTun.Content = Strings.ModeFullTun;
        RadioProxy.Content = Strings.ModeFastProxy;
        TextModeDesc.Text = _controller.ActiveMode == "tun" ? Strings.ModeDescTun : Strings.ModeDescProxy;

        TextDiscoveredHeader.Text = Strings.DiscoveredDevices;
        TextNoDevices.Text = Strings.SearchingDevices;
        TextManualHeader.Text = Strings.ManualConnect;
        InputHost.Header = Strings.HostLabel;
        InputPin.Header = Strings.PinLabel;
        InputPin.PlaceholderText = "1234";
        BtnConnect.Content = Strings.ConnectAction;

        BtnDisconnect.Content = Strings.DisconnectAction;
        BtnErrorDismiss.Content = Strings.DismissAction;
        BtnErrorRetry.Content = Strings.RetryAction;

        ExpanderLogs.Header = Strings.AdvancedSection;
        BtnCopyLogs.Content = Strings.CopyLogsAction;
        BtnClearLogs.Content = Strings.ClearLogsAction;
    }

    private void OnStateChanged(ConnectionState state)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (state)
            {
                case ConnectionState.IdleState:
                    PanelIdle.Visibility = Visibility.Visible;
                    PanelConnected.Visibility = Visibility.Collapsed;
                    PanelError.Visibility = Visibility.Collapsed;
                    TextStatus.Text = Strings.StatusIdle;
                    StatusDot.Fill = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
                    _durationTimer.Stop();
                    break;

                case ConnectionState.PreparingState:
                    PanelIdle.Visibility = Visibility.Visible;
                    PanelConnected.Visibility = Visibility.Collapsed;
                    PanelError.Visibility = Visibility.Collapsed;
                    TextStatus.Text = Strings.StatusPreparing;
                    StatusDot.Fill = (SolidColorBrush)Application.Current.Resources["WarningBrush"];
                    break;

                case ConnectionState.ConnectedState c:
                    PanelIdle.Visibility = Visibility.Collapsed;
                    PanelConnected.Visibility = Visibility.Visible;
                    PanelError.Visibility = Visibility.Collapsed;
                    TextStatus.Text = Strings.StatusConnected;
                    StatusDot.Fill = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
                    TextConnectedDevice.Text = c.DeviceName;
                    TextConnectedEndpoint.Text = $"{c.Host}:{c.Port} ({c.Mode.ToUpper()} Mode)";
                    _connectedStart = DateTimeOffset.UtcNow;
                    _durationTimer.Start();
                    break;

                case ConnectionState.ErrorState err:
                    PanelIdle.Visibility = Visibility.Collapsed;
                    PanelConnected.Visibility = Visibility.Collapsed;
                    PanelError.Visibility = Visibility.Visible;
                    TextStatus.Text = Strings.StatusError;
                    StatusDot.Fill = (SolidColorBrush)Application.Current.Resources["DangerBrush"];
                    TextErrorTitle.Text = Strings.GetErrorTitle(err.Message);
                    TextErrorBody.Text = Strings.GetErrorBody(err.Message);
                    _durationTimer.Stop();
                    break;
            }
        });
    }

    private void OnDevicesChanged(IReadOnlyList<LanDiscovery.Device> devices)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ListDevices.ItemsSource = devices;
            TextNoDevices.Visibility = devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void OnStatsSampled(TunnelStats.Sample sample)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TextDownSpeed.Text = TunnelStats.FormatRate(sample.DownloadRateBps);
            TextUpSpeed.Text = TunnelStats.FormatRate(sample.UploadRateBps);
            TextDownTotal.Text = TunnelStats.FormatBytes(sample.BytesDown);
            TextUpTotal.Text = TunnelStats.FormatBytes(sample.BytesUp);
            TextLatency.Text = sample.LatencyMs > 0 ? $"Ping: {sample.LatencyMs} ms" : "Ping: --";
        });
    }

    private void UpdateDuration()
    {
        if (_connectedStart != DateTimeOffset.MinValue)
        {
            var elapsed = DateTimeOffset.UtcNow - _connectedStart;
            TextDuration.Text = $"{Strings.DurationLabel}: {elapsed:hh\\:mm\\:ss}";
        }
    }

    private void OnLogChanged()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var lines = LocalLog.Snapshot().Select(e => $"{e.ElapsedSeconds:F1}s: {e.Message}");
            TextLogsViewer.Text = string.Join(Environment.NewLine, lines);
        });
    }

    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        var host = InputHost.Text.Trim();
        var pin = InputPin.Text.Trim();

        if (string.IsNullOrWhiteSpace(host)) host = "192.168.43.1";
        if (!PinCode.IsValid(pin))
        {
            TextStatus.Text = Strings.PinHint;
            StatusDot.Fill = (SolidColorBrush)Application.Current.Resources["DangerBrush"];
            return;
        }

        await _controller.ConnectAsync(host, AirTunConfig.DefaultSocksPort, pin, "Manual Host");
    }

    private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
    {
        _controller.Disconnect();
    }

    private void BtnErrorDismiss_Click(object sender, RoutedEventArgs e)
    {
        _controller.Disconnect();
    }

    private async void BtnErrorRetry_Click(object sender, RoutedEventArgs e)
    {
        var host = InputHost.Text.Trim();
        var pin = InputPin.Text.Trim();
        if (string.IsNullOrWhiteSpace(host)) host = "192.168.43.1";
        if (string.IsNullOrWhiteSpace(pin)) pin = "1234";

        await _controller.ConnectAsync(host, AirTunConfig.DefaultSocksPort, pin, "Retried Host");
    }

    private void ListDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListDevices.SelectedItem is LanDiscovery.Device d)
        {
            InputHost.Text = d.Host;
            if (string.IsNullOrWhiteSpace(InputPin.Text))
            {
                InputPin.Focus(FocusState.Programmatic);
            }
            else
            {
                _ = _controller.ConnectAsync(d.Host, d.PortNumber, InputPin.Text.Trim(), d.DeviceName);
            }
        }
    }

    private void RadioMode_Checked(object sender, RoutedEventArgs e)
    {
        if (RadioTun is null || RadioProxy is null) return;
        _controller.ActiveMode = RadioTun.IsChecked == true ? "tun" : "proxy";
        TextModeDesc.Text = _controller.ActiveMode == "tun" ? Strings.ModeDescTun : Strings.ModeDescProxy;
    }

    private void BtnLangToggle_Click(object sender, RoutedEventArgs e)
    {
        Strings.IsPersian = !Strings.IsPersian;
        ApplyStrings();
    }

    private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = root.RequestedTheme == ElementTheme.Light ? ElementTheme.Dark : ElementTheme.Light;
            BtnThemeToggle.Content = root.RequestedTheme == ElementTheme.Light ? "☀️" : "🌙";
        }
    }

    private void BtnCopyLogs_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(TextLogsViewer.Text);
        Clipboard.SetContent(package);
    }

    private void BtnClearLogs_Click(object sender, RoutedEventArgs e)
    {
        LocalLog.Clear();
    }
}
