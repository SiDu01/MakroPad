namespace MakroPad.Actions;

public interface ISystemVolumeController
{
    Task<ActionExecutionResult> SetMasterVolumeAsync(int percent, CancellationToken cancellationToken = default);
}
