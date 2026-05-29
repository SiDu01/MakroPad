namespace MakroPad.Actions;

public interface IKeyboardController
{
    Task<ActionExecutionResult> SendShortcutAsync(
        string shortcut,
        CancellationToken cancellationToken = default);
}
