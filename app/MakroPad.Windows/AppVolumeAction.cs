using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MakroPad.Core.Actions;
using MakroPad.Core.Device;
using NAudio.CoreAudioApi;

namespace MakroPad.Windows;

/// <summary><c>app.volume</c>: sets the volume of every audio session of one process (<c>process=Spotify</c>).</summary>
public sealed class AppVolumeAction : IActionHandler
{
    public string ActionId => "app.volume";

    public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Input is not SliderInput slider)
        {
            return Task.FromResult(ActionResult.Failed("app.volume braucht einen Slider"));
        }

        var process = NormalizeProcessName(context.Binding.GetParameter("process") ?? "");
        if (process.Length == 0)
        {
            return Task.FromResult(ActionResult.Failed("app.volume: Parameter process fehlt"));
        }

        try
        {
            var matched = 0;
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;
            for (var index = 0; index < sessions.Count; index++)
            {
                using var session = sessions[index];
                if (string.Equals(ProcessNameOf(session), process, StringComparison.OrdinalIgnoreCase))
                {
                    session.SimpleAudioVolume.Volume = slider.Percent / 100f;
                    matched++;
                }
            }

            return Task.FromResult(matched == 0
                ? ActionResult.Failed($"{process} spielt nichts ab")
                : ActionResult.Ok(process, slider.Percent));
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            return Task.FromResult(ActionResult.Failed($"{process}: {ex.Message}"));
        }
    }

    /// <summary>Process names of all current audio sessions (for editors and diagnostics).</summary>
    public static IReadOnlyList<string> ListSessionProcesses()
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

    private static string NormalizeProcessName(string name)
    {
        var trimmed = name.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? trimmed[..^4] : trimmed;
    }

    private static string ProcessNameOf(AudioSessionControl session)
    {
        try
        {
            var processId = (int)session.GetProcessID;
            if (processId <= 0)
            {
                return "";
            }

            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return "";
        }
    }
}
