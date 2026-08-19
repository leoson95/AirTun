using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Xaml;

namespace AirTun.App;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private MainWindow? _mainWindow;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    private static void LogCrash(string message)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AirTun");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "crash.log"), $"{DateTime.Now} {message}\n");
        }
        catch { }
    }

    public App()
    {
        this.UnhandledException += (s, e) =>
        {
            LogCrash($"[App.UnhandledException]: {e.Message} \n {e.Exception}");
            e.Handled = true;
        };

        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Single-instance guard via Global Mutex
        _singleInstanceMutex = new Mutex(initiallyOwned: true, "Global\\AirTun_SingleInstance_Mutex_v1", out bool createdNew);

        if (!createdNew)
        {
            // Another instance is already running — bring it to front and exit
            var hWnd = FindWindow(null, "AirTun");
            if (hWnd != IntPtr.Zero)
            {
                if (IsIconic(hWnd))
                    ShowWindow(hWnd, SW_RESTORE);
                else
                    ShowWindow(hWnd, SW_SHOW);
                SetForegroundWindow(hWnd);
            }
            Environment.Exit(0);
            return;
        }

        try
        {
            _mainWindow = new MainWindow();
            _mainWindow.Activate();
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
            ShowWindow(hWnd, SW_SHOW);
            SetForegroundWindow(hWnd);
        }
        catch (Exception ex)
        {
            LogCrash($"[OnLaunched Exception]: {ex}");
        }
    }
}

