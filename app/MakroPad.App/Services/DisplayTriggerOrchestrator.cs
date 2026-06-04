using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MakroPad.Core.Configuration;
using MakroPad.Core.DisplayTriggers;

namespace MakroPad.App.Services;

public sealed class DisplayTriggerOrchestrator : IDisposable
{
    private readonly IDisplayTriggerSink sink;
    private readonly DisplayTriggerInputTracker inputTracker;
    private readonly object sequenceLock = new();
    private CancellationTokenSource? sequenceCancellation;
    private bool disposed;

    public DisplayTriggerOrchestrator(
        IDisplayTriggerSink sink,
        DisplayTriggerInputTracker? inputTracker = null)
    {
        this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
        this.inputTracker = inputTracker ?? new DisplayTriggerInputTracker();
    }

    public bool HasMediaChangedTrigger(IReadOnlyList<DisplayTriggerConfig> triggers)
    {
        ArgumentNullException.ThrowIfNull(triggers);

        return NormalizeEnabled(triggers).Any(IsMediaChanged);
    }

    public Task<DisplayTriggerDispatchResult> HandleInputAsync(
        IReadOnlyList<DisplayTriggerConfig> triggers,
        DisplayTriggerRuntimeState state,
        string input,
        int? sliderValue = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(triggers);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var candidates = triggers
            .Select((trigger, index) => (Trigger: DisplayTriggerNormalizer.Normalize(trigger), Index: index))
            .Where(candidate => candidate.Trigger.Enabled
                && IsEventType(candidate.Trigger, "input")
                && InputMatches(candidate.Trigger, input))
            .ToList();

        if (candidates.Count == 0)
        {
            return Task.FromResult(DisplayTriggerDispatchResult.NotHandled);
        }

        var percent = sliderValue is null
            ? (int?)null
            : DisplayTriggerRenderer.MapSliderValueToPercent(sliderValue.Value);
        var selected = percent is null
            ? candidates.Select(candidate => candidate.Trigger).ToList()
            : candidates
                .Where(candidate => inputTracker.ShouldTrigger(
                    $"{input}|{candidate.Index}",
                    candidate.Trigger,
                    percent.Value))
                .Select(candidate => candidate.Trigger)
                .ToList();

        return selected.Count == 0
            ? Task.FromResult(DisplayTriggerDispatchResult.NotHandled)
            : SendSequenceAsync(
                selected,
                state,
                new DisplayTriggerRenderContext
                {
                    Input = input,
                    SliderValue = sliderValue,
                    SliderPercent = percent
                },
                cancellationToken);
    }

    public Task<DisplayTriggerDispatchResult> HandleActionResultAsync(
        IReadOnlyList<DisplayTriggerConfig> triggers,
        DisplayTriggerRuntimeState state,
        string input,
        string status,
        string actionName,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(triggers);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var normalized = NormalizeEnabled(triggers).ToList();
        var selected = normalized
            .Where(trigger => IsEventType(trigger, "actionResult")
                && InputMatches(trigger, input)
                && DisplayTriggerMatcher.ActionStatusMatches(trigger, status))
            .ToList();

        if (selected.Count == 0)
        {
            return Task.FromResult(DisplayTriggerDispatchResult.NotHandled);
        }

        if (state.MediaTrack is not null
            && selected.Any(trigger => trigger.DurationMs > 0))
        {
            selected.AddRange(normalized.Where(IsMediaChanged));
        }

        return SendSequenceAsync(
            selected,
            state,
            new DisplayTriggerRenderContext
            {
                Input = input,
                ActionStatus = status,
                ActionName = actionName,
                ActionMessage = message,
                MediaArtist = state.MediaTrack?.Artist ?? "",
                MediaTitle = state.MediaTrack?.Title ?? ""
            },
            cancellationToken);
    }

    public Task<DisplayTriggerDispatchResult> HandleMediaChangedAsync(
        IReadOnlyList<DisplayTriggerConfig> triggers,
        DisplayTriggerRuntimeState state,
        NowPlayingTrack track,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(triggers);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(track);

        var selected = NormalizeEnabled(triggers).Where(IsMediaChanged).ToList();
        return selected.Count == 0
            ? Task.FromResult(DisplayTriggerDispatchResult.NotHandled)
            : SendSequenceAsync(
                selected,
                state,
                new DisplayTriggerRenderContext
                {
                    Input = "*",
                    MediaArtist = track.Artist,
                    MediaTitle = track.Title
                },
                cancellationToken);
    }

    public Task<DisplayTriggerDispatchResult> HandleConnectionChangedAsync(
        IReadOnlyList<DisplayTriggerConfig> triggers,
        DisplayTriggerRuntimeState state,
        string connectionStatus,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(triggers);
        ArgumentNullException.ThrowIfNull(state);

        var normalizedStatus = DisplayTriggerNormalizer.NormalizeConnectionStatus(connectionStatus);
        var selected = NormalizeEnabled(triggers)
            .Where(trigger => IsEventType(trigger, "connectionChanged")
                && DisplayTriggerMatcher.ConnectionStatusMatches(trigger, normalizedStatus))
            .ToList();

        return selected.Count == 0
            ? Task.FromResult(DisplayTriggerDispatchResult.NotHandled)
            : SendSequenceAsync(
                selected,
                state,
                new DisplayTriggerRenderContext
                {
                    Input = "*",
                    ConnectionStatus = connectionStatus
                },
                cancellationToken);
    }

    public Task<DisplayTriggerDispatchResult> HandleProfileChangedAsync(
        IReadOnlyList<DisplayTriggerConfig> triggers,
        DisplayTriggerRuntimeState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(triggers);
        ArgumentNullException.ThrowIfNull(state);

        var selected = NormalizeEnabled(triggers)
            .Where(trigger => IsEventType(trigger, "profileChanged"))
            .ToList();

        return selected.Count == 0
            ? Task.FromResult(DisplayTriggerDispatchResult.NotHandled)
            : SendSequenceAsync(
                selected,
                state,
                new DisplayTriggerRenderContext { Input = "*" },
                cancellationToken);
    }

    public void ResetInputTracking()
    {
        inputTracker.Clear();
    }

    public void CancelSequence()
    {
        lock (sequenceLock)
        {
            sequenceCancellation?.Cancel();
        }
    }

    public void Dispose()
    {
        lock (sequenceLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            sequenceCancellation?.Cancel();
            sequenceCancellation?.Dispose();
            sequenceCancellation = null;
        }
    }

    private async Task<DisplayTriggerDispatchResult> SendSequenceAsync(
        IReadOnlyList<DisplayTriggerConfig> triggers,
        DisplayTriggerRuntimeState state,
        DisplayTriggerRenderContext eventContext,
        CancellationToken cancellationToken)
    {
        var sequenceToken = BeginSequence(cancellationToken);
        var sentCount = 0;

        try
        {
            for (var index = 0; index < triggers.Count; index++)
            {
                var trigger = triggers[index];
                var lines = DisplayTriggerRenderer.RenderLines(
                    trigger,
                    eventContext with
                    {
                        Profile = state.ActiveProfileName,
                        Status = state.ConnectionStatus
                    });

                if (lines.Count == 0)
                {
                    continue;
                }

                await sink.SendTemporaryTemplateAsync(lines, trigger.DurationMs, sequenceToken);
                sentCount++;

                if (index < triggers.Count - 1 && trigger.DurationMs > 0)
                {
                    await Task.Delay(trigger.DurationMs, sequenceToken);
                }
            }

            return new DisplayTriggerDispatchResult(triggers.Count, sentCount, false);
        }
        catch (OperationCanceledException) when (sequenceToken.IsCancellationRequested)
        {
            return new DisplayTriggerDispatchResult(triggers.Count, sentCount, true);
        }
    }

    private CancellationToken BeginSequence(CancellationToken cancellationToken)
    {
        lock (sequenceLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            sequenceCancellation?.Cancel();
            sequenceCancellation?.Dispose();
            sequenceCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return sequenceCancellation.Token;
        }
    }

    private static IEnumerable<DisplayTriggerConfig> NormalizeEnabled(
        IEnumerable<DisplayTriggerConfig> triggers)
    {
        return triggers
            .Select(DisplayTriggerNormalizer.Normalize)
            .Where(trigger => trigger.Enabled);
    }

    private static bool InputMatches(DisplayTriggerConfig trigger, string input)
    {
        return trigger.Input == "*"
            || trigger.Input.Equals(input, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMediaChanged(DisplayTriggerConfig trigger)
    {
        return IsEventType(trigger, "mediaChanged");
    }

    private static bool IsEventType(DisplayTriggerConfig trigger, string eventType)
    {
        return trigger.EventType.Equals(eventType, StringComparison.Ordinal);
    }
}
