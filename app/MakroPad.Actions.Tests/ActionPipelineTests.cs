using MakroPad.Core.Configuration;

namespace MakroPad.Actions.Tests;

public sealed class ActionPipelineTests
{
    [Fact]
    public async Task HandleInputAsyncExecutesBoundButton()
    {
        var pipeline = CreatePipeline();

        var result = await pipeline.HandleInputAsync(MakroPadConfig.CreateDefault(), "button:1");

        Assert.Equal(ActionPipelineResultKind.Executed, result.Kind);
        Assert.Equal("media.playPause", result.Binding?.Action);
        Assert.True(result.ExecutionResult?.Succeeded);
    }

    [Fact]
    public async Task HandleInputAsyncReturnsUnboundForMissingInput()
    {
        var pipeline = CreatePipeline();

        var result = await pipeline.HandleInputAsync(MakroPadConfig.CreateDefault(), "button:99");

        Assert.Equal(ActionPipelineResultKind.Unbound, result.Kind);
    }

    [Fact]
    public async Task HandleInputAsyncThrottlesRepeatedSliderInput()
    {
        var pipeline = CreatePipeline();
        var config = MakroPadConfig.CreateDefault();

        var first = await pipeline.HandleInputAsync(config, "slider:1", "1000");
        var second = await pipeline.HandleInputAsync(config, "slider:1", "1001");

        Assert.Equal(ActionPipelineResultKind.Executed, first.Kind);
        Assert.Equal(ActionPipelineResultKind.Throttled, second.Kind);
    }

    [Fact]
    public async Task HandleInputAsyncIgnoresOnePercentSliderVolumeJitter()
    {
        var volumeController = new FakeVolumeController();
        var pipeline = CreatePipeline(volumeController);
        var config = MakroPadConfig.CreateDefault();

        var first = await pipeline.HandleInputAsync(config, "slider:1", "4095");
        await Task.Delay(120);
        var second = await pipeline.HandleInputAsync(config, "slider:1", "4054");
        await Task.Delay(120);
        var third = await pipeline.HandleInputAsync(config, "slider:1", "3972");

        Assert.Equal(ActionPipelineResultKind.Executed, first.Kind);
        Assert.Equal(ActionPipelineResultKind.Throttled, second.Kind);
        Assert.Equal(ActionPipelineResultKind.Executed, third.Kind);
        Assert.Equal([100, 97], volumeController.SetVolumes);
    }

    private static ActionPipeline CreatePipeline(FakeVolumeController? volumeController = null)
    {
        return new ActionPipeline(
            new ActionResolver(),
            new ActionExecutor(new FakeMediaController(), volumeController ?? new FakeVolumeController()));
    }

    private sealed class FakeMediaController : ISystemMediaController
    {
        public Task<ActionExecutionResult> PlayPauseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(ActionExecutionResult.Ok("Play/Pause"));
        }

        public Task<ActionExecutionResult> NextTrackAsync(CancellationToken cancellationToken = default)
            => PlayPauseAsync(cancellationToken);

        public Task<ActionExecutionResult> PreviousTrackAsync(CancellationToken cancellationToken = default)
            => PlayPauseAsync(cancellationToken);

        public Task<ActionExecutionResult> StopAsync(CancellationToken cancellationToken = default)
            => PlayPauseAsync(cancellationToken);

        public Task<ActionExecutionResult> VolumeUpAsync(CancellationToken cancellationToken = default)
            => PlayPauseAsync(cancellationToken);

        public Task<ActionExecutionResult> VolumeDownAsync(CancellationToken cancellationToken = default)
            => PlayPauseAsync(cancellationToken);

        public Task<ActionExecutionResult> MuteAsync(CancellationToken cancellationToken = default)
            => PlayPauseAsync(cancellationToken);
    }

    private sealed class FakeVolumeController : ISystemVolumeController
    {
        public List<int> SetVolumes { get; } = [];

        public Task<ActionExecutionResult> SetMasterVolumeAsync(
            int percent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetVolumes.Add(percent);

            return Task.FromResult(ActionExecutionResult.Ok($"Volume {percent}%"));
        }

        public Task<int?> GetMasterVolumePercentAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<int?>(50);
        }
    }
}
