using NAudio.CoreAudioApi;
using System.Runtime.InteropServices;

namespace MakroPad.Actions;

internal sealed class WindowsSystemVolumeController : ISystemVolumeController
{
    public Task<ActionExecutionResult> SetMasterVolumeAsync(int percent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var deviceEnumerator = new MMDeviceEnumerator();
            using var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(percent, 0, 100) / 100f;

            return Task.FromResult(ActionExecutionResult.Ok($"Volume {percent}%"));
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
            return Task.FromResult(ActionExecutionResult.Failed($"Volume failed: {ex.Message}"));
        }
    }
}
