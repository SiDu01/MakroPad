using System;
using System.Threading;
using System.Threading.Tasks;

using MakroPad.App.ViewModels;
using MakroPad.Device;

namespace MakroPad.App.Services;

public sealed class AppRuntimeLoopService : IDisposable
{
    private readonly CancellationTokenSource audioSessionsCancellation = new();
    private readonly CancellationTokenSource nowPlayingCancellation = new();
    private bool started;
    private bool disposed;

    public void Start(
        AudioSessionsViewModel audioSessionsViewModel,
        NowPlayingViewModel nowPlayingViewModel,
        Func<IMakroPadConnection?> getConnection,
        Func<bool> defaultScreenUsesMediaPlaceholder,
        Func<bool> hasMediaChangedDisplayTrigger,
        Func<NowPlayingTrack, CancellationToken, Task> handleMediaChangedDisplayAsync,
        Func<Task> sendDeviceConfigAsync,
        Action<string> addLog)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started)
        {
            return;
        }

        started = true;
        _ = audioSessionsViewModel.RefreshAudioSessionsAsync();
        _ = audioSessionsViewModel.RunRefreshLoopAsync(audioSessionsCancellation.Token);
        _ = nowPlayingViewModel.RunLoopAsync(
            getConnection,
            defaultScreenUsesMediaPlaceholder,
            hasMediaChangedDisplayTrigger,
            handleMediaChangedDisplayAsync,
            sendDeviceConfigAsync,
            addLog,
            nowPlayingCancellation.Token);
    }

    public void Stop()
    {
        if (disposed)
        {
            return;
        }

        audioSessionsCancellation.Cancel();
        nowPlayingCancellation.Cancel();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Stop();
        audioSessionsCancellation.Dispose();
        nowPlayingCancellation.Dispose();
        disposed = true;
    }
}
