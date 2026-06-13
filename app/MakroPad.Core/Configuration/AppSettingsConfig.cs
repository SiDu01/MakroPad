namespace MakroPad.Core.Configuration;

public sealed record AppSettingsConfig
{
    public bool AutoConnect { get; init; } = true;

    public bool EnableNowPlayingDisplay { get; init; } = true;

    public int VolumeSliderDeadbandPercent { get; init; } = 2;

    public int ButtonRows { get; init; } = 4;

    public int ButtonColumns { get; init; } = 5;

    public int SliderCount { get; init; } = 3;

    public List<DisplayTriggerConfig> DisplayTriggers { get; init; } = [];

    public bool DisplayTriggerDefaultsInitialized { get; init; }

    public bool MediaTriggerDefaultsInitialized { get; init; }

    public string? LastPort { get; init; }
}
