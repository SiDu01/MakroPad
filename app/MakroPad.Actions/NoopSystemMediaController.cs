namespace MakroPad.Actions;

public sealed class NoopSystemMediaController : ISystemMediaController
{
    public Task<ActionExecutionResult> PlayPauseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ActionExecutionResult.Failed(
            "Media Play/Pause is only implemented on Windows for now"));
    }
}
