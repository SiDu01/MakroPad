namespace MakroPad.Core.Configuration;

public sealed record AppSettingsConfig
{
    public bool AutoConnect { get; init; } = true;

    public bool EnableNowPlayingDisplay { get; init; } = true;

    public string? LastPort { get; init; }
}
