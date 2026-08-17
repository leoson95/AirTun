using System.Text.Json;
using AirTun.Core.Proxy;

namespace AirTun.App.Services;

public sealed class FileBackupStore : IBackupStore
{
    private static readonly string BackupPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AirTun",
        "proxy-backup.json"
    );

    public void Save(ProxyBackup backup)
    {
        try
        {
            var dir = Path.GetDirectoryName(BackupPath);
            if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(BackupPath, JsonSerializer.Serialize(backup));
        }
        catch { }
    }

    public ProxyBackup? Load()
    {
        try
        {
            if (!File.Exists(BackupPath)) return null;
            return JsonSerializer.Deserialize<ProxyBackup>(File.ReadAllText(BackupPath));
        }
        catch
        {
            return null;
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(BackupPath)) File.Delete(BackupPath);
        }
        catch { }
    }
}
