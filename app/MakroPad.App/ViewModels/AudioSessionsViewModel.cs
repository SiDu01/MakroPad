using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.Actions;

namespace MakroPad.App.ViewModels;

public partial class AudioSessionsViewModel : ViewModelBase
{
    private readonly IApplicationVolumeController applicationVolumeController;

    [ObservableProperty]
    private string audioSessionsStatus = "Audio sessions idle";

    public ObservableCollection<ApplicationAudioSession> AudioSessions { get; } = [];

    public ObservableCollection<string> AvailableAudioProcessNames { get; } = [];

    public AudioSessionsViewModel(IApplicationVolumeController applicationVolumeController)
    {
        this.applicationVolumeController = applicationVolumeController;
    }

    [RelayCommand]
    public async Task RefreshAudioSessionsAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => AudioSessionsStatus = "Refreshing audio sessions...");
        var sessions = await applicationVolumeController.GetAudioSessionsAsync();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            AudioSessions.Clear();
            AvailableAudioProcessNames.Clear();

            foreach (var session in sessions)
            {
                AudioSessions.Add(session);
            }

            foreach (var processName in sessions
                .Select(session => session.ProcessName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                AvailableAudioProcessNames.Add(processName);
            }

            AudioSessionsStatus = $"Audio sessions: {sessions.Count} found at {DateTime.Now:HH:mm:ss}";
            SessionsRefreshed?.Invoke(this, EventArgs.Empty);
        });
    }

    public async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(cancellationToken);
                await RefreshAudioSessionsAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() => AudioSessionsStatus = $"Audio sessions failed: {ex.Message}");
            }
        }
    }

    public event EventHandler? SessionsRefreshed;
}
