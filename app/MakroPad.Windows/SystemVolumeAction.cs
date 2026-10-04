using System.Runtime.InteropServices;
using MakroPad.Core.Actions;
using MakroPad.Core.Device;
using NAudio.CoreAudioApi;

namespace MakroPad.Windows;

/// <summary><c>system.volume</c>: sets the default output device's master volume from a slider.</summary>
public sealed class SystemVolumeAction : IActionHandler
{
    public string ActionId => "system.volume";

    public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Input is not SliderInput slider)
        {
            return Task.FromResult(ActionResult.Failed("system.volume braucht einen Slider"));
        }

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.MasterVolumeLevelScalar = slider.Percent / 100f;
            return Task.FromResult(ActionResult.Ok("System", slider.Percent));
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            return Task.FromResult(ActionResult.Failed($"Lautstärke: {ex.Message}"));
        }
    }
}
