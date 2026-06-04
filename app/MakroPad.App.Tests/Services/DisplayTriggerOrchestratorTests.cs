using MakroPad.App.Services;
using MakroPad.Core.Configuration;
using MakroPad.Core.DisplayTriggers;

namespace MakroPad.App.Tests.Services;

public sealed class DisplayTriggerOrchestratorTests
{
    private static readonly DisplayTriggerRuntimeState DefaultState =
        new("Studio", "Connected");

    [Fact]
    public async Task InputTriggerUsesSliderTrackerAndRendersRuntimeContext()
    {
        var sink = new RecordingDisplayTriggerSink();
        using var orchestrator = new DisplayTriggerOrchestrator(sink);
        var triggers = new[]
        {
            Trigger(
                eventType: "input",
                input: "slider:1",
                changePercent: 5,
                template: "{profile}|{status}|{slider.percent}")
        };

        var first = await orchestrator.HandleInputAsync(triggers, DefaultState, "slider:1", 2048);
        var second = await orchestrator.HandleInputAsync(triggers, DefaultState, "slider:1", 2100);
        var third = await orchestrator.HandleInputAsync(triggers, DefaultState, "slider:1", 2458);

        Assert.False(first.Handled);
        Assert.False(second.Handled);
        Assert.True(third.Handled);
        Assert.Equal("Studio|Connected|60", sink.Sends.Single().Lines.Single().Text);
    }

    [Fact]
    public async Task ActionResultRestoresMediaAfterTemporaryScreen()
    {
        var sink = new RecordingDisplayTriggerSink();
        using var orchestrator = new DisplayTriggerOrchestrator(sink);
        var state = DefaultState with { MediaTrack = new NowPlayingTrack("Artist", "Title") };
        var triggers = new[]
        {
            Trigger("actionResult", "*", durationMs: 1, template: "Action {action.message}"),
            Trigger("mediaChanged", "*", durationMs: 0, template: "{media.artist} - {media.title}")
        };

        var result = await orchestrator.HandleActionResultAsync(
            triggers,
            state,
            "button:1",
            "ok",
            "media.playPause",
            "Done");

        Assert.True(result.Handled);
        Assert.Equal(2, result.SentScreenCount);
        Assert.Collection(
            sink.Sends,
            send => Assert.Equal("Action Done", send.Lines.Single().Text),
            send => Assert.Equal("Artist - Title", send.Lines.Single().Text));
    }

    [Fact]
    public async Task NewEventCancelsRunningSequence()
    {
        var sink = new RecordingDisplayTriggerSink();
        using var orchestrator = new DisplayTriggerOrchestrator(sink);
        var slowTriggers = new[]
        {
            Trigger("input", "button:1", durationMs: 10000, template: "First"),
            Trigger("input", "button:1", durationMs: 0, template: "Never")
        };
        var replacement = new[]
        {
            Trigger("profileChanged", "*", durationMs: 0, template: "Profile {profile}")
        };

        var firstTask = orchestrator.HandleInputAsync(slowTriggers, DefaultState, "button:1");
        await sink.FirstSend.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = await orchestrator.HandleProfileChangedAsync(replacement, DefaultState);
        var first = await firstTask;

        Assert.True(first.WasCancelled);
        Assert.True(second.Handled);
        Assert.DoesNotContain(sink.Sends, send => send.Lines.Any(line => line.Text == "Never"));
        Assert.Contains(sink.Sends, send => send.Lines.Any(line => line.Text == "Profile Studio"));
    }

    [Fact]
    public async Task ConnectionTriggerMatchesNormalizedStatus()
    {
        var sink = new RecordingDisplayTriggerSink();
        using var orchestrator = new DisplayTriggerOrchestrator(sink);
        var triggers = new[]
        {
            Trigger(
                eventType: "connectionChanged",
                input: "*",
                connectionStatus: "connected",
                template: "{connection.status}")
        };

        var result = await orchestrator.HandleConnectionChangedAsync(
            triggers,
            DefaultState,
            "Connected");

        Assert.True(result.Handled);
        Assert.Equal("Connected", sink.Sends.Single().Lines.Single().Text);
    }

    private static DisplayTriggerConfig Trigger(
        string eventType,
        string input,
        string connectionStatus = "any",
        int changePercent = 0,
        int durationMs = 0,
        string template = "Display")
    {
        return new DisplayTriggerConfig
        {
            Enabled = true,
            EventType = eventType,
            Input = input,
            ConnectionStatus = connectionStatus,
            ChangePercent = changePercent,
            DurationMs = durationMs,
            Template = template
        };
    }

    private sealed class RecordingDisplayTriggerSink : IDisplayTriggerSink
    {
        private readonly object syncRoot = new();

        public List<Send> Sends { get; } = [];

        public TaskCompletionSource FirstSend { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SendTemporaryTemplateAsync(
            IReadOnlyList<DisplayTemplateLine> lines,
            int durationMs,
            CancellationToken cancellationToken = default)
        {
            lock (syncRoot)
            {
                Sends.Add(new Send(lines.ToList(), durationMs));
            }

            FirstSend.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private sealed record Send(IReadOnlyList<DisplayTemplateLine> Lines, int DurationMs);
}
