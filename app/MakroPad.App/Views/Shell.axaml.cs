using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MakroPad.App.ViewModels;

namespace MakroPad.App.Views;

public partial class Shell : Window
{
    public Shell()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Closed += (_, _) => ViewModel?.Shutdown();
    }

    private ShellViewModel? ViewModel => DataContext as ShellViewModel;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var actionEditor = ViewModel?.ProfilesEditor.ActionEditor;
        if (actionEditor?.IsRecordingShortcut != true)
        {
            return;
        }

        e.Handled = true;

        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            actionEditor.CancelShortcutRecording();
            return;
        }

        var shortcut = FormatShortcut(e);
        if (shortcut is null)
        {
            return;
        }

        actionEditor.CompleteShortcutRecording(shortcut);
    }

    private static string? FormatShortcut(KeyEventArgs e)
    {
        var key = NormalizeKey(e.Key);
        if (key is null)
        {
            return null;
        }

        var parts = new List<string>();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add("Win");
        }

        parts.Add(key);
        return string.Join("+", parts);
    }

    private static string? NormalizeKey(Key key)
    {
        return key switch
        {
            Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin => null,
            Key.Return => "Enter",
            Key.Escape => "Escape",
            Key.Back => "Backspace",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Home => "Home",
            Key.End => "End",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            >= Key.A and <= Key.Z => key.ToString(),
            >= Key.D0 and <= Key.D9 => key.ToString()[1..],
            >= Key.NumPad0 and <= Key.NumPad9 => key.ToString()["NumPad".Length..],
            >= Key.F1 and <= Key.F24 => key.ToString(),
            _ => null
        };
    }
}
