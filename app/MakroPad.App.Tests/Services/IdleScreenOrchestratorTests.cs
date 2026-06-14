using System;
using System.Collections.Generic;
using MakroPad.App.Services;
using MakroPad.Core.Configuration;

namespace MakroPad.App.Tests.Services;

public sealed class IdleScreenOrchestratorTests
{
    private sealed class FakeProbe(params string[] running) : IProcessPresenceProbe
    {
        private readonly HashSet<string> running = new(running, StringComparer.OrdinalIgnoreCase);

        public bool IsRunning(string processName) => running.Contains(processName);
    }

    private static IdleScreenConfig Default(string name) =>
        new() { Name = name, Activation = "default", Template = name };

    private static IdleScreenConfig ForProcess(string name, string process) =>
        new() { Name = name, Activation = "process", ProcessName = process, Template = name };

    [Fact]
    public void ShowsDefaultWhenNoProcessMatches()
    {
        var orchestrator = new IdleScreenOrchestrator(new FakeProbe());
        orchestrator.Configure([Default("Standard"), ForProcess("Spotify", "Spotify")], 8);

        Assert.Equal("Standard", orchestrator.Evaluate(DateTime.UtcNow)?.Name);
    }

    [Fact]
    public void ProcessScreenBeatsDefault()
    {
        var orchestrator = new IdleScreenOrchestrator(new FakeProbe("Spotify"));
        orchestrator.Configure([Default("Standard"), ForProcess("Spotify", "Spotify")], 8);

        Assert.Equal("Spotify", orchestrator.Evaluate(DateTime.UtcNow)?.Name);
    }

    [Fact]
    public void RotatesAcrossActiveSetAfterInterval()
    {
        var orchestrator = new IdleScreenOrchestrator(new FakeProbe());
        orchestrator.Configure([Default("A"), Default("B")], rotationSeconds: 5);

        var start = DateTime.UtcNow;
        Assert.Equal("A", orchestrator.Evaluate(start)?.Name);
        Assert.Equal("A", orchestrator.Evaluate(start.AddSeconds(3))?.Name);
        Assert.Equal("B", orchestrator.Evaluate(start.AddSeconds(6))?.Name);
        Assert.Equal("A", orchestrator.Evaluate(start.AddSeconds(12))?.Name);
    }

    [Fact]
    public void FallsBackToEnabledWhenNoDefaultAndNoProcess()
    {
        var orchestrator = new IdleScreenOrchestrator(new FakeProbe());
        orchestrator.Configure([ForProcess("Only", "NotRunning")], 8);

        Assert.Equal("Only", orchestrator.Evaluate(DateTime.UtcNow)?.Name);
    }
}
