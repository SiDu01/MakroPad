namespace MakroPad.Actions;

public interface IApplicationVolumeController
{
    Task<ActionExecutionResult> SetApplicationVolumeAsync(
        string processName,
        int percent,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApplicationAudioSession>> GetAudioSessionsAsync(
        CancellationToken cancellationToken = default);
}
