using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MakroPad.App.ViewModels;

namespace MakroPad.App.Views;

public partial class BindingInspectorView : UserControl
{
    public BindingInspectorView() => InitializeComponent();

    private void OnRecordClicked(object? sender, RoutedEventArgs e)
    {
        // Recording listens on the text box, so it needs the focus.
        if (sender is Control { Parent: DockPanel panel } && panel.FindControl<TextBox>("ShortcutBox") is { } box)
        {
            box.Focus();
        }
    }

    private void OnShortcutKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: ShortcutParameterViewModel { IsRecording: true } parameter })
        {
            return;
        }

        e.Handled = true;
        if (ShortcutText.FromKey(e.Key, e.KeyModifiers) is { } shortcut)
        {
            parameter.Value = shortcut;
            parameter.IsRecording = false;
        }
    }

    private void OnShortcutLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: ShortcutParameterViewModel parameter })
        {
            parameter.IsRecording = false;
        }
    }
}

/// <summary>Turns a key press into the shortcut syntax of <c>keyboard.shortcut</c>.</summary>
internal static class ShortcutText
{
    public static string? FromKey(Key key, KeyModifiers modifiers)
    {
        var name = KeyName(key);
        if (name is null)
        {
            return null; // modifier alone or unsupported key: keep waiting
        }

        var parts = new List<string>();
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add("Win");
        }

        parts.Add(name);
        return string.Join('+', parts);
    }

    private static string? KeyName(Key key) => key switch
    {
        >= Key.A and <= Key.Z => key.ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (key - Key.NumPad0))).ToString(),
        >= Key.F1 and <= Key.F24 => key.ToString(),
        Key.Enter => "Enter",
        Key.Escape => "Esc",
        Key.Tab => "Tab",
        Key.Space => "Space",
        Key.Back => "Backspace",
        Key.Delete => "Delete",
        Key.Insert => "Insert",
        Key.Home => "Home",
        Key.End => "End",
        Key.PageUp => "PageUp",
        Key.PageDown => "PageDown",
        Key.Up => "Up",
        Key.Down => "Down",
        Key.Left => "Left",
        Key.Right => "Right",
        Key.Snapshot => "PrintScreen",
        Key.Pause => "Pause",
        Key.CapsLock => "CapsLock",
        Key.NumLock => "NumLock",
        Key.Scroll => "ScrollLock",
        Key.Apps => "Apps",
        Key.OemPlus => "Plus",
        Key.OemMinus => "Minus",
        Key.OemComma => "Comma",
        Key.OemPeriod => "Period",
        _ => null
    };
}
