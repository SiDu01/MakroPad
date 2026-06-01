using NAudio.CoreAudioApi;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MakroPad.Actions;

internal sealed class WindowsApplicationVolumeController : IApplicationVolumeController
{
    public Task<ActionExecutionResult> SetApplicationVolumeAsync(
        string processName,
        int percent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedProcessName = NormalizeProcessName(processName);
        if (string.IsNullOrWhiteSpace(normalizedProcessName))
        {
            return Task.FromResult(ActionExecutionResult.Failed(
                "App volume process parameter is required"));
        }

        try
        {
            using var deviceEnumerator = new MMDeviceEnumerator();
            using var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;
            var matchedSessions = 0;
            var volumeScalar = Math.Clamp(percent, 0, 100) / 100f;

            for (var index = 0; index < sessions.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var session = sessions[index];

                if (!SessionMatchesProcess(session, normalizedProcessName))
                {
                    continue;
                }

                session.SimpleAudioVolume.Volume = volumeScalar;
                matchedSessions++;
            }

            return matchedSessions == 0
                ? Task.FromResult(ActionExecutionResult.Failed(
                    $"No audio session found for {normalizedProcessName}"))
                : Task.FromResult(ActionExecutionResult.Ok(
                    $"Volume {percent}% for {normalizedProcessName} ({matchedSessions} session{(matchedSessions == 1 ? "" : "s")})"));
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
            return Task.FromResult(ActionExecutionResult.Failed($"Application volume failed: {ex.Message}"));
        }
    }

    private static bool SessionMatchesProcess(AudioSessionControl session, string normalizedProcessName)
    {
        try
        {
            var processId = (int)session.GetProcessID;
            if (processId <= 0)
            {
                return false;
            }

            using var process = Process.GetProcessById(processId);
            return NormalizeProcessName(process.ProcessName)
                .Equals(normalizedProcessName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    private static string NormalizeProcessName(string processName)
    {
        var trimmed = processName.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }
}
