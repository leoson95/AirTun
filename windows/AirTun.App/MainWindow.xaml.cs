using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
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
    private DateTimeOffset _connectedStart = DateTimeOffset.UtcNow;
    private AppWindow? _appWindow;
    private TaskbarIcon? _trayIcon;
    private LanDiscovery.Device? _selectedDevice;

    private readonly List<double> _downHistory = new(30);
    private readonly List<double> _upHistory = new(30);
    private double _peakSpeed = 0;

    private readonly Polygon _polygonDownload = new();
    private readonly Polyline _polylineDownload = new();
    private readonly Polyline _polylineUpload = new();

    private const int GWL_STYLE = -16;
    private const int WS_MAXIMIZEBOX = 0x00010000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    public MainWindow()
    {
        this.InitializeComponent();
        this.Title = "AirTun";

        ConfigureWindow(440, 700);
        InitializeTray();
        InitializeTrafficGraph();


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
        SwitchStartWithWindows.IsOn = _controller.Settings.StartWithWindows;

        RefreshCustomRulesList();
        UpdateModeCardsUi();
        ApplyStrings();
        SelectTab(0);

        // Prepopulate waveform with baseline zeros
        for (int i = 0; i < 30; i++)
        {
            _downHistory.Add(0);
            _upHistory.Add(0);
        }

        // Check if launched with --minimized / --autostart
        var args = Environment.GetCommandLineArgs();
        if (args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) || a.Equals("--autostart", StringComparison.OrdinalIgnoreCase)))
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _appWindow?.Hide();
            });
        }
    }

    private void InitializeTrafficGraph()
    {
        _polygonDownload.Fill = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
        _polygonDownload.Opacity = 0.22;

        _polylineDownload.Stroke = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
        _polylineDownload.StrokeThickness = 2.2;

        _polylineUpload.Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 56, 189, 248));
        _polylineUpload.StrokeThickness = 1.6;

        CanvasTrafficGraph.Children.Add(_polygonDownload);
        CanvasTrafficGraph.Children.Add(_polylineDownload);
        CanvasTrafficGraph.Children.Add(_polylineUpload);
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
            var appIconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(appIconPath))
            {
                try { _appWindow.SetIcon(appIconPath); } catch { }
            }

            uint dpi = 96;
            try { dpi = GetDpiForWindow(hWnd); } catch { }
            if (dpi < 96) dpi = 96;
            double scale = dpi / 96.0;
            int scaledW = (int)Math.Round(width * scale);
            int scaledH = (int)Math.Round(height * scale);

            _appWindow.Resize(new Windows.Graphics.SizeInt32(scaledW, scaledH));
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
            }


            _appWindow.Closing += (sender, args) =>
            {
                if (_controller.Settings.CloseToTray)
                {
                    args.Cancel = true;
                    _appWindow.Hide();
                    _trayIcon?.ShowNotification("AirTun", "Minimized to system tray. Active in background.");
                }
            };

            _appWindow.Changed += (sender, args) =>
            {
                if (args.DidPresenterChange && _appWindow.Presenter is OverlappedPresenter p)
                {
                    if (p.State == OverlappedPresenterState.Minimized && _controller.Settings.MinimizeToTray)
                    {
                        _appWindow.Hide();
                    }
                }
            };
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    private void InitializeTray()
    {
        try
        {
            var openItem = new MenuFlyoutItem
            {
                Text = Strings.TrayOpen,
                Command = new RelayCommand(ShowAppWindow)
            };
            openItem.Click += (_, _) => ShowAppWindow();

            var exitItem = new MenuFlyoutItem
            {
                Text = Strings.TrayExit,
                Command = new RelayCommand(ExitApp)
            };
            exitItem.Click += (_, _) => ExitApp();

            var flyout = new MenuFlyout();
            flyout.Items.Add(openItem);
            flyout.Items.Add(new MenuFlyoutSeparator());
            flyout.Items.Add(exitItem);

            var trayIconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "TrayIcon.ico");
            var icon = File.Exists(trayIconPath)
                ? new System.Drawing.Icon(trayIconPath)
                : System.Drawing.SystemIcons.Shield;

            _trayIcon = new TaskbarIcon
            {
                ToolTipText = "AirTun - Phone Internet Sharing",
                Icon = icon,
                ContextFlyout = flyout,
                NoLeftClickDelay = true,
                LeftClickCommand = new RelayCommand(ToggleAppWindow),
                DoubleClickCommand = new RelayCommand(ShowAppWindow)
            };

            _trayIcon.ForceCreate();
        }
        catch (Exception ex)
        {
            LocalLog.Add($"System Tray notice: {ex.Message}");
        }
    }

    private void ShowAppWindow()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                if (_appWindow is not null)
                {
                    _appWindow.Show();
                    if (_appWindow.Presenter is OverlappedPresenter p)
                    {
                        p.Restore();
                    }
                }
                this.Activate();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                ShowWindow(hwnd, SW_RESTORE);
                SetForegroundWindow(hwnd);
            }
            catch (Exception ex)
            {
                LocalLog.Add($"ShowAppWindow: {ex.Message}");
            }
        });
    }

    private void ToggleAppWindow()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_appWindow is not null && _appWindow.IsVisible)
            {
                _appWindow.Hide();
            }
            else
            {
                ShowAppWindow();
            }
        });
    }

    private void ExitApp()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            try { _controller.Disconnect(); } catch { }
            try { _trayIcon?.Dispose(); } catch { }
            try { Application.Current.Exit(); } catch { }
            Environment.Exit(0);
        });
    }

    private sealed class RelayCommand(Action execute) : System.Windows.Input.ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    private void ApplyStrings()
    {
        Root.FlowDirection = FlowDirection.LeftToRight;
        BtnLangToggle.Content = Strings.IsPersian ? "EN" : "FA";

        NavTextConnect.Text = Strings.TabConnect;
        NavTextRouting.Text = Strings.TabRouting;
        NavTextLogs.Text = Strings.TabLogs;
        NavTextAbout.Text = Strings.TabAbout;

        TextStatus.Text = Strings.StatusIdle;
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
        TextStartWithWindowsTitle.Text = Strings.StartWithWindowsTitle;
        TextStartWithWindowsDesc.Text = Strings.StartWithWindowsDesc;
        TextCloseToTrayTitle.Text = Strings.CloseToTrayTitle;
        TextCloseToTrayDesc.Text = Strings.CloseToTrayDesc;
        TextMinimizeToTrayTitle.Text = Strings.MinimizeToTrayTitle;
        TextMinimizeToTrayDesc.Text = Strings.MinimizeToTrayDesc;

        TextLiveTrafficHeader.Text = Strings.LiveTrafficHeader;
        TextAboutDescription.Text = Strings.AboutDescription;
        BtnOpenGithub.Content = Strings.OpenGithubAction;

        var flowDir = Strings.IsPersian ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        TextBypassDesc.FlowDirection = flowDir;
        TextStartWithWindowsDesc.FlowDirection = flowDir;
        TextCloseToTrayDesc.FlowDirection = flowDir;
        TextMinimizeToTrayDesc.FlowDirection = flowDir;
        TextAboutDescription.FlowDirection = flowDir;
        TextPinHint.FlowDirection = flowDir;
    }

    private void OnStateChanged(ConnectionState state)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (state)
            {
                case ConnectionState.IdleState or ConnectionState.DiscoveringState:
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

                case ConnectionState.ConnectedState connected:
                    PanelIdle.Visibility = Visibility.Collapsed;
                    PanelConnected.Visibility = Visibility.Visible;
                    PanelError.Visibility = Visibility.Collapsed;
                    TextStatus.Text = Strings.StatusConnected;
                    StatusDot.Fill = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
                    TextConnectedDevice.Text = connected.DeviceName;
                    var bypassInfo = _controller.Routing.BypassDomestic ? " | 🇮🇷 Bypass IR: ON" : "";
                    TextConnectedEndpoint.Text = $"{connected.Host}:{connected.Port} ({connected.Mode.ToUpperInvariant()} Mode){bypassInfo}";
                    _connectedStart = DateTimeOffset.UtcNow;
                    _durationTimer.Start();
                    break;

                case ConnectionState.ErrorState err:
                    PanelIdle.Visibility = Visibility.Collapsed;
                    PanelConnected.Visibility = Visibility.Collapsed;
                    PanelError.Visibility = Visibility.Visible;
                    TextStatus.Text = Strings.StatusError;
                    StatusDot.Fill = (SolidColorBrush)Application.Current.Resources["DangerBrush"];
                    TextErrorTitle.Text = Strings.GetErrorTitle(err.Code.ToString());
                    TextErrorBody.Text = err.Message ?? Strings.GetErrorBody(err.Code.ToString());
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
                TextGeoLocation.Text = "🌐 Public IP Hidden";
                TextGeoIsp.Text = "Traffic routing active";
            }
        });
    }

    private void OnDevicesChanged(IReadOnlyList<LanDiscovery.Device> devices)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (devices.Count > 0)
            {
                _selectedDevice = devices[0];
                TextDetectedPhoneName.Text = _selectedDevice.DeviceName;
                TextDetectedPhoneIp.Text = $"{_selectedDevice.Host}:{_selectedDevice.PortNumber}";
                if (!string.IsNullOrWhiteSpace(_selectedDevice.Pin))
                {
                    InputPin.Text = _selectedDevice.Pin;
                }
                TextSignalStatus.Text = Strings.IsPersian ? "● آماده اتصال" : "● Ready";
                BadgeSignal.Background = (SolidColorBrush)Application.Current.Resources["AccentSoftBrush"];
            }
            else
            {
                _selectedDevice = null;
                TextDetectedPhoneName.Text = Strings.IsPersian ? "در حال جستجوی گوشی..." : "Searching for Phone...";
                TextDetectedPhoneIp.Text = Strings.IsPersian ? "هات‌اسپات یا وای‌فای را متصل کرده و دکمه شروع را در اپ بزنید" : "Connect to Wi-Fi / Hotspot and tap START in Android App";
                TextSignalStatus.Text = Strings.IsPersian ? "📡 در حال اسکن" : "📡 Scanning";
                BadgeSignal.Background = (SolidColorBrush)Application.Current.Resources["FillTertiary"];
            }
        });
    }


    private void OnStatsSampled(TunnelStats.Sample traffic)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TextDownSpeed.Text = $"{FormatBytes((long)traffic.DownloadRateBps)}/s";
            TextUpSpeed.Text = $"{FormatBytes((long)traffic.UploadRateBps)}/s";
            TextDownTotal.Text = $"Total: {FormatBytes(traffic.BytesDown)}";
            TextUpTotal.Text = $"Total: {FormatBytes(traffic.BytesUp)}";
            TextLatency.Text = $"{Strings.LatencyLabel}: {traffic.LatencyMs} ms";

            // Update traffic history
            _downHistory.Add(traffic.DownloadRateBps);
            if (_downHistory.Count > 30) _downHistory.RemoveAt(0);

            _upHistory.Add(traffic.UploadRateBps);
            if (_upHistory.Count > 30) _upHistory.RemoveAt(0);

            if (traffic.DownloadRateBps > _peakSpeed)
            {
                _peakSpeed = traffic.DownloadRateBps;
            }
            TextPeakSpeed.Text = $"Peak: {FormatBytes((long)_peakSpeed)}/s";

            RedrawTrafficGraph();
        });
    }

    private void RedrawTrafficGraph()
    {
        var width = CanvasTrafficGraph.ActualWidth;
        var height = CanvasTrafficGraph.ActualHeight;
        if (width <= 10 || height <= 10 || _downHistory.Count < 2) return;

        var maxVal = Math.Max(_peakSpeed, 1024 * 50); // min scale 50 KB/s
        var stepX = width / (_downHistory.Count - 1);

        var downLinePoints = new PointCollection();
        var downPolyPoints = new PointCollection();
        var upLinePoints = new PointCollection();

        downPolyPoints.Add(new Windows.Foundation.Point(0, height));

        for (int i = 0; i < _downHistory.Count; i++)
        {
            var x = i * stepX;
            var downNorm = Math.Clamp(_downHistory[i] / maxVal, 0.0, 1.0);
            var yDown = height - (downNorm * (height - 8)) - 4;

            downLinePoints.Add(new Windows.Foundation.Point(x, yDown));
            downPolyPoints.Add(new Windows.Foundation.Point(x, yDown));

            var upNorm = Math.Clamp(_upHistory[i] / maxVal, 0.0, 1.0);
            var yUp = height - (upNorm * (height - 8)) - 4;
            upLinePoints.Add(new Windows.Foundation.Point(x, yUp));
        }

        downPolyPoints.Add(new Windows.Foundation.Point(width, height));

        _polylineDownload.Points = downLinePoints;
        _polygonDownload.Points = downPolyPoints;
        _polylineUpload.Points = upLinePoints;
    }

    private void OnLogChanged()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var entries = LocalLog.Snapshot();
            TextLogsViewer.Text = string.Join(Environment.NewLine, entries.Select(e => $"[{e.ElapsedSeconds:F1}s] {e.Message}"));
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

    private void UpdateDuration()
    {
        var elapsed = DateTimeOffset.UtcNow - _connectedStart;
        TextDuration.Text = $"{Strings.DurationLabel}: {elapsed:hh\\:mm\\:ss}";
    }

    private void SelectTab(int tabIndex)
    {
        ViewTabConnect.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        ViewTabRouting.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        ViewTabLogs.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        ViewTabAbout.Visibility = tabIndex == 3 ? Visibility.Visible : Visibility.Collapsed;

        var accent = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
        var muted = (SolidColorBrush)Application.Current.Resources["LabelSecondary"];

        NavTextConnect.Foreground = tabIndex == 0 ? accent : muted;
        NavTextRouting.Foreground = tabIndex == 1 ? accent : muted;
        NavTextLogs.Foreground = tabIndex == 2 ? accent : muted;
        NavTextAbout.Foreground = tabIndex == 3 ? accent : muted;

        NavTextConnect.FontWeight = tabIndex == 0 ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
        NavTextRouting.FontWeight = tabIndex == 1 ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
        NavTextLogs.FontWeight = tabIndex == 2 ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
        NavTextAbout.FontWeight = tabIndex == 3 ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
    }

    private void NavBtnConnect_Click(object sender, RoutedEventArgs e) => SelectTab(0);
    private void NavBtnRouting_Click(object sender, RoutedEventArgs e) => SelectTab(1);
    private void NavBtnLogs_Click(object sender, RoutedEventArgs e) => SelectTab(2);
    private void NavBtnAbout_Click(object sender, RoutedEventArgs e) => SelectTab(3);

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
        var accentBrush = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
        var hairlineBrush = (SolidColorBrush)Application.Current.Resources["HairlineBrush"];
        var tertiaryFill = (SolidColorBrush)Application.Current.Resources["FillTertiary"];
        var secondaryFill = (SolidColorBrush)Application.Current.Resources["FillSecondary"];

        CardModeTun.BorderBrush = isTun ? accentBrush : hairlineBrush;
        CardModeTun.BorderThickness = new Thickness(isTun ? 2 : 1);
        CardModeTun.Background = isTun ? tertiaryFill : secondaryFill;

        CardModeProxy.BorderBrush = !isTun ? accentBrush : hairlineBrush;
        CardModeProxy.BorderThickness = new Thickness(!isTun ? 2 : 1);
        CardModeProxy.Background = !isTun ? tertiaryFill : secondaryFill;
    }

    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        var pin = InputPin.Text.Trim();
        if (pin.Length != 4)
        {
            TextPinHint.Text = Strings.IsPersian ? "⚠️ لطفاً پین ۴ رقمی کامل را وارد کنید" : "⚠️ Please enter full 4 digits";
            return;
        }

        if (_selectedDevice is null)
        {
            TextPinHint.Text = Strings.IsPersian ? "⚠️ هیچ گوشی‌ای یافت نشد. ابتدا در گوشی دکمه شروع را بزنید." : "⚠️ No phone detected yet. Tap START on Android phone.";
            return;
        }

        var host = _selectedDevice.Host;
        var port = _selectedDevice.PortNumber;
        var deviceName = _selectedDevice.DeviceName;

        TextPinHint.Text = Strings.PinHint;
        await _controller.ConnectAsync(host, port, pin, deviceName);
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
        if (_selectedDevice is not null)
        {
            await _controller.ConnectAsync(_selectedDevice.Host, _selectedDevice.PortNumber, pin, _selectedDevice.DeviceName);
        }
        else
        {
            _controller.Disconnect();
        }
    }

    private void SwitchBypassDomestic_Toggled(object sender, RoutedEventArgs e)
    {
        _controller.Routing.BypassDomestic = SwitchBypassDomestic.IsOn;
        _controller.Settings.BypassDomestic = SwitchBypassDomestic.IsOn;
        _controller.SaveCurrentSettings();
        LocalLog.Add($"Bypass Domestic Sites set to: {SwitchBypassDomestic.IsOn}");
    }

    private void SwitchStartWithWindows_Toggled(object sender, RoutedEventArgs e)
    {
        var isEnabled = SwitchStartWithWindows.IsOn;
        _controller.Settings.StartWithWindows = isEnabled;
        _controller.SaveCurrentSettings();
        StartupHelper.SetStartup(isEnabled);
        LocalLog.Add($"Start with Windows set to: {isEnabled}");
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
                FileName = "https://github.com/omid-io/AirTun",
                UseShellExecute = true,
            });
        }
        catch { }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
