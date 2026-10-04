using Microsoft.Win32;

namespace MakroPad.Windows;

/// <summary>Per-user autostart via <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.</summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MakroPad";

    /// <summary>Adds or removes the entry. Returns false if the registry could not be written.</summary>
    public static bool Apply(bool enabled, string executablePath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            var command = $"\"{executablePath}\" --tray";
            if (enabled)
            {
                if (!string.Equals(key.GetValue(ValueName) as string, command, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue(ValueName, command);
                }
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName);
            }

            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return false;
        }
    }
}
