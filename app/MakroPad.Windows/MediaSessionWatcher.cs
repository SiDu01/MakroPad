using MakroPad.Core.Runtime;
using Windows.Media.Control;

namespace MakroPad.Windows;

/// <summary>
/// Follows the media session Windows shows in its volume flyout (GSMTC). Works with any player
/// that reports to Windows: Spotify, browsers, VLC, the Media Player app …
/// </summary>
public sealed class MediaSessionWatcher : INowPlayingSource, IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(250);

    private readonly object gate = new();
    private readonly Timer refreshTimer;
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;
    private NowPlaying current = NowPlaying.None;

    private MediaSessionWatcher()
    {
        refreshTimer = new Timer(_ => _ = RefreshAsync(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public NowPlaying Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }

    public event Action<NowPlaying>? Changed;

    /// <summary>Starts watching; returns a watcher that reports nothing if the API is unavailable.</summary>
    public static async Task<MediaSessionWatcher> StartAsync()
    {
        var watcher = new MediaSessionWatcher();
        try
        {
            watcher.manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            watcher.manager.CurrentSessionChanged += (_, _) => watcher.Track(watcher.manager.GetCurrentSession());
            watcher.Track(watcher.manager.GetCurrentSession());
            await watcher.RefreshAsync();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            watcher.manager = null;
        }

        return watcher;
    }

    public void Dispose()
    {
        refreshTimer.Dispose();
        Track(null);
    }

    private void Track(GlobalSystemMediaTransportControlsSession? next)
    {
        lock (gate)
        {
            if (session is not null)
            {
                session.MediaPropertiesChanged -= OnSessionChanged;
                session.PlaybackInfoChanged -= OnSessionChanged;
            }

            session = next;
            if (session is not null)
            {
                session.MediaPropertiesChanged += OnSessionChanged;
                session.PlaybackInfoChanged += OnSessionChanged;
            }
        }

        ScheduleRefresh();
    }

    private void OnSessionChanged(GlobalSystemMediaTransportControlsSession sender, object args) => ScheduleRefresh();

    // Players often send several updates per track change (title first, artist later); wait for them to settle.
    private void ScheduleRefresh() => refreshTimer.Change(Debounce, Timeout.InfiniteTimeSpan);

    private async Task RefreshAsync()
    {
        GlobalSystemMediaTransportControlsSession? active;
        lock (gate)
        {
            active = session;
        }

        var next = NowPlaying.None;
        if (active is not null)
        {
            try
            {
                var properties = await active.TryGetMediaPropertiesAsync();
                var playback = active.GetPlaybackInfo();
                var status = playback?.PlaybackStatus;
                if (properties is not null && status is not GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed
                    and not GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped)
                {
                    var artist = string.IsNullOrWhiteSpace(properties.Artist) ? properties.AlbumArtist ?? "" : properties.Artist;
                    next = new NowPlaying(
                        (properties.Title ?? "").Trim(),
                        artist.Trim(),
                        status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                        SourceName(active.SourceAppUserModelId));
                }
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
            {
                next = NowPlaying.None;
            }
        }

        lock (gate)
        {
            if (next == current)
            {
                return;
            }

            current = next;
        }

        Changed?.Invoke(next);
    }

    /// <summary><c>Spotify.exe</c> or <c>308046B0AF4A39CB</c> style ids → something readable.</summary>
    private static string SourceName(string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return "";
        }

        var name = appId.Split('!')[0];
        name = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

        // Some browsers register a hashed id (e.g. F0DC299D809B9700) that means nothing to people.
        var isHash = name.Length >= 12 && name.All(Uri.IsHexDigit);
        return isHash ? "" : name.Split('.')[0];
    }
}
