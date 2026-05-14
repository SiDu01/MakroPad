namespace MakroPad.Actions;

public interface ISystemMediaController
{
    Task<ActionExecutionResult> PlayPauseAsync(CancellationToken cancellationToken = default);
}
