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
using AirTun.Core.Geo;
using AirTun.Core.Routing;
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
        CenterAndSizeWindow(480, 720);
        InitializeTray();

        _controller.StateChanged += OnStateChanged;
        _controller.DevicesChanged += OnDevicesChanged;
        _controller.StatsSampled += OnStatsSampled;
        _controller.GeoLocationUpdated += OnGeoLocationUpdated;
        LocalLog.Changed += OnLogChanged;
        _controller.Routing.RulesChanged += RefreshCustomRulesList;

        _durationTimer.Interval = TimeSpan.FromSeconds(1);
        _durationTimer.Tick += (_, _) => UpdateDuration();

        _controller.RecoverOnStartup();
        _controller.StartDiscovery();

        SwitchBypassDomestic.IsOn = _controller.Routing.BypassDomestic;
        RefreshCustomRulesList();
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
        catch { }
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

        TextBypassTitle.Text = Strings.BypassDomesticTitle;
        TextBypassDesc.Text = Strings.BypassDomesticDesc;

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

        ExpanderRouting.Header = Strings.RoutingHeader;
        TextCustomRulesIntro.Text = Strings.CustomRulesHeader;
        InputNewRulePattern.PlaceholderText = Strings.RulePatternPlaceholder;

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
                    var bypassInfo = _controller.Routing.BypassDomestic ? " | 🇮🇷 Bypass IR: ON" : "";
                    TextConnectedEndpoint.Text = $"{c.Host}:{c.Port} ({c.Mode.ToUpper()} Mode){bypassInfo}";
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

    private void OnGeoLocationUpdated(GeoIpInfo? geo)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (geo is not null)
            {
                TextGeoLocation.Text = $"{geo.FlagEmoji} {geo.Country} ({geo.Ip})";
                TextGeoIsp.Text = $"{geo.City} · {geo.Isp}";
            }
            else
            {
                TextGeoLocation.Text = Strings.FetchingGeo;
                TextGeoIsp.Text = "";
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

    private void RefreshCustomRulesList()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ListCustomRules.ItemsSource = null;
            ListCustomRules.ItemsSource = _controller.Routing.CustomRules.ToList();
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

    private void SwitchBypassDomestic_Toggled(object sender, RoutedEventArgs e)
    {
        _controller.Routing.BypassDomestic = SwitchBypassDomestic.IsOn;
        _controller.SaveCurrentSettings();
        LocalLog.Add($"Bypass Domestic Sites set to: {SwitchBypassDomestic.IsOn}");
    }

    private void BtnAddRule_Click(object sender, RoutedEventArgs e)
    {
        var pattern = InputNewRulePattern.Text.Trim();
        if (string.IsNullOrWhiteSpace(pattern)) return;

        var action = ComboNewRuleAction.SelectedIndex switch
        {
            1 => RuleAction.Proxy,
            2 => RuleAction.Block,
            _ => RuleAction.Direct,
        };

        var type = pattern.StartsWith("*.") ? RuleType.DomainSuffix : RuleType.DomainFull;
        _controller.Routing.AddCustomRule(new RoutingRule(type, pattern.TrimStart('*', '.'), action));
        _controller.SaveCurrentSettings();

        InputNewRulePattern.Text = "";
        RefreshCustomRulesList();
        LocalLog.Add($"Added custom rule: {pattern} -> {action}");
    }

    private void BtnDeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RoutingRule rule })
        {
            _controller.Routing.RemoveCustomRule(rule);
            _controller.SaveCurrentSettings();
            RefreshCustomRulesList();
            LocalLog.Add($"Removed rule: {rule.Pattern}");
        }
    }

    private async void BtnRefreshGeo_Click(object sender, RoutedEventArgs e)
    {
        await _controller.RefreshGeoLocationAsync();
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
