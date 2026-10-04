namespace MakroPad.Actions;

public interface ISystemLauncher
{
    Task<ActionExecutionResult> OpenUrlAsync(
        string url,
        CancellationToken cancellationToken = default);

    Task<ActionExecutionResult> OpenAppAsync(
        string target,
        CancellationToken cancellationToken = default);
}
