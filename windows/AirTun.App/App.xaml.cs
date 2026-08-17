using Microsoft.UI.Xaml;

namespace AirTun.App;

public partial class App : Application
{
    private MainWindow? _mainWindow;

    public App()
    {
        this.UnhandledException += (s, e) =>
        {
            File.AppendAllText(@"C:\Tools\airtun\crash.log", $"{DateTime.Now} [App.UnhandledException]: {e.Message} \n {e.Exception}\n");
            e.Handled = true;
        };

        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _mainWindow = new MainWindow();
            _mainWindow.Activate();
        }
        catch (Exception ex)
        {
            File.AppendAllText(@"C:\Tools\airtun\crash.log", $"{DateTime.Now} [OnLaunched Exception]: {ex}\n");
        }
    }
}
