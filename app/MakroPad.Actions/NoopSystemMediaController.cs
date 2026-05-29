namespace MakroPad.Actions;

public sealed class NoopSystemMediaController : ISystemMediaController
{
    public Task<ActionExecutionResult> PlayPauseAsync(CancellationToken cancellationToken = default)
        => UnsupportedAsync("Media Play/Pause", cancellationToken);

    public Task<ActionExecutionResult> NextTrackAsync(CancellationToken cancellationToken = default)
        => UnsupportedAsync("Media Next Track", cancellationToken);

    public Task<ActionExecutionResult> PreviousTrackAsync(CancellationToken cancellationToken = default)
        => UnsupportedAsync("Media Previous Track", cancellationToken);

    public Task<ActionExecutionResult> StopAsync(CancellationToken cancellationToken = default)
        => UnsupportedAsync("Media Stop", cancellationToken);

    public Task<ActionExecutionResult> VolumeUpAsync(CancellationToken cancellationToken = default)
        => UnsupportedAsync("Volume Up", cancellationToken);

    public Task<ActionExecutionResult> VolumeDownAsync(CancellationToken cancellationToken = default)
        => UnsupportedAsync("Volume Down", cancellationToken);

    public Task<ActionExecutionResult> MuteAsync(CancellationToken cancellationToken = default)
        => UnsupportedAsync("Mute", cancellationToken);

    private static Task<ActionExecutionResult> UnsupportedAsync(
        string actionName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ActionExecutionResult.Failed(
            $"{actionName} is only implemented on Windows for now"));
    }
}
