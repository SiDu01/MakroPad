using MakroPad.Core.Actions;

namespace MakroPad.Core.Configuration;

/// <summary>User-authored configuration (<c>config.json</c>). The app never rewrites it on its own.</summary>
public sealed record MakroPadConfig
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public AppSettings Settings { get; init; } = new();

    public IReadOnlyList<ProfileConfig> Profiles { get; init; } = [];

    public AutoProfileSettings AutoProfile { get; init; } = new();

    public static MakroPadConfig CreateDefault() => new()
    {
        Profiles =
        [
            new ProfileConfig
            {
                Name = "Default",
                Bindings =
                [
                    new Binding("button:1", "media.playPause"),
                    new Binding("button:2", "media.previousTrack"),
                    new Binding("button:3", "media.nextTrack"),
                    new Binding("button:16", "profile.next"),
                    new Binding("slider:1", "system.volume")
                ]
            }
        ]
    };

    public ProfileConfig? FindProfile(string? name)
        => Profiles.FirstOrDefault(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase));
}

public sealed record ProfileConfig
{
    public string Name { get; init; } = "";

    public IReadOnlyList<Binding> Bindings { get; init; } = [];
}

public sealed record AppSettings
{
    /// <summary>Start hidden in the notification area.</summary>
    public bool StartInTray { get; init; }

    /// <summary>Register the app in the current user's Windows autostart.</summary>
    public bool StartWithWindows { get; init; }

    /// <summary>Minimum slider change in percent before a volume action runs again (0/100 % always pass).</summary>
    public int SliderDeadbandPercent { get; init; } = 2;

    public FeedbackSettings Feedback { get; init; } = new();

    /// <summary><c>system</c>, <c>light</c> or <c>dark</c>.</summary>
    public string Theme { get; init; } = "system";
}

/// <summary>Which events show a short overlay on the OLED.</summary>
public sealed record FeedbackSettings
{
    public bool Actions { get; init; } = true;

    public bool Volume { get; init; } = true;

    public bool Errors { get; init; } = true;

    public bool ProfileChange { get; init; } = true;

    /// <summary>Show title and artist briefly when the playing track changes.</summary>
    public bool TrackChange { get; init; } = true;
}

/// <summary>Switches the profile when another program comes to the foreground.</summary>
public sealed record AutoProfileSettings
{
    public bool Enabled { get; init; } = true;

    public IReadOnlyList<ProfileRule> Rules { get; init; } = [];

    /// <summary>Profile for programs without a rule; null keeps the current profile.</summary>
    public string? FallbackProfile { get; init; }
}

/// <summary><see cref="Process"/> is the process name without <c>.exe</c>, e.g. <c>Discord</c>.</summary>
public sealed record ProfileRule(string Process, string Profile);

/// <summary>App-owned runtime state (<c>state.json</c>), kept apart from the user's config.</summary>
public sealed record AppState
{
    public string? ActiveProfile { get; init; }

    public string? LastPort { get; init; }
}
