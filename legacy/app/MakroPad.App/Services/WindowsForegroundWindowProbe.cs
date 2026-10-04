using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MakroPad.App.Services;

/// <summary>
/// Win32-based foreground window probe. Reports the focused process name and whether its window
/// covers the whole monitor (fullscreen). Desktop/shell windows are treated as "no app" so the
/// idle desktop does not count as fullscreen. Returns an empty result on non-Windows.
/// </summary>
public sealed class WindowsForegroundWindowProbe : IForegroundWindowProbe
{
    private const uint MonitorDefaultToNearest = 0x00000002;

    public ForegroundWindowInfo Capture()
    {
        if (!OperatingSystem.IsWindows())
        {
            return default;
        }

        try
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero || IsShellWindow(window))
            {
                return default;
            }

            return new ForegroundWindowInfo(TryGetProcessName(window), IsFullscreen(window));
        }
        catch
        {
            return default;
        }
    }

    private static bool IsShellWindow(IntPtr window)
    {
        var className = new StringBuilder(256);
        if (GetClassName(window, className, className.Capacity) == 0)
        {
            return false;
        }

        var name = className.ToString();
        return name is "Progman" or "WorkerW";
    }

    private static string? TryGetProcessName(IntPtr window)
    {
        if (GetWindowThreadProcessId(window, out var processId) == 0 || processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsFullscreen(IntPtr window)
    {
        if (!GetWindowRect(window, out var windowRect))
        {
            return false;
        }

        var monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        var screen = info.Monitor;
        return windowRect.Left <= screen.Left
               && windowRect.Top <= screen.Top
               && windowRect.Right >= screen.Right
               && windowRect.Bottom >= screen.Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }
}
