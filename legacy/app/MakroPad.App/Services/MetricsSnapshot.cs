namespace MakroPad.App.Services;

/// <summary>
/// Latest sampled system metrics. <see cref="NetworkKbps"/> is total throughput across active,
/// non-loopback interfaces in kilobits per second.
/// </summary>
public sealed record MetricsSnapshot
{
    public static readonly MetricsSnapshot Default = new();

    public double CpuPercent { get; init; }

    public double MemoryPercent { get; init; }

    public double BatteryPercent { get; init; } = 100;

    public double NetworkKbps { get; init; }
}
