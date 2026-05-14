namespace MakroPad.Actions;

public sealed class NoopSystemVolumeController : ISystemVolumeController
{
    public Task<ActionExecutionResult> SetMasterVolumeAsync(int percent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ActionExecutionResult.Failed(
            "System volume is only implemented on Windows for now"));
    }
}
