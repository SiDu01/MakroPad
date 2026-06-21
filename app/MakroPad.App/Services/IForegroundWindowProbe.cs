namespace MakroPad.App.Services;

/// <summary>Foreground window snapshot: which app is focused and whether it covers the monitor.</summary>
public readonly record struct ForegroundWindowInfo(string? ProcessName, bool IsFullscreen);

/// <summary>Captures information about the current foreground window. Abstracted for testing.</summary>
public interface IForegroundWindowProbe
{
    ForegroundWindowInfo Capture();
}
