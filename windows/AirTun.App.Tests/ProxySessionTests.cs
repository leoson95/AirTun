using AirTun.Core.Proxy;
using Xunit;

namespace AirTun.App.Tests;

public class ProxySessionTests
{
    private sealed class FakeProxyStore : IProxyStore
    {
        public ProxySnapshot Current = new(false, null, null, null);
        public int NotifyCount;

        public ProxySnapshot Read() => Current;
        public void Write(ProxySnapshot snapshot) => Current = snapshot;
        public void NotifyChanged() => NotifyCount++;
    }

    private sealed class FakeBackupStore : IBackupStore
    {
        public ProxyBackup? Saved;

        public void Save(ProxyBackup backup) => Saved = backup;
        public ProxyBackup? Load() => Saved;
        public void Delete() => Saved = null;
    }

    [Fact]
    public void ConnectSetsSystemProxyAndSavesBackup()
    {
        var store = new FakeProxyStore();
        var backup = new FakeBackupStore();
        var session = new ProxySession(store, backup);

        var result = session.Connect("192.168.43.1", 10808);

        Assert.True(result.Ok);
        Assert.True(store.Current.Enabled);
        Assert.Equal("socks=socks5://192.168.43.1:10808", store.Current.Server);
        Assert.NotNull(backup.Saved);
    }

    [Fact]
    public void DisconnectRestoresOriginalProxySettings()
    {
        var store = new FakeProxyStore();
        var backup = new FakeBackupStore();
        var session = new ProxySession(store, backup);

        session.Connect("192.168.43.1", 10808);
        var result = session.Disconnect();

        Assert.True(result.Ok);
        Assert.False(store.Current.Enabled);
        Assert.Null(store.Current.Server);
        Assert.Null(backup.Saved);
    }

    [Fact]
    public void RecoverIfCrashedRestoresOnlyIfStillApplied()
    {
        var store = new FakeProxyStore();
        var backup = new FakeBackupStore();
        var session = new ProxySession(store, backup);

        session.Connect("192.168.43.1", 10808);
        var newSession = new ProxySession(store, backup);
        var recovered = newSession.RecoverIfCrashed();

        Assert.True(recovered);
        Assert.False(store.Current.Enabled);
        Assert.Null(backup.Saved);
    }
}
