using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
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

    public Task<IReadOnlyList<ApplicationAudioSession>> GetAudioSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = new List<ApplicationAudioSession>();

        try
        {
            using var deviceEnumerator = new MMDeviceEnumerator();
            using var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;

            for (var index = 0; index < sessions.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var session = sessions[index];
                var processId = (int)session.GetProcessID;
                if (processId <= 0)
                {
                    continue;
                }

                var processName = TryGetProcessName(processId);
                if (string.IsNullOrWhiteSpace(processName))
                {
                    continue;
                }

                var volume = session.SimpleAudioVolume;
                result.Add(new ApplicationAudioSession(
                    processName,
                    processId,
                    string.IsNullOrWhiteSpace(session.DisplayName) ? processName : session.DisplayName,
                    (int)Math.Round(volume.Volume * 100),
                    volume.Mute,
                    session.State == AudioSessionState.AudioSessionStateActive));
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
            return Task.FromResult<IReadOnlyList<ApplicationAudioSession>>([]);
        }

        return Task.FromResult<IReadOnlyList<ApplicationAudioSession>>(
            result
                .OrderBy(session => session.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(session => session.ProcessId)
                .ToArray());
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

            var processName = TryGetProcessName(processId);
            return NormalizeProcessName(processName)
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

    private static string TryGetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return "";
        }
    }
}
