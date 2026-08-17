namespace AirTun.Core.Proxy;

public interface IProxyStore
{
    ProxySnapshot Read();
    void Write(ProxySnapshot snapshot);
    void NotifyChanged();
}

public interface IBackupStore
{
    void Save(ProxyBackup backup);
    ProxyBackup? Load();
    void Delete();
}
