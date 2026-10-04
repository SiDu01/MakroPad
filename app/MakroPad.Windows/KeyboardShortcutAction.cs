using MakroPad.Core.Actions;

namespace MakroPad.Windows;

/// <summary><c>keyboard.shortcut</c>: sends e.g. <c>keys=Ctrl+Shift+Esc</c> to the focused window.</summary>
public sealed class KeyboardShortcutAction : IActionHandler
{
    public string ActionId => "keyboard.shortcut";

    public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var shortcut = context.Binding.GetParameter("keys") ?? "";
        if (!ShortcutParser.TryParse(shortcut, out var keys, out var error))
        {
            return Task.FromResult(ActionResult.Failed(error));
        }

        NativeInput.SendChord(keys, ShortcutParser.IsExtendedKey);
        return Task.FromResult(ActionResult.Ok(shortcut));
    }
}
