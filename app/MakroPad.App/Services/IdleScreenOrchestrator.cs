using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using MakroPad.Core.Configuration;

namespace MakroPad.App.Services;

/// <summary>
/// Picks the active idle screen from the configured list and pushes it when it changes.
/// Priority: if any "process" screen's process is running, only those are active; otherwise the
/// "default" screens are active; otherwise any enabled screen. When more than one screen is
/// active they rotate every <c>IdleRotationSeconds</c>.
/// </summary>
public sealed class IdleScreenOrchestrator : IDisposable
{
    private readonly IProcessPresenceProbe processProbe;
    private List<IdleScreenConfig> screens = [];
    private int rotationSeconds = 8;
    private int rotationIndex;
    private DateTime? lastRotationAt;
    private IdleScreenConfig? current;
    private DispatcherTimer? timer;
    private Func<IdleScreenConfig, Task>? onActiveChanged;
    private bool disposed;

    public IdleScreenOrchestrator(IProcessPresenceProbe processProbe)
    {
        this.processProbe = processProbe;
    }

    public IdleScreenConfig? Current => current;

    public void Configure(IEnumerable<IdleScreenConfig> screens, int rotationSeconds)
    {
        this.screens = screens?.ToList() ?? [];
        this.rotationSeconds = Math.Max(1, rotationSeconds);
        rotationIndex = 0;
        lastRotationAt = null;
        current = null;
    }

    public void Start(Func<IdleScreenConfig, Task> onActiveChanged)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        this.onActiveChanged = onActiveChanged;
        if (timer is null)
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += OnTimerTick;
            timer.Start();
        }

        Tick();
    }

    /// <summary>Forces an immediate re-evaluation (e.g. after connect or config change).</summary>
    public void Refresh() => Tick();

    /// <summary>
    /// Pure selection used by the timer and by tests: returns the screen that should be shown at
    /// <paramref name="now"/>, advancing the rotation index when the interval elapsed.
    /// </summary>
    public IdleScreenConfig? Evaluate(DateTime now)
    {
        var active = ActiveSet();
        if (active.Count == 0)
        {
            return null;
        }

        if (active.Count == 1)
        {
            lastRotationAt = now;
            rotationIndex = 0;
            return active[0];
        }

        lastRotationAt ??= now;
        if ((now - lastRotationAt.Value).TotalSeconds >= rotationSeconds)
        {
            rotationIndex++;
            lastRotationAt = now;
        }

        return active[rotationIndex % active.Count];
    }

    private IReadOnlyList<IdleScreenConfig> ActiveSet()
    {
        var enabled = screens.Where(s => s.Enabled).ToList();

        var processActive = enabled
            .Where(s => string.Equals(s.Activation, "process", StringComparison.OrdinalIgnoreCase)
                        && processProbe.IsRunning(s.ProcessName))
            .ToList();
        if (processActive.Count > 0)
        {
            return processActive;
        }

        var defaults = enabled
            .Where(s => !string.Equals(s.Activation, "process", StringComparison.OrdinalIgnoreCase))
            .ToList();
        return defaults.Count > 0 ? defaults : enabled;
    }

    private void OnTimerTick(object? sender, EventArgs e) => Tick();

    private void Tick()
    {
        var next = Evaluate(DateTime.UtcNow);
        if (next is null || Equals(next, current))
        {
            return;
        }

        current = next;
        _ = onActiveChanged?.Invoke(next);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (timer is not null)
        {
            timer.Stop();
            timer.Tick -= OnTimerTick;
            timer = null;
        }
    }
}
