using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace AirTun.App;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
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
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AirTun", "crash.log");
            try
            {
                var dir = Path.GetDirectoryName(logPath);
                if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(logPath, $"{DateTime.Now}: {ex}");
            }
            catch { }
        }
    }
}
