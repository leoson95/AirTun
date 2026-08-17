namespace AirTun.Core.Proxy;

public sealed class ProxySession(IProxyStore store, IBackupStore backup)
{
    public sealed record Result(bool Ok, string? ErrorCode = null)
    {
        public static readonly Result Success = new(true);
        public static Result Fail(string code) => new(false, code);
    }

    public static ProxySnapshot AppliedFor(string host, int port, string? bypassList = null) =>
        new(
            Enabled: true,
            Server: $"socks=socks5://{host}:{port}",
            Override: string.IsNullOrWhiteSpace(bypassList) ? "<local>" : bypassList,
            AutoConfigUrl: null
        );

    public Result Connect(string host, int port, string? bypassList = null)
    {
        var original = store.Read();
        var applied = AppliedFor(host, port, bypassList);

        backup.Save(new ProxyBackup(original, applied.Server!));
        store.Write(applied);
        store.NotifyChanged();

        if (store.Read() != applied)
        {
            store.Write(original);
            store.NotifyChanged();
            backup.Delete();
            return Result.Fail("ERR_PROXY_APPLY_FAILED");
        }
        return Result.Success;
    }

    public Result Disconnect()
    {
        var saved = backup.Load();
        if (saved is null) return Result.Success;

        store.Write(saved.Original);
        store.NotifyChanged();

        if (store.Read() != saved.Original)
        {
            return Result.Fail("ERR_ROLLBACK_INCOMPLETE");
        }
        backup.Delete();
        return Result.Success;
    }

    public bool RecoverIfCrashed()
    {
        var saved = backup.Load();
        if (saved?.Original is null || saved.AppliedServer is null)
        {
            backup.Delete();
            return false;
        }

        var current = store.Read();
        if (current.Enabled && current.Server == saved.AppliedServer)
        {
            store.Write(saved.Original);
            store.NotifyChanged();
        }
        backup.Delete();
        return true;
    }
}
