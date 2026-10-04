using System.Runtime.InteropServices;
using MakroPad.Core.Actions;
using MakroPad.Core.Configuration;
using MakroPad.Core.Device;
using NAudio.CoreAudioApi;

namespace MakroPad.Windows;

/// <summary><c>app.volume</c>: sets the volume of every audio session of one process (<c>process=Spotify</c>).</summary>
public sealed class AppVolumeAction : IActionHandler
{
    public string ActionId => "app.volume";

    public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        if (context.Input is not SliderInput slider)
        {
            return Task.FromResult(ActionResult.Failed("app.volume braucht einen Slider"));
        }

        var process = ConfigEditor.NormalizeProcess(context.Binding.GetParameter("process") ?? "");
        if (process.Length == 0)
        {
            return Task.FromResult(ActionResult.Failed("app.volume: Parameter process fehlt"));
        }

        return Task.FromResult(AudioSessions.SetVolume(
            name => string.Equals(name, process, StringComparison.OrdinalIgnoreCase),
            slider.Percent,
            matched => matched.Count == 0 ? ActionResult.Failed($"{process} spielt nichts ab") : ActionResult.Ok(process, slider.Percent)));
    }

    /// <summary>Process names of all current audio sessions (for editors and diagnostics).</summary>
    public static IReadOnlyList<string> ListSessionProcesses() => AudioSessions.ListProcesses();
}

/// <summary><c>app.volume.foreground</c>: volume of whatever program is in the foreground.</summary>
public sealed class ForegroundVolumeAction : IActionHandler
{
    public string ActionId => "app.volume.foreground";

    public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        if (context.Input is not SliderInput slider)
        {
            return Task.FromResult(ActionResult.Failed("Braucht einen Slider"));
        }

        var foreground = ForegroundWatcher.ForegroundProcessName();
        if (foreground is null)
        {
            return Task.FromResult(ActionResult.Failed("Kein Programm im Vordergrund"));
        }

        return Task.FromResult(AudioSessions.SetVolume(
            name => string.Equals(name, foreground, StringComparison.OrdinalIgnoreCase),
            slider.Percent,
            matched => matched.Count == 0 ? ActionResult.Failed($"{foreground} spielt nichts ab") : ActionResult.Ok(foreground, slider.Percent)));
    }
}

/// <summary><c>app.volume.others</c>: volume of every program except the one in the foreground.</summary>
public sealed class OtherAppsVolumeAction : IActionHandler
{
    public string ActionId => "app.volume.others";

    public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        if (context.Input is not SliderInput slider)
        {
            return Task.FromResult(ActionResult.Failed("Braucht einen Slider"));
        }

        var foreground = ForegroundWatcher.ForegroundProcessName() ?? "";
        return Task.FromResult(AudioSessions.SetVolume(
            name => !string.Equals(name, foreground, StringComparison.OrdinalIgnoreCase),
            slider.Percent,
            matched => matched.Count == 0
                ? ActionResult.Failed("Kein anderes Programm spielt etwas ab")
                : ActionResult.Ok(matched.Count == 1 ? matched[0] : $"{matched.Count} Programme", slider.Percent)));
    }
}

/// <summary>Audio sessions of the default output device, matched by process name.</summary>
internal static class AudioSessions
{
    public static ActionResult SetVolume(Func<string, bool> matches, int percent, Func<IReadOnlyList<string>, ActionResult> describe)
    {
        try
        {
            var matched = new List<string>();
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;
            for (var index = 0; index < sessions.Count; index++)
            {
                using var session = sessions[index];
                var name = ProcessNameOf(session);
                if (name.Length == 0 || !matches(name))
                {
                    continue;
                }

                session.SimpleAudioVolume.Volume = Math.Clamp(percent, 0, 100) / 100f;
                if (!matched.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    matched.Add(name);
                }
            }

            return describe(matched);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            return ActionResult.Failed($"Lautstärke: {ex.Message}");
        }
    }

    public static IReadOnlyList<string> ListProcesses()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < sessions.Count; index++)
            {
                using var session = sessions[index];
                if (ProcessNameOf(session) is { Length: > 0 } name)
                {
                    names.Add(name);
                }
            }

            return [.. names];
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            return [];
        }
    }

    private static string ProcessNameOf(AudioSessionControl session)
    {
        try
        {
            return ForegroundWatcher.ProcessNameOf((int)session.GetProcessID) ?? "";
        }
        catch (COMException)
        {
            return "";
        }
    }
}
