using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MakroPad.Core.Runtime;

namespace MakroPad.Windows;

/// <summary>Polls which process owns the foreground window (twice a second is plenty for profile switching).</summary>
public sealed class ForegroundWatcher : IForegroundSource, IDisposable
{
    private const int RecentLimit = 12;

    private readonly Timer timer;
    private readonly object gate = new();
    private readonly List<string> recent = [];
    private string? current;

    public ForegroundWatcher(TimeSpan? interval = null)
    {
        var period = interval ?? TimeSpan.FromMilliseconds(500);
        timer = new Timer(_ => Poll(), null, TimeSpan.Zero, period);
    }

    public string? Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }

    public IReadOnlyList<string> Recent
    {
        get
        {
            lock (gate)
            {
                return [.. recent];
            }
        }
    }

    public event Action<string>? Changed;

    public void Dispose() => timer.Dispose();

    /// <summary>Process name of the foreground window, or null (e.g. while the desktop is locked).</summary>
    public static string? ForegroundProcessName() => ProcessNameOf(ForegroundProcessId());

    public static int ForegroundProcessId()
    {
        var window = GetForegroundWindow();
        if (window == 0)
        {
            return 0;
        }

        GetWindowThreadProcessId(window, out var processId);
        return (int)processId;
    }

    public static string? ProcessNameOf(int processId)
    {
        if (processId <= 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }

    private void Poll()
    {
        var name = ForegroundProcessName();
        if (name is null)
        {
            return;
        }

        lock (gate)
        {
            if (string.Equals(name, current, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            current = name;
            recent.RemoveAll(r => string.Equals(r, name, StringComparison.OrdinalIgnoreCase));
            recent.Insert(0, name);
            if (recent.Count > RecentLimit)
            {
                recent.RemoveAt(recent.Count - 1);
            }
        }

        Changed?.Invoke(name);
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
