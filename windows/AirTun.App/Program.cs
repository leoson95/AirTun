using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace AirTun.App;

public static class Program
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string lpPathName);

    [STAThread]
    private static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                File.AppendAllText(@"C:\Tools\airtun\crash.log", $"{DateTime.Now} [AppDomain.UnhandledException]: {e.ExceptionObject}\n");
            }
            catch { }
        };

        try
        {
            SetDllDirectory(AppContext.BaseDirectory);

            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(@"C:\Tools\airtun\crash.log", $"{DateTime.Now} [Main Exception]: {ex}\n");
            }
            catch { }
        }
    }
}
