using MakroPad.Core.Configuration;

namespace MakroPad.Actions.Tests;

public sealed class ActionExecutorTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("2048", 50)]
    [InlineData("4095", 100)]
    [InlineData("-1", 0)]
    [InlineData("4096", 100)]
    public async Task ExecuteAsyncMapsSliderValueToVolumePercent(string rawValue, int expectedPercent)
    {
        var volumeController = new FakeVolumeController();
        var executor = new ActionExecutor(new FakeMediaController(), volumeController);

        var result = await executor.ExecuteAsync(new ActionExecutionContext(
            "slider:1",
            new ActionBinding { Action = "system.volume" },
            rawValue));

        Assert.True(result.Succeeded);
        Assert.Equal(expectedPercent, volumeController.SetVolumes.Single());
    }

    [Fact]
    public async Task ExecuteAsyncFailsVolumeActionWhenValueIsInvalid()
    {
        var volumeController = new FakeVolumeController();
        var executor = new ActionExecutor(new FakeMediaController(), volumeController);

        var result = await executor.ExecuteAsync(new ActionExecutionContext(
            "slider:1",
            new ActionBinding { Action = "system.volume" },
            "not-a-number"));

        Assert.False(result.Succeeded);
        Assert.Empty(volumeController.SetVolumes);
    }

    private sealed class FakeMediaController : ISystemMediaController
    {
        public Task<ActionExecutionResult> PlayPauseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(ActionExecutionResult.Ok("Play/Pause"));
        }
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
    }
}
