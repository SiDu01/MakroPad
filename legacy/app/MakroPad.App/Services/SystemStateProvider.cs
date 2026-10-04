using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MakroPad.App.Services;

/// <summary>
/// Phase 1 system-state provider. Captures running process names on demand and keeps a cached set
/// of process names with active media output (fed from the audio-session refresh loop). Foreground,
/// fullscreen and metric signals are added in later phases.
/// </summary>
public sealed class SystemStateProvider : ISystemStateProvider
{
    private readonly IForegroundWindowProbe foregroundWindowProbe;
    private readonly ISystemMetricsSampler? metricsSampler;
    private volatile IReadOnlySet<string> mediaActiveProcesses = SystemStateSnapshot.NoProcesses;

    public SystemStateProvider(
        IForegroundWindowProbe? foregroundWindowProbe = null,
        ISystemMetricsSampler? metricsSampler = null)
    {
        this.foregroundWindowProbe = foregroundWindowProbe ?? new WindowsForegroundWindowProbe();
        this.metricsSampler = metricsSampler;
    }

    /// <summary>Updates the cached set of process names that currently emit audio.</summary>
    public void SetMediaActiveProcesses(IEnumerable<string> processNames)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in processNames)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                set.Add(name.Trim());
            }
        }

        mediaActiveProcesses = set;
    }

    public SystemStateSnapshot Capture()
    {
        var foreground = foregroundWindowProbe.Capture();
        var metrics = metricsSampler?.Current ?? MetricsSnapshot.Default;
        return new SystemStateSnapshot
        {
            RunningProcesses = CaptureRunningProcesses(),
            MediaActiveProcesses = mediaActiveProcesses,
            ForegroundProcess = foreground.ProcessName,
            ForegroundIsFullscreen = foreground.IsFullscreen,
            CpuPercent = metrics.CpuPercent,
            MemoryPercent = metrics.MemoryPercent,
            BatteryPercent = metrics.BatteryPercent,
            NetworkKbps = metrics.NetworkKbps
        };
    }

    private static IReadOnlySet<string> CaptureRunningProcesses()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    set.Add(process.ProcessName);
                }
                catch
                {
                    // Process exited between enumeration and access — ignore.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
            return SystemStateSnapshot.NoProcesses;
        }

        return set;
    }
}
