using MakroPad.Core.Actions;
using MakroPad.Core.Device;
using MakroPad.Core.Protocol;
using MakroPad.Core.Runtime;

namespace MakroPad.Core.Tests.Runtime;

public sealed class InputRouterTests
{
    private readonly InputRouter router = new(
    [
        new Binding("button:1", "media.playPause"),
        new Binding("slider:1", "system.volume")
    ]);

    [Fact]
    public void ButtonRoutesOnPressOnly()
    {
        Assert.Equal("media.playPause", router.Route(new ButtonInput(1, true))?.Action);
        Assert.Null(router.Route(new ButtonInput(1, false)));
    }

    [Fact]
    public void UnboundInputIsIgnored()
    {
        Assert.Null(router.Route(new ButtonInput(2, true)));
    }

    [Fact]
    public void SliderDeadbandSuppressesJitterButKeepsEndpoints()
    {
        Assert.NotNull(router.Route(new SliderInput(1, 500)));
        Assert.Null(router.Route(new SliderInput(1, 510)));
        Assert.NotNull(router.Route(new SliderInput(1, 520)));
        Assert.Null(router.Route(new SliderInput(1, 520)));

        Assert.NotNull(router.Route(new SliderInput(1, 15)));
        Assert.NotNull(router.Route(new SliderInput(1, 0)));
        Assert.Null(router.Route(new SliderInput(1, 0)));
    }
}

public sealed class LatestWinsQueueTests
{
    [Fact]
    public async Task IntermediateItemsAreSkippedButLastRuns()
    {
        var processed = new List<int>();
        var gate = new TaskCompletionSource();
        var queue = new LatestWinsQueue<int>(async item =>
        {
            processed.Add(item);
            if (item == 1)
            {
                await gate.Task;
            }
        });

        var drain = queue.Submit(1);
        await queue.Submit(2);
        await queue.Submit(3);
        await queue.Submit(4);
        gate.SetResult();
        await drain;

        Assert.Equal([1, 4], processed);
    }

    [Fact]
    public async Task FailingWorkDoesNotBlockQueue()
    {
        var processed = new List<int>();
        var queue = new LatestWinsQueue<int>(item =>
        {
            processed.Add(item);
            return item == 1 ? throw new InvalidOperationException() : Task.CompletedTask;
        });

        await queue.Submit(1);
        await queue.Submit(2);

        Assert.Equal([1, 2], processed);
    }
}

public sealed class FeedbackBuilderTests
{
    private static readonly Binding Binding = new("slider:1", "system.volume");

    [Fact]
    public void PercentResultBecomesVolumeScreen()
    {
        var screen = FeedbackBuilder.ForResult(Binding, ActionResult.Ok("System", 42));

        Assert.Equal(ScreenType.Volume, screen.Type);
        Assert.Equal(42, screen.Value);
        Assert.Equal("System", screen.Text);
    }

    [Fact]
    public void FailureBecomesErrorScreen()
    {
        var screen = FeedbackBuilder.ForResult(Binding, ActionResult.Failed("kaputt"));

        Assert.Equal(ScreenType.Action, screen.Type);
        Assert.Equal(ScreenIcon.Fail, screen.Icon);
        Assert.Equal("kaputt", screen.Text);
    }

    [Fact]
    public void PlainSuccessBecomesActionScreen()
    {
        var screen = FeedbackBuilder.ForResult(Binding, ActionResult.Ok("Play/Pause"));

        Assert.Equal(ScreenIcon.Ok, screen.Icon);
        Assert.Equal("Play/Pause", screen.Text);
    }
}

public sealed class ActionRunnerTests
{
    [Fact]
    public async Task UnknownActionAndExceptionsBecomeFailures()
    {
        var runner = new ActionRunner([new ThrowingAction()]);
        var input = new ButtonInput(1, true);

        var unknown = await runner.RunAsync(new ActionContext(new Binding("button:1", "nope"), input));
        var thrown = await runner.RunAsync(new ActionContext(new Binding("button:1", "test.throw"), input));

        Assert.False(unknown.Success);
        Assert.False(thrown.Success);
        Assert.Contains("boom", thrown.Message, StringComparison.Ordinal);
    }

    private sealed class ThrowingAction : IActionHandler
    {
        public string ActionId => "test.throw";

        public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom");
    }
}
