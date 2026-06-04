using System;
using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;
using MakroPad.App.Services;
using MakroPad.Core.Configuration;

namespace MakroPad.App.ViewModels;

public partial class DisplaySettingsViewModel : ViewModelBase
{
    private readonly DeviceDisplaySyncService deviceDisplaySyncService;
    private string activeProfileName = "Default";
    private string connectionStatus = "Disconnected";
    private NowPlayingTrack? nowPlayingTrack;

    public DisplaySettingsViewModel(DeviceDisplaySyncService deviceDisplaySyncService)
    {
        this.deviceDisplaySyncService = deviceDisplaySyncService;
    }

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

    [ObservableProperty]
    private string defaultScreenTitle = "MakroPad";

    partial void OnDefaultScreenTitleChanged(string value) => NotifyChanged();

    [ObservableProperty]
    private string defaultScreenLine1 = "{profile}";

    partial void OnDefaultScreenLine1Changed(string value) => NotifyChanged();

    [ObservableProperty]
    private string defaultScreenLine2 = "{status}";

    partial void OnDefaultScreenLine2Changed(string value) => NotifyChanged();

    [ObservableProperty]
    private string defaultScreenLine3 = "";

    partial void OnDefaultScreenLine3Changed(string value) => NotifyChanged();

    [ObservableProperty]
    private string defaultScreenLine4 = "";

    partial void OnDefaultScreenLine4Changed(string value) => NotifyChanged();

    [ObservableProperty]
    private int defaultScreenRows = 3;

    partial void OnDefaultScreenRowsChanged(int value)
    {
        DefaultScreenRows = OledDefaultScreenRenderer.NormalizeRows(value);
        OnPropertyChanged(nameof(UsesFiveLineOledLayout));
        NotifyChanged();
    }

    public IReadOnlyList<int> AvailableOledLayoutRows { get; } = [3, 5];

    public bool UsesFiveLineOledLayout => DefaultScreenRows == 5;

    public string OledPreviewTitle => RenderOledPreview().Title;

    public string OledPreviewLine1 => RenderOledPreview().Line1;

    public string OledPreviewLine2 => RenderOledPreview().Line2;

    public string OledPreviewLine3 => RenderOledPreview().Line3;

    public string OledPreviewLine4 => RenderOledPreview().Line4;

    public void Load(MakroPadConfig config)
    {
        EnableNowPlayingDisplay = config.AppSettings.EnableNowPlayingDisplay;
        VolumeSliderDeadbandPercent = NormalizeVolumeSliderDeadbandPercent(config.AppSettings.VolumeSliderDeadbandPercent);
        ActionResultTimeoutMs = Math.Max(0, config.Hardware.Display.ActionResultTimeoutMs);
        DefaultScreenRows = OledDefaultScreenRenderer.NormalizeRows(config.Hardware.Display.DefaultLayoutRows);
        DefaultScreenTitle = config.Hardware.Display.DefaultTitle;
        DefaultScreenLine1 = config.Hardware.Display.DefaultLine1;
        DefaultScreenLine2 = config.Hardware.Display.DefaultLine2;
        DefaultScreenLine3 = config.Hardware.Display.DefaultLine3;
        DefaultScreenLine4 = config.Hardware.Display.DefaultLine4;
        RefreshPreview();
    }

    public MakroPadConfig ApplyTo(MakroPadConfig config, string? selectedPort)
    {
        return config with
        {
            Hardware = config.Hardware with
            {
                Display = config.Hardware.Display with
                {
                    ActionResultTimeoutMs = Math.Max(0, ActionResultTimeoutMs),
                    DefaultLayoutRows = OledDefaultScreenRenderer.NormalizeRows(DefaultScreenRows),
                    DefaultTitle = OledDefaultScreenRenderer.NormalizeLine(DefaultScreenTitle, "MakroPad"),
                    DefaultLine1 = OledDefaultScreenRenderer.NormalizeLine(DefaultScreenLine1, "{profile}"),
                    DefaultLine2 = OledDefaultScreenRenderer.NormalizeLine(DefaultScreenLine2, "{status}"),
                    DefaultLine3 = DefaultScreenLine3.Trim(),
                    DefaultLine4 = DefaultScreenLine4.Trim()
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

    public void SetRuntime(
        string activeProfileName,
        string connectionStatus,
        NowPlayingTrack? nowPlayingTrack)
    {
        this.activeProfileName = activeProfileName;
        this.connectionStatus = connectionStatus;
        this.nowPlayingTrack = nowPlayingTrack;
        RefreshPreview();
    }

    public bool UsesMediaPlaceholder()
    {
        return deviceDisplaySyncService.UsesMediaPlaceholder(CreateDeviceDisplayState());
    }

    public DeviceDisplayState CreateDeviceDisplayState()
    {
        return new DeviceDisplayState(
            activeProfileName,
            connectionStatus,
            ActionResultTimeoutMs,
            DefaultScreenRows,
            DefaultScreenTitle,
            DefaultScreenLine1,
            DefaultScreenLine2,
            DefaultScreenLine3,
            DefaultScreenLine4,
            EnableNowPlayingDisplay,
            nowPlayingTrack);
    }

    private OledDefaultScreenPreview RenderOledPreview()
    {
        return deviceDisplaySyncService.RenderPreview(CreateDeviceDisplayState());
    }

    private void NotifyChanged()
    {
        RefreshPreview();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshPreview()
    {
        OnPropertyChanged(nameof(OledPreviewTitle));
        OnPropertyChanged(nameof(OledPreviewLine1));
        OnPropertyChanged(nameof(OledPreviewLine2));
        OnPropertyChanged(nameof(OledPreviewLine3));
        OnPropertyChanged(nameof(OledPreviewLine4));
    }

    private static int NormalizeVolumeSliderDeadbandPercent(int value)
    {
        return Math.Clamp(value, 0, 25);
    }
}
