using AirTun.Core;
using Xunit;

namespace AirTun.App.Tests;

public class ConnectionRulesTests
{
    [Fact]
    public void IdleCanStartAndDiscover()
    {
        Assert.True(ConnectionRules.CanTransition("Idle", "start"));
        Assert.Equal("Preparing", ConnectionRules.Target("Idle", "start"));

        Assert.True(ConnectionRules.CanTransition("Idle", "discover"));
        Assert.Equal("Discovering", ConnectionRules.Target("Idle", "discover"));
    }

    [Fact]
    public void PreparingTransitions()
    {
        Assert.True(ConnectionRules.CanTransition("Preparing", "ready"));
        Assert.Equal("Connected", ConnectionRules.Target("Preparing", "ready"));

        Assert.True(ConnectionRules.CanTransition("Preparing", "failure"));
        Assert.Equal("Error", ConnectionRules.Target("Preparing", "failure"));

        Assert.True(ConnectionRules.CanTransition("Preparing", "stop"));
        Assert.Equal("Idle", ConnectionRules.Target("Preparing", "stop"));
    }

    [Fact]
    public void ConnectedTransitions()
    {
        Assert.True(ConnectionRules.CanTransition("Connected", "statsUpdated"));
        Assert.Equal("Connected", ConnectionRules.Target("Connected", "statsUpdated"));

        Assert.True(ConnectionRules.CanTransition("Connected", "stop"));
        Assert.Equal("Idle", ConnectionRules.Target("Connected", "stop"));
    }

    [Fact]
    public void InvalidTransitionsRejected()
    {
        Assert.False(ConnectionRules.CanTransition("Idle", "ready"));
        Assert.False(ConnectionRules.CanTransition("Connected", "discover"));
    }
}
