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
        var executor = new ActionExecutor(
            new FakeMediaController(),
            volumeController,
            new FakeApplicationVolumeController(),
            new FakeKeyboardController(),
            new FakeSystemLauncher());

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
        var executor = new ActionExecutor(
            new FakeMediaController(),
            volumeController,
            new FakeApplicationVolumeController(),
            new FakeKeyboardController(),
            new FakeSystemLauncher());

        var result = await executor.ExecuteAsync(new ActionExecutionContext(
            "slider:1",
            new ActionBinding { Action = "system.volume" },
            "not-a-number"));

        Assert.False(result.Succeeded);
        Assert.Empty(volumeController.SetVolumes);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("2048", 50)]
    [InlineData("4095", 100)]
    public async Task ExecuteAsyncMapsSliderValueToApplicationVolumePercent(string rawValue, int expectedPercent)
    {
        var appVolumeController = new FakeApplicationVolumeController();
        var executor = new ActionExecutor(
            new FakeMediaController(),
            new FakeVolumeController(),
            appVolumeController,
            new FakeKeyboardController(),
            new FakeSystemLauncher());

        var result = await executor.ExecuteAsync(new ActionExecutionContext(
            "slider:1",
            new ActionBinding
            {
                Action = "app.volume",
                Parameters = new Dictionary<string, string> { ["process"] = "Spotify" }
            },
            rawValue));

        var setVolume = appVolumeController.SetVolumes.Single();
        Assert.True(result.Succeeded);
        Assert.Equal("Spotify", setVolume.ProcessName);
        Assert.Equal(expectedPercent, setVolume.Percent);
    }

    [Fact]
    public async Task ExecuteAsyncFailsApplicationVolumeActionWhenProcessIsMissing()
    {
        var appVolumeController = new FakeApplicationVolumeController();
        var executor = new ActionExecutor(
            new FakeMediaController(),
            new FakeVolumeController(),
            appVolumeController,
            new FakeKeyboardController(),
            new FakeSystemLauncher());

        var result = await executor.ExecuteAsync(new ActionExecutionContext(
            "slider:1",
            new ActionBinding { Action = "app.volume" },
            "2048"));

        Assert.False(result.Succeeded);
        Assert.Empty(appVolumeController.SetVolumes);
    }

    [Fact]
    public async Task ExecuteAsyncFailsApplicationVolumeActionWhenValueIsInvalid()
    {
        var appVolumeController = new FakeApplicationVolumeController();
        var executor = new ActionExecutor(
            new FakeMediaController(),
            new FakeVolumeController(),
            appVolumeController,
            new FakeKeyboardController(),
            new FakeSystemLauncher());

        var result = await executor.ExecuteAsync(new ActionExecutionContext(
            "slider:1",
            new ActionBinding
            {
                Action = "app.volume",
                Parameters = new Dictionary<string, string> { ["process"] = "Spotify" }
            },
            "not-a-number"));

        Assert.False(result.Succeeded);
        Assert.Empty(appVolumeController.SetVolumes);
    }

    [Theory]
    [InlineData("media.playPause", "playPause")]
    [InlineData("media.nextTrack", "nextTrack")]
    [InlineData("media.previousTrack", "previousTrack")]
    [InlineData("media.stop", "stop")]
    [InlineData("media.volumeUp", "volumeUp")]
    [InlineData("media.volumeDown", "volumeDown")]
    [InlineData("media.mute", "mute")]
    public async Task ExecuteAsyncRunsMediaActions(string action, string expectedCall)
    {
        var mediaController = new FakeMediaController();
        var executor = new ActionExecutor(
            mediaController,
            new FakeVolumeController(),
            new FakeApplicationVolumeController(),
            new FakeKeyboardController(),
            new FakeSystemLauncher());

        var result = await executor.ExecuteAsync(new ActionExecutionContext(
            "button:1",
            new ActionBinding { Action = action },
            null));

        Assert.True(result.Succeeded);
        Assert.Equal(expectedCall, mediaController.Calls.Single());
    }

    [Fact]
    public async Task ExecuteAsyncRunsKeyboardShortcutAction()
    {
        var keyboardController = new FakeKeyboardController();
        var executor = new ActionExecutor(
            new FakeMediaController(),
            new FakeVolumeController(),
            new FakeApplicationVolumeController(),
            keyboardController,
            new FakeSystemLauncher());

        var result = await executor.ExecuteAsync(new ActionExecutionContext(
            "button:1",
            new ActionBinding
            {
                Action = "keyboard.shortcut",
                Parameters = new Dictionary<string, string> { ["keys"] = "Ctrl+Alt+T" }
            },
            null));

        Assert.True(result.Succeeded);
        Assert.Equal("Ctrl+Alt+T", keyboardController.Shortcuts.Single());
    }

    [Fact]
    public async Task ExecuteAsyncRunsOpenUrlAction()
    {
        var launcher = new FakeSystemLauncher();
        var executor = new ActionExecutor(
            new FakeMediaController(),
            new FakeVolumeController(),
            new FakeApplicationVolumeController(),
            new FakeKeyboardController(),
            launcher);

        var result = await executor.ExecuteAsync(new ActionExecutionContext(
            "button:1",
            new ActionBinding
            {
                Action = "system.openUrl",
                Parameters = new Dictionary<string, string> { ["url"] = "https://example.com" }
            },
            null));

        Assert.True(result.Succeeded);
        Assert.Equal("https://example.com", launcher.Urls.Single());
    }

    [Fact]
    public async Task ExecuteAsyncRunsOpenAppAction()
    {
        var launcher = new FakeSystemLauncher();
        var executor = new ActionExecutor(
            new FakeMediaController(),
            new FakeVolumeController(),
            new FakeApplicationVolumeController(),
            new FakeKeyboardController(),
            launcher);

        var result = await executor.ExecuteAsync(new ActionExecutionContext(
            "button:1",
            new ActionBinding
            {
                Action = "system.openApp",
                Parameters = new Dictionary<string, string> { ["path"] = "notepad.exe" }
            },
            null));

        Assert.True(result.Succeeded);
        Assert.Equal("notepad.exe", launcher.Apps.Single());
    }

    private sealed class FakeMediaController : ISystemMediaController
    {
        public List<string> Calls { get; } = [];

        public Task<ActionExecutionResult> PlayPauseAsync(CancellationToken cancellationToken = default)
            => RecordAsync("playPause", cancellationToken);

        public Task<ActionExecutionResult> NextTrackAsync(CancellationToken cancellationToken = default)
            => RecordAsync("nextTrack", cancellationToken);

        public Task<ActionExecutionResult> PreviousTrackAsync(CancellationToken cancellationToken = default)
            => RecordAsync("previousTrack", cancellationToken);

        public Task<ActionExecutionResult> StopAsync(CancellationToken cancellationToken = default)
            => RecordAsync("stop", cancellationToken);

        public Task<ActionExecutionResult> VolumeUpAsync(CancellationToken cancellationToken = default)
            => RecordAsync("volumeUp", cancellationToken);

        public Task<ActionExecutionResult> VolumeDownAsync(CancellationToken cancellationToken = default)
            => RecordAsync("volumeDown", cancellationToken);

        public Task<ActionExecutionResult> MuteAsync(CancellationToken cancellationToken = default)
            => RecordAsync("mute", cancellationToken);

        private Task<ActionExecutionResult> RecordAsync(string call, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(call);

            return Task.FromResult(ActionExecutionResult.Ok(call));
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

        public Task<int?> GetMasterVolumePercentAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<int?>(SetVolumes.LastOrDefault());
        }
    }

    private sealed class FakeApplicationVolumeController : IApplicationVolumeController
    {
        public List<(string ProcessName, int Percent)> SetVolumes { get; } = [];

        public Task<ActionExecutionResult> SetApplicationVolumeAsync(
            string processName,
            int percent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetVolumes.Add((processName, percent));

            return Task.FromResult(ActionExecutionResult.Ok($"Volume {percent}% for {processName}"));
        }

        public Task<IReadOnlyList<ApplicationAudioSession>> GetAudioSessionsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<IReadOnlyList<ApplicationAudioSession>>([]);
        }
    }

    private sealed class FakeKeyboardController : IKeyboardController
    {
        public List<string> Shortcuts { get; } = [];

        public Task<ActionExecutionResult> SendShortcutAsync(
            string shortcut,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Shortcuts.Add(shortcut);

            return Task.FromResult(ActionExecutionResult.Ok($"Shortcut {shortcut}"));
        }
    }

    private sealed class FakeSystemLauncher : ISystemLauncher
    {
        public List<string> Urls { get; } = [];

        public List<string> Apps { get; } = [];

        public Task<ActionExecutionResult> OpenUrlAsync(
            string url,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Urls.Add(url);

            return Task.FromResult(ActionExecutionResult.Ok($"URL {url}"));
        }

        public Task<ActionExecutionResult> OpenAppAsync(
            string target,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Apps.Add(target);

            return Task.FromResult(ActionExecutionResult.Ok($"App {target}"));
        }
    }
}
