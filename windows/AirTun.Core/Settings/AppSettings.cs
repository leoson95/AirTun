using System.Text.Json;
using AirTun.Core.Routing;

namespace AirTun.Core.Settings;

public sealed class AppSettings
{
    public bool BypassDomestic { get; set; } = false;
    public bool BypassLan { get; set; } = true;
    public bool CloseToTray { get; set; } = false;
    public bool MinimizeToTray { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public string DnsProvider { get; set; } = "1.1.1.1";
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "dark";
    public List<RoutingRule> CustomRules { get; set; } = [];

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AirTun",
        "settings.json"
    );

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
