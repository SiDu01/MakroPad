namespace MakroPad.Actions;

public interface ISystemMediaController
{
    Task<ActionExecutionResult> PlayPauseAsync(CancellationToken cancellationToken = default);

    Task<ActionExecutionResult> NextTrackAsync(CancellationToken cancellationToken = default);

    Task<ActionExecutionResult> PreviousTrackAsync(CancellationToken cancellationToken = default);

    Task<ActionExecutionResult> StopAsync(CancellationToken cancellationToken = default);

    Task<ActionExecutionResult> VolumeUpAsync(CancellationToken cancellationToken = default);

    Task<ActionExecutionResult> VolumeDownAsync(CancellationToken cancellationToken = default);

    Task<ActionExecutionResult> MuteAsync(CancellationToken cancellationToken = default);
}
