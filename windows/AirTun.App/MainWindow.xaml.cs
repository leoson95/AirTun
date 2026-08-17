using System.Diagnostics;
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

    private string? _detectedHost = null;
    private int _detectedPort = AirTunConfig.DefaultSocksPort;
    private string _detectedName = "Android Device";
    private int _currentTabIndex = 0;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    private const int GWL_STYLE = -16;
    private const long WS_MAXIMIZEBOX = 0x00010000L;

    public MainWindow()
    {
        this.InitializeComponent();
        this.Title = "AirTun";

        ConfigureWindow(620, 920);
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

        SwitchBypassDomestic.IsOn = _controller.Settings.BypassDomestic;
        SwitchCloseToTray.IsOn = _controller.Settings.CloseToTray;
        SwitchMinimizeToTray.IsOn = _controller.Settings.MinimizeToTray;

        RefreshCustomRulesList();
        UpdateModeCardsUi();
        ApplyStrings();
        SelectTab(0);
    }

    private void ConfigureWindow(int width, int height)
    {
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        try
        {
            if (IntPtr.Size == 8)
            {
                var style = GetWindowLongPtr64(hWnd, GWL_STYLE).ToInt64();
                SetWindowLongPtr64(hWnd, GWL_STYLE, new IntPtr(style & ~WS_MAXIMIZEBOX));
            }
            else
            {
                var style = GetWindowLong32(hWnd, GWL_STYLE);
                SetWindowLong32(hWnd, GWL_STYLE, (int)(style & ~WS_MAXIMIZEBOX));
            }
        }
        catch { }

        if (_appWindow is not null)
        {
            _appWindow.Resize(new SizeInt32(width, height));
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = true;
            }

            _appWindow.Closing += (sender, args) =>
            {
                if (_controller.Settings.CloseToTray)
                {
                    args.Cancel = true;
                    _appWindow.Hide();
                    LocalLog.Add("AirTun minimized to system tray on Close.");
                }
                else
                {
                    ExitApp();
                }
            };

            _appWindow.Changed += (sender, args) =>
            {
                if (args.DidPresenterChange && _controller.Settings.MinimizeToTray)
                {
                    if (_appWindow.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Minimized)
                    {
                        _appWindow.Hide();
                        LocalLog.Add("AirTun minimized to system tray.");
                    }
                }
            };

            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            if (displayArea is not null)
            {
                var centeredPosition = new PointInt32(
                    (displayArea.WorkArea.Width - width) / 2,
                    (displayArea.WorkArea.Height - height) / 2
                );
                _appWindow.Move(centeredPosition);
            }

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                this.ExtendsContentIntoTitleBar = true;
                this.SetTitleBar(AppTitleBar);

                var titleBar = _appWindow.TitleBar;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonForegroundColor = Colors.White;
                titleBar.ButtonInactiveForegroundColor = ColorHelper.FromArgb(255, 160, 160, 160);
                titleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(40, 255, 255, 255);
                titleBar.ButtonHoverForegroundColor = Colors.White;
                titleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(70, 255, 255, 255);
            }
        }
    }

    private void InitializeTray()
    {
        try
        {
            var openCommand = new XamlUICommand();
            openCommand.ExecuteRequested += (_, _) => ShowAppWindow();

            var menu = new MenuFlyout();

            var itemOpen = new MenuFlyoutItem
            {
                Text = Strings.TrayOpen,
            };
            itemOpen.Click += (_, _) => ShowAppWindow();
            menu.Items.Add(itemOpen);

            menu.Items.Add(new MenuFlyoutSeparator());

            var itemExit = new MenuFlyoutItem
            {
                Text = Strings.TrayExit,
            };
            itemExit.Click += (_, _) => ExitApp();
            menu.Items.Add(itemExit);

            _trayIcon = new TaskbarIcon
            {
                ToolTipText = "AirTun",
                LeftClickCommand = openCommand,
                DoubleClickCommand = openCommand,
                ContextFlyout = menu,
                NoLeftClickDelay = true,
            };
            _trayIcon.ForceCreate(enablesEfficiencyMode: false);
        }
        catch { }
    }

    private void ShowAppWindow()
    {
        _appWindow?.Show();
        if (_appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.Restore();
        }
        this.Activate();
    }

    private void ExitApp()
    {
        try
        {
            _controller.Disconnect();
            _trayIcon?.Dispose();
        }
        catch { }
        finally
        {
            Environment.Exit(0);
        }
    }

    private void SelectTab(int index)
    {
        _currentTabIndex = index;

        ViewTabConnect.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        ViewTabRouting.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        ViewTabLogs.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        ViewTabAbout.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;

        var accent = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
        var muted = (SolidColorBrush)Application.Current.Resources["LabelSecondary"];

        NavTextConnect.Foreground = index == 0 ? accent : muted;
        NavTextConnect.FontWeight = index == 0 ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;

        NavTextRouting.Foreground = index == 1 ? accent : muted;
        NavTextRouting.FontWeight = index == 1 ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;

        NavTextLogs.Foreground = index == 2 ? accent : muted;
        NavTextLogs.FontWeight = index == 2 ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;

        NavTextAbout.Foreground = index == 3 ? accent : muted;
        NavTextAbout.FontWeight = index == 3 ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
    }

    private void NavBtnConnect_Click(object sender, RoutedEventArgs e) => SelectTab(0);
    private void NavBtnRouting_Click(object sender, RoutedEventArgs e) => SelectTab(1);
    private void NavBtnLogs_Click(object sender, RoutedEventArgs e) => SelectTab(2);
    private void NavBtnAbout_Click(object sender, RoutedEventArgs e) => SelectTab(3);

    private void ApplyStrings()
    {
        BtnLangToggle.Content = Strings.IsPersian ? "EN" : "FA";

        NavTextConnect.Text = Strings.TabConnect;
        NavTextRouting.Text = Strings.TabRouting;
        NavTextLogs.Text = Strings.TabLogs;
        NavTextAbout.Text = Strings.TabAbout;

        TextTunSub.Text = Strings.ModeTunSubtitle;
        TextProxySub.Text = Strings.ModeProxySubtitle;

        TextBypassTitle.Text = Strings.BypassDomesticTitle;
        TextBypassDesc.Text = Strings.BypassDomesticDesc;
        TextCustomRulesHeader.Text = Strings.CustomRulesHeader;
        InputNewRulePattern.PlaceholderText = Strings.RulePatternPlaceholder;

        TextPinHint.Text = Strings.PinHint;
        BtnConnect.Content = Strings.ConnectAction;
        BtnDisconnect.Content = Strings.DisconnectAction;
        BtnErrorDismiss.Content = Strings.DismissAction;
        BtnErrorRetry.Content = Strings.RetryAction;

        BtnCopyLogs.Content = Strings.CopyLogsAction;
        BtnClearLogs.Content = Strings.ClearLogsAction;

        TextSettingsHeader.Text = Strings.SettingsHeader;
        TextCloseToTrayTitle.Text = Strings.CloseToTrayTitle;
        TextCloseToTrayDesc.Text = Strings.CloseToTrayDesc;
        TextMinimizeToTrayTitle.Text = Strings.MinimizeToTrayTitle;
        TextMinimizeToTrayDesc.Text = Strings.MinimizeToTrayDesc;

        TextAboutDescription.Text = Strings.AboutDescription;
        BtnOpenGithub.Content = Strings.OpenGithubAction;
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
            if (devices.Count > 0)
            {
                var first = devices[0];
                _detectedHost = first.Host;
                _detectedPort = first.PortNumber;
                _detectedName = first.DeviceName;

                TextDetectedPhoneName.Text = first.DeviceName;
                TextDetectedPhoneIp.Text = $"IP: {first.Host} (Ready to Pair)";
                TextSignalStatus.Text = "⚡ Available";
                BadgeSignal.Background = (SolidColorBrush)Application.Current.Resources["FillTertiary"];
                BtnConnect.IsEnabled = true;
                InputPin.Focus(FocusState.Programmatic);
            }
            else
            {
                _detectedHost = null;
                TextDetectedPhoneName.Text = Strings.SearchingDevices;
                TextDetectedPhoneIp.Text = "Turn on hotspot / USB and tap START in Android App";
                TextSignalStatus.Text = "📡 Scanning";
                BadgeSignal.Background = (SolidColorBrush)Application.Current.Resources["FillTertiary"];
                BtnConnect.IsEnabled = false;
            }
        });
    }

    private void OnStatsSampled(TunnelStats.Sample sample)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TextDownSpeed.Text = TunnelStats.FormatRate(sample.DownloadRateBps);
            TextUpSpeed.Text = TunnelStats.FormatRate(sample.UploadRateBps);
            TextDownTotal.Text = $"Total: {TunnelStats.FormatBytes(sample.BytesDown)}";
            TextUpTotal.Text = $"Total: {TunnelStats.FormatBytes(sample.BytesUp)}";
            TextLatency.Text = sample.LatencyMs > 0 ? $"Latency: {sample.LatencyMs} ms" : "Latency: --";
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
        var pin = InputPin.Text.Trim();

        if (string.IsNullOrWhiteSpace(_detectedHost))
        {
            TextStatus.Text = "Please wait until phone is detected...";
            StatusDot.Fill = (SolidColorBrush)Application.Current.Resources["WarningBrush"];
            return;
        }

        if (!PinCode.IsValid(pin))
        {
            TextStatus.Text = Strings.PinHint;
            StatusDot.Fill = (SolidColorBrush)Application.Current.Resources["DangerBrush"];
            InputPin.Focus(FocusState.Programmatic);
            return;
        }

        await _controller.ConnectAsync(_detectedHost, _detectedPort, pin, _detectedName);
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
        var pin = InputPin.Text.Trim();
        if (string.IsNullOrWhiteSpace(pin)) pin = "1234";
        var host = _detectedHost ?? "192.168.43.1";

        await _controller.ConnectAsync(host, _detectedPort, pin, _detectedName);
    }

    private void CardModeTun_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _controller.ActiveMode = "tun";
        UpdateModeCardsUi();
    }

    private void CardModeProxy_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _controller.ActiveMode = "proxy";
        UpdateModeCardsUi();
    }

    private void UpdateModeCardsUi()
    {
        var isTun = _controller.ActiveMode == "tun";
        var accent = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
        var hairline = (SolidColorBrush)Application.Current.Resources["HairlineBrush"];
        var tertiary = (SolidColorBrush)Application.Current.Resources["FillTertiary"];
        var secondary = (SolidColorBrush)Application.Current.Resources["FillSecondary"];

        CardModeTun.BorderBrush = isTun ? accent : hairline;
        CardModeTun.BorderThickness = new Thickness(isTun ? 2 : 1);
        CardModeTun.Background = isTun ? tertiary : secondary;

        CardModeProxy.BorderBrush = !isTun ? accent : hairline;
        CardModeProxy.BorderThickness = new Thickness(!isTun ? 2 : 1);
        CardModeProxy.Background = !isTun ? tertiary : secondary;
    }

    private void SwitchBypassDomestic_Toggled(object sender, RoutedEventArgs e)
    {
        _controller.Routing.BypassDomestic = SwitchBypassDomestic.IsOn;
        _controller.SaveCurrentSettings();
        LocalLog.Add($"Bypass Domestic Sites set to: {SwitchBypassDomestic.IsOn}");
    }

    private void SwitchCloseToTray_Toggled(object sender, RoutedEventArgs e)
    {
        _controller.Settings.CloseToTray = SwitchCloseToTray.IsOn;
        _controller.SaveCurrentSettings();
        LocalLog.Add($"Close to Tray set to: {SwitchCloseToTray.IsOn}");
    }

    private void SwitchMinimizeToTray_Toggled(object sender, RoutedEventArgs e)
    {
        _controller.Settings.MinimizeToTray = SwitchMinimizeToTray.IsOn;
        _controller.SaveCurrentSettings();
        LocalLog.Add($"Minimize to Tray set to: {SwitchMinimizeToTray.IsOn}");
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

    private void BtnOpenGithub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/leoson95/AirTun",
                UseShellExecute = true,
            });
        }
        catch { }
    }
}
