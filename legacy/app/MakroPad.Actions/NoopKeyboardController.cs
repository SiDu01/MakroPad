namespace MakroPad.Actions;

public sealed class NoopKeyboardController : IKeyboardController
{
    public Task<ActionExecutionResult> SendShortcutAsync(
        string shortcut,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ActionExecutionResult.Failed(
            "Keyboard shortcuts are only implemented on Windows for now"));
    }
}
