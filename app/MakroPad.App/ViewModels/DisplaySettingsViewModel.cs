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
    private int defaultScreenTitleSize = 1;

    partial void OnDefaultScreenTitleSizeChanged(int value) => NormalizeTextSizeProperty(value, v => DefaultScreenTitleSize = v);

    [ObservableProperty]
    private string defaultScreenLine1 = "{profile}";

    partial void OnDefaultScreenLine1Changed(string value) => NotifyChanged();

    [ObservableProperty]
    private int defaultScreenLine1Size = 1;

    partial void OnDefaultScreenLine1SizeChanged(int value) => NormalizeTextSizeProperty(value, v => DefaultScreenLine1Size = v);

    [ObservableProperty]
    private string defaultScreenLine2 = "{status}";

    partial void OnDefaultScreenLine2Changed(string value) => NotifyChanged();

    [ObservableProperty]
    private int defaultScreenLine2Size = 1;

    partial void OnDefaultScreenLine2SizeChanged(int value) => NormalizeTextSizeProperty(value, v => DefaultScreenLine2Size = v);

    [ObservableProperty]
    private string defaultScreenLine3 = "";

    partial void OnDefaultScreenLine3Changed(string value) => NotifyChanged();

    [ObservableProperty]
    private int defaultScreenLine3Size = 1;

    partial void OnDefaultScreenLine3SizeChanged(int value) => NormalizeTextSizeProperty(value, v => DefaultScreenLine3Size = v);

    [ObservableProperty]
    private string defaultScreenLine4 = "";

    partial void OnDefaultScreenLine4Changed(string value) => NotifyChanged();

    [ObservableProperty]
    private int defaultScreenLine4Size = 1;

    partial void OnDefaultScreenLine4SizeChanged(int value) => NormalizeTextSizeProperty(value, v => DefaultScreenLine4Size = v);

    [ObservableProperty]
    private int defaultScreenRows = 3;

    partial void OnDefaultScreenRowsChanged(int value)
    {
        DefaultScreenRows = OledDefaultScreenRenderer.NormalizeRows(value);
        OnPropertyChanged(nameof(UsesFiveLineOledLayout));
        NotifyChanged();
    }

    public IReadOnlyList<int> AvailableOledLayoutRows { get; } = [3, 5];

    public IReadOnlyList<string> AvailableOledTemplateModes { get; } = ["normal", "html"];

    public IReadOnlyList<int> AvailableOledTextSizes { get; } = [1, 2];

    [ObservableProperty]
    private string defaultScreenTemplateMode = "normal";

    partial void OnDefaultScreenTemplateModeChanged(string value)
    {
        var normalized = OledDefaultScreenRenderer.NormalizeTemplateMode(value);
        if (normalized != value)
        {
            DefaultScreenTemplateMode = normalized;
            return;
        }

        NotifyChanged();
    }

    public bool UsesFiveLineOledLayout => DefaultScreenRows == 5;

    public string OledPreviewTitle => RenderOledPreview().Title;

    public int OledPreviewTitleFontSize => PreviewFontSize(DefaultScreenTitleSize);

    public string OledPreviewLine1 => RenderOledPreview().Line1;

    public int OledPreviewLine1FontSize => PreviewFontSize(DefaultScreenLine1Size);

    public string OledPreviewLine2 => RenderOledPreview().Line2;

    public int OledPreviewLine2FontSize => PreviewFontSize(DefaultScreenLine2Size);

    public string OledPreviewLine3 => RenderOledPreview().Line3;

    public int OledPreviewLine3FontSize => PreviewFontSize(DefaultScreenLine3Size);

    public string OledPreviewLine4 => RenderOledPreview().Line4;

    public int OledPreviewLine4FontSize => PreviewFontSize(DefaultScreenLine4Size);

    public void Load(MakroPadConfig config)
    {
        EnableNowPlayingDisplay = config.AppSettings.EnableNowPlayingDisplay;
        VolumeSliderDeadbandPercent = NormalizeVolumeSliderDeadbandPercent(config.AppSettings.VolumeSliderDeadbandPercent);
        ActionResultTimeoutMs = Math.Max(0, config.Hardware.Display.ActionResultTimeoutMs);
        DefaultScreenRows = OledDefaultScreenRenderer.NormalizeRows(config.Hardware.Display.DefaultLayoutRows);
        DefaultScreenTemplateMode = OledDefaultScreenRenderer.NormalizeTemplateMode(config.Hardware.Display.DefaultTemplateMode);
        DefaultScreenTitle = config.Hardware.Display.DefaultTitle;
        DefaultScreenTitleSize = OledDefaultScreenRenderer.NormalizeTextSize(config.Hardware.Display.DefaultTitleSize);
        DefaultScreenLine1 = config.Hardware.Display.DefaultLine1;
        DefaultScreenLine1Size = OledDefaultScreenRenderer.NormalizeTextSize(config.Hardware.Display.DefaultLine1Size);
        DefaultScreenLine2 = config.Hardware.Display.DefaultLine2;
        DefaultScreenLine2Size = OledDefaultScreenRenderer.NormalizeTextSize(config.Hardware.Display.DefaultLine2Size);
        DefaultScreenLine3 = config.Hardware.Display.DefaultLine3;
        DefaultScreenLine3Size = OledDefaultScreenRenderer.NormalizeTextSize(config.Hardware.Display.DefaultLine3Size);
        DefaultScreenLine4 = config.Hardware.Display.DefaultLine4;
        DefaultScreenLine4Size = OledDefaultScreenRenderer.NormalizeTextSize(config.Hardware.Display.DefaultLine4Size);
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
                    DefaultTemplateMode = OledDefaultScreenRenderer.NormalizeTemplateMode(DefaultScreenTemplateMode),
                    DefaultTitle = OledDefaultScreenRenderer.NormalizeLine(DefaultScreenTitle, "MakroPad"),
                    DefaultTitleSize = OledDefaultScreenRenderer.NormalizeTextSize(DefaultScreenTitleSize),
                    DefaultLine1 = OledDefaultScreenRenderer.NormalizeLine(DefaultScreenLine1, "{profile}"),
                    DefaultLine1Size = OledDefaultScreenRenderer.NormalizeTextSize(DefaultScreenLine1Size),
                    DefaultLine2 = OledDefaultScreenRenderer.NormalizeLine(DefaultScreenLine2, "{status}"),
                    DefaultLine2Size = OledDefaultScreenRenderer.NormalizeTextSize(DefaultScreenLine2Size),
                    DefaultLine3 = DefaultScreenLine3.Trim(),
                    DefaultLine3Size = OledDefaultScreenRenderer.NormalizeTextSize(DefaultScreenLine3Size),
                    DefaultLine4 = DefaultScreenLine4.Trim(),
                    DefaultLine4Size = OledDefaultScreenRenderer.NormalizeTextSize(DefaultScreenLine4Size)
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
            DefaultScreenTemplateMode,
            DefaultScreenTitle,
            DefaultScreenTitleSize,
            DefaultScreenLine1,
            DefaultScreenLine1Size,
            DefaultScreenLine2,
            DefaultScreenLine2Size,
            DefaultScreenLine3,
            DefaultScreenLine3Size,
            DefaultScreenLine4,
            DefaultScreenLine4Size,
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
        OnPropertyChanged(nameof(OledPreviewTitleFontSize));
        OnPropertyChanged(nameof(OledPreviewLine1));
        OnPropertyChanged(nameof(OledPreviewLine1FontSize));
        OnPropertyChanged(nameof(OledPreviewLine2));
        OnPropertyChanged(nameof(OledPreviewLine2FontSize));
        OnPropertyChanged(nameof(OledPreviewLine3));
        OnPropertyChanged(nameof(OledPreviewLine3FontSize));
        OnPropertyChanged(nameof(OledPreviewLine4));
        OnPropertyChanged(nameof(OledPreviewLine4FontSize));
    }

    private static int NormalizeVolumeSliderDeadbandPercent(int value)
    {
        return Math.Clamp(value, 0, 25);
    }

    private void NormalizeTextSizeProperty(int value, Action<int> assign)
    {
        var normalized = OledDefaultScreenRenderer.NormalizeTextSize(value);
        if (normalized != value)
        {
            assign(normalized);
            return;
        }

        NotifyChanged();
    }

    private static int PreviewFontSize(int textSize)
    {
        return OledDefaultScreenRenderer.NormalizeTextSize(textSize) * 16;
    }
}
