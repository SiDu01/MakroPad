using System;
using System.Collections.Generic;
using MakroPad.App.Services;
using MakroPad.Core.Configuration;

namespace MakroPad.App.Tests.Services;

public sealed class IdleScreenOrchestratorTests
{
    private sealed class FakeStateProvider : ISystemStateProvider
    {
        public SystemStateSnapshot Snapshot { get; set; } = new();

        public SystemStateSnapshot Capture() => Snapshot;
    }

    private static SystemStateSnapshot Snapshot(
        IEnumerable<string>? running = null,
        IEnumerable<string>? media = null) => new()
        {
            RunningProcesses = new HashSet<string>(running ?? [], StringComparer.OrdinalIgnoreCase),
            MediaActiveProcesses = new HashSet<string>(media ?? [], StringComparer.OrdinalIgnoreCase)
        };

    private static IdleScreenConfig Default(string name) =>
        new() { Name = name, Template = name };

    private static IdleScreenConfig ForProcess(string name, string process, int priority = 10) =>
        new()
        {
            Name = name,
            Template = name,
            Priority = priority,
            Conditions = [new IdleScreenCondition { Type = "processRunning", Target = process }]
        };

    private static IdleScreenConfig ForMedia(string name, string process, int priority = 20) =>
        new()
        {
            Name = name,
            Template = name,
            Priority = priority,
            Conditions = [new IdleScreenCondition { Type = "mediaActive", Target = process }]
        };

    [Fact]
    public void ShowsDefaultWhenNoProcessMatches()
    {
        var provider = new FakeStateProvider { Snapshot = Snapshot() };
        var orchestrator = new IdleScreenOrchestrator(provider);
        orchestrator.Configure([Default("Standard"), ForProcess("Spotify", "Spotify")], 8);

        Assert.Equal("Standard", orchestrator.Evaluate(DateTime.UtcNow)?.Name);
    }

    [Fact]
    public void ProcessScreenBeatsDefaultByPriority()
    {
        var provider = new FakeStateProvider { Snapshot = Snapshot(running: ["Spotify"]) };
        var orchestrator = new IdleScreenOrchestrator(provider);
        orchestrator.Configure([Default("Standard"), ForProcess("Spotify", "Spotify")], 8);

        Assert.Equal("Spotify", orchestrator.Evaluate(DateTime.UtcNow)?.Name);
    }

    [Fact]
    public void MediaScreenShowsOnlyWhilePlayingElseDefault()
    {
        var provider = new FakeStateProvider();
        var orchestrator = new IdleScreenOrchestrator(provider);
        orchestrator.Configure([Default("Standard"), ForMedia("SpotifyNP", "Spotify")], 8);

        // App open but nothing playing -> default.
        provider.Snapshot = Snapshot(running: ["Spotify"]);
        Assert.Equal("Standard", orchestrator.Evaluate(DateTime.UtcNow)?.Name);

        // Song playing -> media screen.
        provider.Snapshot = Snapshot(running: ["Spotify"], media: ["Spotify"]);
        Assert.Equal("SpotifyNP", orchestrator.Evaluate(DateTime.UtcNow)?.Name);
    }

    [Fact]
    public void RotatesAcrossActiveSetAfterInterval()
    {
        var provider = new FakeStateProvider { Snapshot = Snapshot() };
        var orchestrator = new IdleScreenOrchestrator(provider);
        orchestrator.Configure([Default("A"), Default("B")], rotationSeconds: 5);

        var start = DateTime.UtcNow;
        Assert.Equal("A", orchestrator.Evaluate(start)?.Name);
        Assert.Equal("A", orchestrator.Evaluate(start.AddSeconds(3))?.Name);
        Assert.Equal("B", orchestrator.Evaluate(start.AddSeconds(6))?.Name);
        Assert.Equal("A", orchestrator.Evaluate(start.AddSeconds(12))?.Name);
    }

    [Fact]
    public void NothingEligibleKeepsCurrentNull()
    {
        var provider = new FakeStateProvider { Snapshot = Snapshot() };
        var orchestrator = new IdleScreenOrchestrator(provider);
        orchestrator.Configure([ForProcess("Only", "NotRunning")], 8);

        Assert.Null(orchestrator.Evaluate(DateTime.UtcNow));
    }

    [Fact]
    public void ActivationDelayDefersEligibility()
    {
        var provider = new FakeStateProvider { Snapshot = Snapshot(media: ["Spotify"]) };
        var orchestrator = new IdleScreenOrchestrator(provider);
        var media = ForMedia("SpotifyNP", "Spotify") with { ActivationDelaySeconds = 2 };
        orchestrator.Configure([Default("Standard"), media], 8);

        var start = DateTime.UtcNow;
        Assert.Equal("Standard", orchestrator.Evaluate(start)?.Name);
        Assert.Equal("Standard", orchestrator.Evaluate(start.AddSeconds(1))?.Name);
        Assert.Equal("SpotifyNP", orchestrator.Evaluate(start.AddSeconds(2))?.Name);
    }

    [Fact]
    public void MinDwellHoldsScreenThroughBriefDrop()
    {
        var provider = new FakeStateProvider();
        var orchestrator = new IdleScreenOrchestrator(provider);
        var media = ForMedia("SpotifyNP", "Spotify") with { MinDwellSeconds = 5 };
        orchestrator.Configure([Default("Standard"), media], 8);

        var start = DateTime.UtcNow;
        provider.Snapshot = Snapshot(media: ["Spotify"]);
        Assert.Equal("SpotifyNP", orchestrator.Evaluate(start)?.Name);

        // Track changes -> media briefly inactive; dwell keeps the media screen.
        provider.Snapshot = Snapshot(running: ["Spotify"]);
        Assert.Equal("SpotifyNP", orchestrator.Evaluate(start.AddSeconds(2))?.Name);

        // Still nothing playing after the dwell window -> fall back to default.
        Assert.Equal("Standard", orchestrator.Evaluate(start.AddSeconds(6))?.Name);
    }

    [Fact]
    public void ExclusiveScreenPreemptsDwellOfCurrent()
    {
        var provider = new FakeStateProvider { Snapshot = Snapshot() };
        var orchestrator = new IdleScreenOrchestrator(provider);
        var standard = Default("Standard") with { MinDwellSeconds = 30 };
        var alarm = new IdleScreenConfig
        {
            Name = "Alarm",
            Template = "Alarm",
            Priority = 100,
            Exclusive = true,
            Conditions = [new IdleScreenCondition { Type = "processRunning", Target = "Alert" }]
        };
        orchestrator.Configure([standard, alarm], 8);

        var start = DateTime.UtcNow;
        Assert.Equal("Standard", orchestrator.Evaluate(start)?.Name);

        // Alarm condition fires while Standard is still within its long dwell -> preempts immediately.
        provider.Snapshot = Snapshot(running: ["Alert"]);
        Assert.Equal("Alarm", orchestrator.Evaluate(start.AddSeconds(2))?.Name);
    }

    [Fact]
    public void ExclusiveEligibleIsShownAlone()
    {
        var provider = new FakeStateProvider { Snapshot = Snapshot(running: ["Game"]) };
        var orchestrator = new IdleScreenOrchestrator(provider);
        var game = new IdleScreenConfig
        {
            Name = "Game",
            Template = "Game",
            Priority = 5,
            Exclusive = true,
            Conditions = [new IdleScreenCondition { Type = "processRunning", Target = "Game" }]
        };
        orchestrator.Configure([Default("Standard"), game], 8);

        // Default is eligible too, but the exclusive screen suppresses it.
        Assert.Equal("Game", orchestrator.Evaluate(DateTime.UtcNow)?.Name);
    }
}
