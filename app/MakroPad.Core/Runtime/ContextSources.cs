namespace MakroPad.Core.Runtime;

/// <summary>What a media player currently reports; <see cref="None"/> when nothing plays.</summary>
public sealed record NowPlaying(string Title, string Artist, bool IsPlaying, string Source)
{
    public static NowPlaying None { get; } = new("", "", false, "");

    public bool HasTrack => Title.Length > 0;

    /// <summary>Same track regardless of play/pause state.</summary>
    public bool IsSameTrack(NowPlaying other)
        => string.Equals(Title, other.Title, StringComparison.Ordinal) && string.Equals(Artist, other.Artist, StringComparison.Ordinal);
}

/// <summary>Reports the system-wide media session (Windows: GSMTC).</summary>
public interface INowPlayingSource
{
    NowPlaying Current { get; }

    /// <summary>Raised on any thread when title, artist or play state change.</summary>
    event Action<NowPlaying>? Changed;
}

/// <summary>Reports the process that owns the foreground window.</summary>
public interface IForegroundSource
{
    /// <summary>Process name without <c>.exe</c>, or null when unknown.</summary>
    string? Current { get; }

    /// <summary>Raised on any thread when another process comes to the foreground.</summary>
    event Action<string>? Changed;

    /// <summary>Distinct processes seen in the foreground recently, newest first.</summary>
    IReadOnlyList<string> Recent { get; }
}
