using System;
using System.Collections.Generic;

namespace MakroPad.App.Services;

/// <summary>
/// A point-in-time view of the system signals idle-screen conditions are evaluated against.
/// Captured by an <see cref="ISystemStateProvider"/> so the (pure) condition evaluation stays
/// testable. Phase 1 fills <see cref="RunningProcesses"/> and <see cref="MediaActiveProcesses"/>;
/// the foreground/fullscreen/metric fields are placeholders for later phases.
/// </summary>
public sealed record SystemStateSnapshot
{
    public static readonly IReadOnlySet<string> NoProcesses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Process names (without .exe) currently running.</summary>
    public IReadOnlySet<string> RunningProcesses { get; init; } = NoProcesses;

    /// <summary>Process names (without .exe) that currently have an active audio session.</summary>
    public IReadOnlySet<string> MediaActiveProcesses { get; init; } = NoProcesses;

    /// <summary>Process name (without .exe) of the foreground window, or null.</summary>
    public string? ForegroundProcess { get; init; }

    /// <summary>Whether the foreground window covers its whole monitor.</summary>
    public bool ForegroundIsFullscreen { get; init; }

    public double CpuPercent { get; init; }

    public double MemoryPercent { get; init; }

    public double BatteryPercent { get; init; } = 100;

    public double NetworkKbps { get; init; }
}
