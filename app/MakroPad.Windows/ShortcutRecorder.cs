using System.ComponentModel;
using System.Runtime.InteropServices;
using MakroPad.Core.Actions;

namespace MakroPad.Windows;

/// <summary>
/// Records one shortcut through a low-level keyboard hook. Unlike window key events this also sees
/// combinations Windows normally handles itself (Win+E, Win+R, Alt+Tab …), and it swallows every
/// key while recording so the combination does not trigger anything. Esc alone cancels.
/// Must be started on a thread with a message loop (the UI thread).
/// </summary>
public sealed class ShortcutRecorder
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyUp = 0x0105;
    private const uint LlkhfInjected = 0x10;
    private const ushort Escape = 0x1B;

    private readonly HashSet<ushort> heldModifiers = [];
    private LowLevelKeyboardProc? callback;
    private nint hook;
    private TaskCompletionSource<string?>? pending;

    public bool IsRecording => hook != 0;

    /// <summary>Returns the recorded shortcut, or null when cancelled (Esc, <see cref="Cancel"/> or the token).</summary>
    public Task<string?> RecordAsync(CancellationToken cancellationToken = default)
    {
        Cancel();
        heldModifiers.Clear();
        pending = new TaskCompletionSource<string?>();
        callback = OnKey;
        hook = SetWindowsHookEx(WhKeyboardLl, callback, GetModuleHandle(null), 0);
        if (hook == 0)
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            pending = null;
            return Task.FromException<string?>(error);
        }

        var task = pending.Task;
        cancellationToken.Register(Cancel);
        return task;
    }

    public void Cancel() => Finish(null);

    private nint OnKey(int code, nint message, nint data)
    {
        if (code < 0 || hook == 0)
        {
            return CallNextHookEx(0, code, message, data);
        }

        var info = Marshal.PtrToStructure<KbdLlHookStruct>(data);
        if ((info.Flags & LlkhfInjected) != 0)
        {
            return CallNextHookEx(0, code, message, data);
        }

        var key = (ushort)info.VirtualKey;
        var isDown = message is WmKeyDown or WmSysKeyDown;
        var isUp = message is WmKeyUp or WmSysKeyUp;

        if (ShortcutParser.ToModifier(key) is { } modifier)
        {
            if (isDown)
            {
                heldModifiers.Add(modifier);
            }
            else if (isUp)
            {
                heldModifiers.Remove(modifier);
            }

            return 1; // swallowed, so a lone Win press does not open the Start menu
        }

        if (isDown)
        {
            if (key == Escape && heldModifiers.Count == 0)
            {
                Finish(null);
            }
            else if (ShortcutParser.Format(heldModifiers, key) is { } shortcut)
            {
                Finish(shortcut);
            }
        }

        return 1;
    }

    private void Finish(string? result)
    {
        if (hook != 0)
        {
            UnhookWindowsHookEx(hook);
            hook = 0;
        }

        var completion = pending;
        pending = null;
        completion?.TrySetResult(result);
    }

    private delegate nint LowLevelKeyboardProc(int code, nint message, nint data);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hookId, LowLevelKeyboardProc callback, nint module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}
