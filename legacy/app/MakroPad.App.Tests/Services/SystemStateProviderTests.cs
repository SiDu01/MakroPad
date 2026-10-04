using System;
using System.Diagnostics;
using MakroPad.App.Services;

namespace MakroPad.App.Tests.Services;

public sealed class SystemStateProviderTests
{
    private sealed class FakeForegroundProbe(ForegroundWindowInfo info) : IForegroundWindowProbe
    {
        public ForegroundWindowInfo Capture() => info;
    }

    private sealed class FakeMetricsSampler(MetricsSnapshot snapshot) : ISystemMetricsSampler
    {
        public MetricsSnapshot Current { get; } = snapshot;

        public void Sample() { }

        public System.Threading.Tasks.Task RunSampleLoopAsync(System.Threading.CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.CompletedTask;
    }

    [Fact]
    public void CaptureIncludesForegroundInfo()
    {
        var provider = new SystemStateProvider(new FakeForegroundProbe(new ForegroundWindowInfo("Teams", true)));

        var snapshot = provider.Capture();

        Assert.Equal("Teams", snapshot.ForegroundProcess);
        Assert.True(snapshot.ForegroundIsFullscreen);
    }

    [Fact]
    public void CaptureIncludesRunningProcesses()
    {
        var provider = new SystemStateProvider(new FakeForegroundProbe(default));

        var snapshot = provider.Capture();

        Assert.Contains(Process.GetCurrentProcess().ProcessName, snapshot.RunningProcesses);
    }

    [Fact]
    public void SetMediaActiveProcessesIsReflectedInSnapshot()
    {
        var provider = new SystemStateProvider(new FakeForegroundProbe(default));

        provider.SetMediaActiveProcesses(["Spotify", "  ", "Chrome"]);
        var snapshot = provider.Capture();

        Assert.Contains("Spotify", snapshot.MediaActiveProcesses);
        Assert.Contains("Chrome", snapshot.MediaActiveProcesses);
        Assert.DoesNotContain("  ", snapshot.MediaActiveProcesses);
    }

    [Fact]
    public void CaptureIncludesMetricsFromSampler()
    {
        var metrics = new MetricsSnapshot
        {
            CpuPercent = 73,
            MemoryPercent = 64,
            BatteryPercent = 18,
            NetworkKbps = 1234
        };
        var provider = new SystemStateProvider(new FakeForegroundProbe(default), new FakeMetricsSampler(metrics));

        var snapshot = provider.Capture();

        Assert.Equal(73, snapshot.CpuPercent);
        Assert.Equal(64, snapshot.MemoryPercent);
        Assert.Equal(18, snapshot.BatteryPercent);
        Assert.Equal(1234, snapshot.NetworkKbps);
    }

    [Fact]
    public void CaptureWithoutSamplerUsesDefaultMetrics()
    {
        var provider = new SystemStateProvider(new FakeForegroundProbe(default));

        var snapshot = provider.Capture();

        Assert.Equal(0, snapshot.CpuPercent);
        Assert.Equal(100, snapshot.BatteryPercent);
    }

    [Fact]
    public void WindowsForegroundProbeDoesNotThrow()
    {
        // Environment-dependent result; we only assert it is callable and never throws.
        var info = new WindowsForegroundWindowProbe().Capture();

        if (!OperatingSystem.IsWindows())
        {
            Assert.Null(info.ProcessName);
            Assert.False(info.IsFullscreen);
        }
    }
}
