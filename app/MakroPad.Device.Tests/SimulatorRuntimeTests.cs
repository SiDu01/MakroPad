using System.Collections.Concurrent;
using MakroPad.Core.Actions;
using MakroPad.Core.Configuration;
using MakroPad.Core.Device;
using MakroPad.Core.Runtime;

namespace MakroPad.Device.Tests;

/// <summary>End-to-end: simulated device → runtime → action → OLED feedback line.</summary>
public sealed class SimulatorRuntimeTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly SimulatedDevice device = new();
    private readonly ConcurrentQueue<string> linesFromApp = new();
    private readonly RecordingAction playPause = new("media.playPause");
    private readonly RecordingAction volume = new("system.volume");
    private MakroPadRuntime runtime = null!;

    public Task InitializeAsync()
    {
        device.LineFromApp += linesFromApp.Enqueue;
        var profiles = new ProfileManager();
        profiles.Load(
        [
            new ProfileConfig
            {
                Name = "Default",
                Bindings =
                [
                    new Binding("button:1", "media.playPause"),
                    new Binding("button:16", "profile.next"),
                    new Binding("slider:1", "system.volume", new Dictionary<string, string> { ["label"] = "Master" })
                ]
            },
            new ProfileConfig { Name = "Gaming", Bindings = [new Binding("button:16", "profile.next")] }
        ]);
        runtime = new MakroPadRuntime(
            device,
            new ActionRunner(new IActionHandler[] { playPause, volume }.Concat(new ProfileActions(profiles).Create())),
            profiles,
            TimeProvider.System,
            "test");
        runtime.Start();
        return WaitUntil(() => runtime.Status == ConnectionStatus.Connected);
    }

    public async Task DisposeAsync() => await runtime.DisposeAsync();

    [Fact]
    public async Task HandshakeSendsAckAndState()
    {
        await WaitUntil(() => linesFromApp.Any(line => line.StartsWith("state;profile=Default;time=", StringComparison.Ordinal)));
        Assert.Contains("hello-ack;proto=2;app=test", linesFromApp);
    }

    [Fact]
    public async Task ButtonPressRunsActionAndShowsFeedback()
    {
        device.PressButton(1);

        await WaitUntil(() => linesFromApp.Contains("screen;type=action;text=media.playPause;icon=ok"));
        Assert.Equal(1, playPause.Count);
    }

    [Fact]
    public async Task SliderRunsVolumeActionWithPercent()
    {
        device.MoveSlider(1, 420);

        await WaitUntil(() => linesFromApp.Contains("screen;type=volume;text=Master;value=42;ms=1200"));
    }

    [Fact]
    public async Task ProfileButtonSwitchesBindingsAndShowsProfile()
    {
        device.PressButton(16);

        await WaitUntil(() => linesFromApp.Contains("screen;type=profile;text=Gaming"));
        Assert.Contains(linesFromApp, line => line.StartsWith("state;profile=Gaming;", StringComparison.Ordinal));
        Assert.Equal("Gaming", runtime.Profiles.Active.Name);

        device.PressButton(1);
        await Task.Delay(200);
        Assert.Equal(0, playPause.Count);
    }

    [Fact]
    public async Task DisabledFeedbackSendsNoOverlay()
    {
        runtime.Settings = new AppSettings { Feedback = new FeedbackSettings { Actions = false } };

        device.PressButton(1);
        await WaitUntil(() => playPause.Count == 1);
        await Task.Delay(200);

        Assert.DoesNotContain(linesFromApp, line => line.StartsWith("screen;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReconnectsAfterUnplug()
    {
        var statuses = new ConcurrentQueue<ConnectionStatus>();
        runtime.StatusChanged += statuses.Enqueue;

        device.Disconnect();

        await WaitUntil(() => statuses.SequenceEqual([ConnectionStatus.Searching, ConnectionStatus.Connected]));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition not met in time.");
            }

            await Task.Delay(20);
        }
    }

    private sealed class RecordingAction(string id) : IActionHandler
    {
        private int count;

        public int Count => Volatile.Read(ref count);

        public string ActionId => id;

        public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref count);
            var percent = context.Input is SliderInput slider ? slider.Percent : (int?)null;
            return Task.FromResult(ActionResult.Ok(id, percent));
        }
    }
}
