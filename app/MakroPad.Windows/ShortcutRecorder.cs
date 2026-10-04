using System.ComponentModel;
using System.Runtime.InteropServices;
using MakroPad.Core.Actions;

namespace MakroPad.Windows;

/// <summary>
/// Records one shortcut through a low-level keyboard hook. Unlike window key events this also sees
/// combinations Windows normally handles itself (Win+E, Win+R, Alt+Tab …), and it swallows every
/// key while recording so the combination does not trigger anything. Esc alone cancels.
/// The hook runs on its own thread with its own message loop: Windows drops (and eventually
/// removes) low-level hooks whose thread does not answer quickly, which a busy UI thread cannot guarantee.
/// </summary>
public sealed class ShortcutRecorder
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyUp = 0x0105;
    private const uint WmQuit = 0x0012;
    private const ushort Escape = 0x1B;

    private readonly object gate = new();
    private Session? current;

    /// <summary>Diagnostic messages (hook installed, keys seen, why recording ended). Raised on the hook thread.</summary>
    public event Action<string>? Trace;

    public bool IsRecording
    {
        get
        {
            lock (gate)
            {
                return current is not null;
            }
        }
    }

    /// <summary>Returns the recorded shortcut, or null when cancelled (Esc or <see cref="Cancel"/>).</summary>
    public Task<string?> RecordAsync()
    {
        Cancel();
        var session = new Session(this);
        lock (gate)
        {
            current = session;
        }

        session.Start();
        return session.Result;
    }

    public void Cancel()
    {
        Session? session;
        lock (gate)
        {
            session = current;
            current = null;
        }

        if (session is not null)
        {
            Trace?.Invoke("Aufnahme abgebrochen");
            session.Stop(null);
        }
    }

    private void Ended(Session session)
    {
        lock (gate)
        {
            if (ReferenceEquals(current, session))
            {
                current = null;
            }
        }
    }

    private sealed class Session(ShortcutRecorder owner)
    {
        private readonly TaskCompletionSource<string?> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly HashSet<ushort> heldModifiers = [];
        private LowLevelKeyboardProc? callback;
        private uint threadId;
        private volatile bool stopped;

        public Task<string?> Result => result.Task;

        public void Start()
        {
            var ready = new ManualResetEventSlim();
            var thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "MakroPad shortcut recorder" };
            thread.Start();
            ready.Wait(TimeSpan.FromSeconds(2));
        }

        /// <summary>Ends the session from any thread.</summary>
        public void Stop(string? shortcut)
        {
            if (stopped)
            {
                return;
            }

            stopped = true;
            result.TrySetResult(shortcut);
            owner.Ended(this);
            if (threadId != 0)
            {
                PostThreadMessage(threadId, WmQuit, 0, 0);
            }
        }

        private void Run(ManualResetEventSlim ready)
        {
            threadId = GetCurrentThreadId();
            callback = OnKey;
            var hook = SetWindowsHookEx(WhKeyboardLl, callback, GetModuleHandle(null), 0);
            if (hook == 0)
            {
                var error = new Win32Exception(Marshal.GetLastWin32Error());
                owner.Trace?.Invoke($"Tastatur-Hook nicht installiert: {error.Message}");
                stopped = true;
                result.TrySetException(error);
                owner.Ended(this);
                ready.Set();
                return;
            }

            owner.Trace?.Invoke("Aufnahme gestartet, Tastatur-Hook aktiv");
            ready.Set();
            try
            {
                while (!stopped && GetMessage(out var message, 0, 0, 0) > 0)
                {
                    TranslateMessage(ref message);
                    DispatchMessage(ref message);
                }
            }
            finally
            {
                UnhookWindowsHookEx(hook);
                GC.KeepAlive(callback);
            }
        }

        private nint OnKey(int code, nint message, nint data)
        {
            if (code < 0 || stopped)
            {
                return CallNextHookEx(0, code, message, data);
            }

            // Injected keys are recorded too: keyboard software often re-injects physical presses.
            var key = (ushort)Marshal.ReadInt32(data);
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
                owner.Trace?.Invoke($"Taste 0x{key:X2} gedrückt, Modifikatoren: {heldModifiers.Count}");
                if (key == Escape && heldModifiers.Count == 0)
                {
                    owner.Trace?.Invoke("Esc: Aufnahme abgebrochen");
                    Stop(null);
                }
                else if (ShortcutParser.Format(heldModifiers, key) is { } shortcut)
                {
                    owner.Trace?.Invoke($"Aufgenommen: {shortcut}");
                    Stop(shortcut);
                }
            }

            return 1;
        }
    }

    private delegate nint LowLevelKeyboardProc(int code, nint message, nint data);

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public nint Window;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hookId, LowLevelKeyboardProc callback, nint module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Msg message, nint window, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Msg message);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref Msg message);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint threadId, uint message, nint wParam, nint lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}
