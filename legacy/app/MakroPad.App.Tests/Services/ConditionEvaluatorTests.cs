using System;
using System.Collections.Generic;
using MakroPad.App.Services;
using MakroPad.Core.Configuration;

namespace MakroPad.App.Tests.Services;

public sealed class ConditionEvaluatorTests
{
    private static SystemStateSnapshot Snapshot(
        IEnumerable<string>? running = null,
        IEnumerable<string>? media = null,
        string? foreground = null,
        bool fullscreen = false,
        double cpu = 0,
        double battery = 100) => new()
        {
            RunningProcesses = new HashSet<string>(running ?? [], StringComparer.OrdinalIgnoreCase),
            MediaActiveProcesses = new HashSet<string>(media ?? [], StringComparer.OrdinalIgnoreCase),
            ForegroundProcess = foreground,
            ForegroundIsFullscreen = fullscreen,
            CpuPercent = cpu,
            BatteryPercent = battery
        };

    [Fact]
    public void EmptyConditionsAreAlwaysEligible()
    {
        var screen = new IdleScreenConfig { Name = "Default" };
        Assert.True(ConditionEvaluator.IsEligible(screen, Snapshot()));
    }

    [Fact]
    public void ProcessRunningMatchesIgnoringExeAndCase()
    {
        var condition = new IdleScreenCondition { Type = "processRunning", Target = "Spotify.exe" };
        Assert.True(ConditionEvaluator.IsSatisfied(condition, Snapshot(running: ["spotify"])));
        Assert.False(ConditionEvaluator.IsSatisfied(condition, Snapshot(running: ["chrome"])));
    }

    [Fact]
    public void MediaActiveChecksMediaSet()
    {
        var condition = new IdleScreenCondition { Type = "mediaActive", Target = "Spotify" };
        Assert.True(ConditionEvaluator.IsSatisfied(condition, Snapshot(media: ["Spotify"])));
        Assert.False(ConditionEvaluator.IsSatisfied(condition, Snapshot(running: ["Spotify"])));
    }

    [Fact]
    public void NegateInvertsResult()
    {
        var condition = new IdleScreenCondition { Type = "processRunning", Target = "Spotify", Negate = true };
        Assert.False(ConditionEvaluator.IsSatisfied(condition, Snapshot(running: ["Spotify"])));
        Assert.True(ConditionEvaluator.IsSatisfied(condition, Snapshot()));
    }

    [Fact]
    public void ForegroundRequiresMatchingFocusedProcess()
    {
        var condition = new IdleScreenCondition { Type = "processForeground", Target = "Teams" };
        Assert.True(ConditionEvaluator.IsSatisfied(condition, Snapshot(foreground: "Teams")));
        Assert.False(ConditionEvaluator.IsSatisfied(condition, Snapshot(foreground: "Chrome")));
    }

    [Fact]
    public void FullscreenWithTargetRequiresBoth()
    {
        var condition = new IdleScreenCondition { Type = "fullscreen", Target = "Game" };
        Assert.True(ConditionEvaluator.IsSatisfied(condition, Snapshot(foreground: "Game", fullscreen: true)));
        Assert.False(ConditionEvaluator.IsSatisfied(condition, Snapshot(foreground: "Game", fullscreen: false)));
    }

    [Fact]
    public void FullscreenWithoutTargetMatchesAnyFullscreen()
    {
        var condition = new IdleScreenCondition { Type = "fullscreen" };
        Assert.True(ConditionEvaluator.IsSatisfied(condition, Snapshot(foreground: "Anything", fullscreen: true)));
        Assert.False(ConditionEvaluator.IsSatisfied(condition, Snapshot(fullscreen: false)));
    }

    [Fact]
    public void SystemMetricComparesValue()
    {
        var cpuHigh = new IdleScreenCondition
        {
            Type = "systemMetric", Metric = "cpu", Comparison = "gt", Threshold = 80
        };
        Assert.True(ConditionEvaluator.IsSatisfied(cpuHigh, Snapshot(cpu: 95)));
        Assert.False(ConditionEvaluator.IsSatisfied(cpuHigh, Snapshot(cpu: 50)));

        var batteryLow = new IdleScreenCondition
        {
            Type = "systemMetric", Metric = "battery", Comparison = "lte", Threshold = 20
        };
        Assert.True(ConditionEvaluator.IsSatisfied(batteryLow, Snapshot(battery: 15)));
        Assert.False(ConditionEvaluator.IsSatisfied(batteryLow, Snapshot(battery: 80)));
    }

    [Fact]
    public void AndLogicRequiresAllConditions()
    {
        var screen = new IdleScreenConfig
        {
            ConditionLogic = "and",
            Conditions =
            [
                new IdleScreenCondition { Type = "processForeground", Target = "Spotify" },
                new IdleScreenCondition { Type = "mediaActive", Target = "Spotify" }
            ]
        };

        Assert.True(ConditionEvaluator.IsEligible(screen, Snapshot(foreground: "Spotify", media: ["Spotify"])));
        Assert.False(ConditionEvaluator.IsEligible(screen, Snapshot(foreground: "Spotify")));
    }

    [Fact]
    public void OrLogicRequiresAnyCondition()
    {
        var screen = new IdleScreenConfig
        {
            ConditionLogic = "or",
            Conditions =
            [
                new IdleScreenCondition { Type = "processRunning", Target = "Spotify" },
                new IdleScreenCondition { Type = "processRunning", Target = "Chrome" }
            ]
        };

        Assert.True(ConditionEvaluator.IsEligible(screen, Snapshot(running: ["Chrome"])));
        Assert.False(ConditionEvaluator.IsEligible(screen, Snapshot(running: ["Edge"])));
    }
}
