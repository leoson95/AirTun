using System.Diagnostics;

namespace AirTun.Launcher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var appDir = Path.Combine(AppContext.BaseDirectory, "app");
        var exePath = Path.Combine(appDir, "AirTun.App.exe");

        if (!File.Exists(exePath))
        {
            exePath = Path.Combine(AppContext.BaseDirectory, "AirTun.App.exe");
            appDir = AppContext.BaseDirectory;
        }

        if (!File.Exists(exePath))
        {
            return 1;
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = appDir,
            UseShellExecute = true,
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        try
        {
            Process.Start(psi);
            return 0;
        }
        catch
        {
            return 1;
        }
    }
}
