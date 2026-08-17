using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AirTun.Launcher;

internal static class Program
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string lpPathName);

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

        // Set DLL search path
        SetDllDirectory(appDir);

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = appDir,
            UseShellExecute = false,
        };

        var existingPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        psi.EnvironmentVariables["PATH"] = $"{appDir};{existingPath}";

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
