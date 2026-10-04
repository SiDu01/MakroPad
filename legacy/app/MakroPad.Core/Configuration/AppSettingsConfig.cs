namespace MakroPad.Core.Configuration;

public sealed record AppSettingsConfig
{
    public bool AutoConnect { get; init; } = true;

    /// <summary>When true, the app starts hidden in the system tray instead of showing its window.</summary>
    public bool StartMinimizedToTray { get; init; }

    public bool EnableNowPlayingDisplay { get; init; } = true;

    public int VolumeSliderDeadbandPercent { get; init; } = 2;

    public int ButtonRows { get; init; } = 4;

    public int ButtonColumns { get; init; } = 5;

    public int SliderCount { get; init; } = 3;

    public List<DisplayTriggerConfig> DisplayTriggers { get; init; } = [];

    public bool DisplayTriggerDefaultsInitialized { get; init; }

    public bool MediaTriggerDefaultsInitialized { get; init; }

    public List<IdleScreenConfig> IdleScreens { get; init; } = [];

    public bool IdleScreensInitialized { get; init; }

    public bool IdleScreensConditionsInitialized { get; init; }

    public int IdleRotationSeconds { get; init; } = 8;

    public string? LastPort { get; init; }
}
