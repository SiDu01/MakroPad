using System;

using CommunityToolkit.Mvvm.ComponentModel;
using MakroPad.App.Services;
using MakroPad.Core.Configuration;

namespace MakroPad.App.ViewModels;

/// <summary>
/// Runtime display options that are not tied to a single idle screen: Now Playing,
/// the action-result timeout and the volume slider deadband. Idle screen content lives in
/// <see cref="IdleScreensEditorViewModel"/>.
/// </summary>
public partial class DisplaySettingsViewModel : ViewModelBase
{
    public event EventHandler? Changed;

    [ObservableProperty]
    private bool enableNowPlayingDisplay = true;

    partial void OnEnableNowPlayingDisplayChanged(bool value) => NotifyChanged();

    [ObservableProperty]
    private int volumeSliderDeadbandPercent = 2;

    partial void OnVolumeSliderDeadbandPercentChanged(int value)
    {
        var normalized = NormalizeVolumeSliderDeadbandPercent(value);
        if (normalized != value)
        {
            VolumeSliderDeadbandPercent = normalized;
            return;
        }

        NotifyChanged();
    }

    [ObservableProperty]
    private int actionResultTimeoutMs = 3000;

    partial void OnActionResultTimeoutMsChanged(int value) => NotifyChanged();

    public void Load(MakroPadConfig config)
    {
        EnableNowPlayingDisplay = config.AppSettings.EnableNowPlayingDisplay;
        VolumeSliderDeadbandPercent = NormalizeVolumeSliderDeadbandPercent(config.AppSettings.VolumeSliderDeadbandPercent);
        ActionResultTimeoutMs = Math.Max(0, config.Hardware.Display.ActionResultTimeoutMs);
    }

    public MakroPadConfig ApplyTo(MakroPadConfig config, string? selectedPort)
    {
        return config with
        {
            Hardware = config.Hardware with
            {
                Display = config.Hardware.Display with
                {
                    ActionResultTimeoutMs = Math.Max(0, ActionResultTimeoutMs)
                }
            },
            AppSettings = config.AppSettings with
            {
                EnableNowPlayingDisplay = EnableNowPlayingDisplay,
                VolumeSliderDeadbandPercent = NormalizeVolumeSliderDeadbandPercent(VolumeSliderDeadbandPercent),
                LastPort = selectedPort == DeviceSessionService.SimulatorPort
                    ? config.AppSettings.LastPort
                    : selectedPort
            }
        };
    }

    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static int NormalizeVolumeSliderDeadbandPercent(int value)
    {
        return Math.Clamp(value, 0, 25);
    }
}
