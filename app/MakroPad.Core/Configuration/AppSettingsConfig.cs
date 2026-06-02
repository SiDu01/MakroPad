namespace MakroPad.Core.Configuration;

public sealed record AppSettingsConfig
{
    public bool AutoConnect { get; init; } = true;

    public bool EnableNowPlayingDisplay { get; init; } = true;

    public int VolumeSliderDeadbandPercent { get; init; } = 2;

    public string? LastPort { get; init; }
}
