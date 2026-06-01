using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MakroPad.App.Services;

public sealed class ProcessWindowNowPlayingProvider : INowPlayingProvider
{
    private static readonly string[] IgnoredSpotifyTitles =
    [
        "Spotify",
        "Spotify Free",
        "Spotify Premium",
        "Advertisement"
    ];

    public Task<NowPlayingTrack?> GetCurrentTrackAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<NowPlayingTrack?>(null);
        }

        foreach (var process in Process.GetProcessesByName("Spotify"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var title = process.MainWindowTitle?.Trim();
            var track = ParseSpotifyWindowTitle(title);
            if (track is not null)
            {
                return Task.FromResult<NowPlayingTrack?>(track);
            }
        }

        return Task.FromResult<NowPlayingTrack?>(null);
    }

    private static NowPlayingTrack? ParseSpotifyWindowTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)
            || IgnoredSpotifyTitles.Contains(title, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var separatorIndex = title.IndexOf(" - ", StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= title.Length - 3)
        {
            return null;
        }

        var artist = title[..separatorIndex].Trim();
        var trackTitle = title[(separatorIndex + 3)..].Trim();

        return string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(trackTitle)
            ? null
            : new NowPlayingTrack(artist, trackTitle);
    }
}
