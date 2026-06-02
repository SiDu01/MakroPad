using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MakroPad.App.Services;
using MakroPad.Core.Protocol;
using MakroPad.Device;

namespace MakroPad.App.ViewModels;

public partial class NowPlayingViewModel : ViewModelBase
{
    private readonly INowPlayingProvider nowPlayingProvider;

    [ObservableProperty]
    private string nowPlayingStatus = "Now Playing idle";

    public NowPlayingTrack? LastSentTrack { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public NowPlayingViewModel(INowPlayingProvider nowPlayingProvider)
    {
        this.nowPlayingProvider = nowPlayingProvider;
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;

        if (!isEnabled)
        {
            LastSentTrack = null;
            TrackChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task RunLoopAsync(
        Func<IMakroPadConnection?> getConnection,
        Func<bool> defaultScreenUsesMediaPlaceholder,
        Func<Task> sendDeviceConfigAsync,
        Action<string> addLog,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(cancellationToken);
                await UpdateAsync(
                    getConnection,
                    defaultScreenUsesMediaPlaceholder,
                    sendDeviceConfigAsync,
                    addLog,
                    cancellationToken);
                await sendDeviceConfigAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await SetStatusAsync($"Now Playing failed: {ex.Message}");
            }
        }
    }

    private async Task UpdateAsync(
        Func<IMakroPadConnection?> getConnection,
        Func<bool> defaultScreenUsesMediaPlaceholder,
        Func<Task> sendDeviceConfigAsync,
        Action<string> addLog,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            LastSentTrack = null;
            await SetStatusAsync("Now Playing disabled");
            return;
        }

        var connection = getConnection();
        if (connection is null || connection.State != DeviceConnectionState.Connected)
        {
            await SetStatusAsync("Now Playing waiting for device");
            return;
        }

        var track = await nowPlayingProvider.GetCurrentTrackAsync(cancellationToken);
        if (track is null)
        {
            if (LastSentTrack is not null)
            {
                LastSentTrack = null;
                TrackChanged?.Invoke(this, EventArgs.Empty);
                await sendDeviceConfigAsync();
            }

            await SetStatusAsync("No Spotify track detected");
            return;
        }

        await SetStatusAsync($"{track.Artist} - {track.Title}");

        if (track == LastSentTrack)
        {
            return;
        }

        var line = ProtocolCodec.Format(
            "display",
            new Dictionary<string, string?>
            {
                ["mode"] = "media",
                ["artist"] = track.Artist,
                ["title"] = track.Title,
                ["preferred"] = defaultScreenUsesMediaPlaceholder() ? "false" : "true"
            });

        try
        {
            await connection.SendLineAsync(line, cancellationToken);
            LastSentTrack = track;
            TrackChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => addLog($"Now Playing display failed: {ex.Message}"));
        }
    }

    private async Task SetStatusAsync(string status)
    {
        await Dispatcher.UIThread.InvokeAsync(() => NowPlayingStatus = status);
    }

    public event EventHandler? TrackChanged;
}
