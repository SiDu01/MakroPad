using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using MakroPad.Core.Configuration;

namespace MakroPad.App.Services;

/// <summary>
/// Picks the active idle screen from the configured list and pushes it when it changes.
///
/// Selection precedence, evaluated every second against a <see cref="SystemStateSnapshot"/>:
/// <list type="number">
/// <item>A screen is <i>eligible</i> when it is enabled and its conditions hold (empty conditions =
/// always eligible). The conditions must hold for <c>ActivationDelaySeconds</c> before counting.</item>
/// <item>Anti-flicker: the currently shown screen is held until its <c>MinDwellSeconds</c> elapse,
/// even if its conditions dropped — unless a higher-priority exclusive screen preempts it.</item>
/// <item>If any eligible screen is <c>Exclusive</c>, the highest-priority one is shown alone.</item>
/// <item>Otherwise the eligible screens at the highest <c>Priority</c> form the active set and
/// rotate every <c>IdleRotationSeconds</c>.</item>
/// </list>
/// </summary>
public sealed class IdleScreenOrchestrator : IDisposable
{
    private readonly ISystemStateProvider stateProvider;
    private List<IdleScreenConfig> screens = [];
    private DateTime?[] eligibleSince = [];
    private int rotationSeconds = 8;
    private int rotationIndex;
    private DateTime? lastRotationAt;
    private IdleScreenConfig? current;
    private DateTime? currentShownSince;
    private DispatcherTimer? timer;
    private Func<IdleScreenConfig, Task>? onActiveChanged;
    private bool disposed;

    public IdleScreenOrchestrator(ISystemStateProvider stateProvider)
    {
        this.stateProvider = stateProvider;
    }

    public IdleScreenConfig? Current => current;

    public void Configure(IEnumerable<IdleScreenConfig> screens, int rotationSeconds)
    {
        this.screens = screens?.ToList() ?? [];
        eligibleSince = new DateTime?[this.screens.Count];
        this.rotationSeconds = Math.Max(1, rotationSeconds);
        rotationIndex = 0;
        lastRotationAt = null;
        current = null;
        currentShownSince = null;
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
    /// <paramref name="now"/>, advancing internal rotation/dwell state as needed.
    /// </summary>
    public IdleScreenConfig? Evaluate(DateTime now)
    {
        var snapshot = stateProvider.Capture();
        var eligible = ComputeEligible(now, snapshot);

        // Anti-flicker: hold the current screen until its min-dwell elapses, unless a higher-priority
        // exclusive screen is eligible and may preempt it.
        if (current is not null)
        {
            var dwellElapsed = currentShownSince is null
                || (now - currentShownSince.Value).TotalSeconds >= current.MinDwellSeconds;
            if (!dwellElapsed)
            {
                var preemptor = eligible
                    .Where(s => s.Exclusive && s.Priority > current.Priority)
                    .OrderByDescending(s => s.Priority)
                    .FirstOrDefault();
                return preemptor is null ? current : Switch(preemptor, now);
            }
        }

        var target = ComputeTarget(now, eligible);
        return target is null ? current : Switch(target, now);
    }

    private List<IdleScreenConfig> ComputeEligible(DateTime now, SystemStateSnapshot snapshot)
    {
        var eligible = new List<IdleScreenConfig>();
        for (var i = 0; i < screens.Count; i++)
        {
            var screen = screens[i];
            var raw = screen.Enabled && ConditionEvaluator.IsEligible(screen, snapshot);
            if (!raw)
            {
                eligibleSince[i] = null;
                continue;
            }

            eligibleSince[i] ??= now;
            if ((now - eligibleSince[i]!.Value).TotalSeconds >= screen.ActivationDelaySeconds)
            {
                eligible.Add(screen);
            }
        }

        return eligible;
    }

    private IdleScreenConfig? ComputeTarget(DateTime now, List<IdleScreenConfig> eligible)
    {
        if (eligible.Count == 0)
        {
            return null;
        }

        var exclusive = eligible
            .Where(s => s.Exclusive)
            .OrderByDescending(s => s.Priority)
            .FirstOrDefault();
        if (exclusive is not null)
        {
            lastRotationAt = now;
            rotationIndex = 0;
            return exclusive;
        }

        var maxPriority = eligible.Max(s => s.Priority);
        var active = eligible.Where(s => s.Priority == maxPriority).ToList();

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

    private IdleScreenConfig Switch(IdleScreenConfig screen, DateTime now)
    {
        if (!ReferenceEquals(screen, current))
        {
            current = screen;
            currentShownSince = now;
        }

        return screen;
    }

    private void OnTimerTick(object? sender, EventArgs e) => Tick();

    private void Tick()
    {
        var previous = current;
        var next = Evaluate(DateTime.UtcNow);
        if (next is null || ReferenceEquals(next, previous))
        {
            return;
        }

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
