using System;
using MakroPad.App.Services;

namespace MakroPad.App.Tests.Services;

public sealed class SystemMetricsSamplerTests
{
    [Fact]
    public void StartsWithDefaultSnapshot()
    {
        var sampler = new SystemMetricsSampler();

        Assert.Equal(100, sampler.Current.BatteryPercent);
        Assert.Equal(0, sampler.Current.CpuPercent);
    }

    [Fact]
    public void SamplingTwiceProducesPlausibleRanges()
    {
        var sampler = new SystemMetricsSampler();

        sampler.Sample();
        sampler.Sample();
        var snapshot = sampler.Current;

        Assert.InRange(snapshot.CpuPercent, 0, 100);
        Assert.InRange(snapshot.MemoryPercent, 0, 100);
        Assert.InRange(snapshot.BatteryPercent, 0, 100);
        Assert.True(snapshot.NetworkKbps >= 0);
    }
}
