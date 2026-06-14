using System;
using System.Diagnostics;

namespace MakroPad.App.Services;

public sealed class ProcessPresenceProbe : IProcessPresenceProbe
{
    public bool IsRunning(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        var name = processName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        try
        {
            return Process.GetProcessesByName(name).Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
