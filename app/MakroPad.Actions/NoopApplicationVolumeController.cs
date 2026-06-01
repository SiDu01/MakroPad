namespace MakroPad.Actions;

public sealed class NoopApplicationVolumeController : IApplicationVolumeController
{
    public Task<ActionExecutionResult> SetApplicationVolumeAsync(
        string processName,
        int percent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ActionExecutionResult.Failed(
            "Application volume is only implemented on Windows for now"));
    }
}
