using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.App.Services;
using MakroPad.Core.Actions;
using MakroPad.Core.Device;
using MakroPad.Core.Runtime;
using MakroPad.Device;

namespace MakroPad.App.ViewModels;

public enum LogKind
{
    FromDevice,
    ToDevice,
    Action,
    Info,
    Warning
}

public sealed record LogEntry(DateTime Time, LogKind Kind, string Text)
{
    public string TimeText => Time.ToString("HH:mm:ss.fff");

    public string KindText => Kind switch
    {
        LogKind.FromDevice => "Gerät",
        LogKind.ToDevice => "App",
        LogKind.Action => "Aktion",
        LogKind.Warning => "Hinweis",
        _ => "Info"
    };
}

/// <summary>Connection facts, config problems, audio sessions, the raw protocol and the simulator.</summary>
public sealed partial class DiagnosticsViewModel : ObservableObject
{
    private const int MaxEntries = 500;

    private readonly MakroPadRuntime runtime;
    private readonly ConfigSession session;
    private readonly SimulatedDevice? simulator;
    private readonly Func<IReadOnlyList<string>> listAudioSessions;
    private readonly List<LogEntry> all = [];

    [ObservableProperty]
    private bool showDevice = true;

    [ObservableProperty]
    private bool showApp = true;

    [ObservableProperty]
    private bool showActions = true;

    [ObservableProperty]
    private int simulatorSlider;

    public DiagnosticsViewModel(MakroPadRuntime runtime, ConfigSession session, SimulatedDevice? simulator, Func<IReadOnlyList<string>> listAudioSessions)
    {
        this.runtime = runtime;
        this.session = session;
        this.simulator = simulator;
        this.listAudioSessions = listAudioSessions;

        runtime.LineLogged += (direction, line) => Post(() => Add(direction == LineDirection.FromDevice ? LogKind.FromDevice : LogKind.ToDevice, line));
        runtime.ActionCompleted += (binding, result) => Post(() =>
            Add(result.Success ? LogKind.Action : LogKind.Warning, $"{binding.Input} {binding.Action}: {result.Message}"));
        runtime.StatusChanged += _ => Post(RefreshConnection);
        session.Changed += external =>
        {
            if (external)
            {
                Add(LogKind.Info, "config.json wurde außerhalb der App geändert und neu geladen");
            }

            RefreshProblems();
            OnPropertyChanged(nameof(IsDebugMode));
        };

        foreach (var notice in session.Service.Notices)
        {
            Add(LogKind.Info, notice);
        }

        RefreshConnection();
        RefreshProblems();
        RefreshAudio();
    }

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public ObservableCollection<string> ConfigProblems { get; } = [];

    public bool HasConfigProblems => ConfigProblems.Count > 0;

    public ObservableCollection<string> AudioSessions { get; } = [];

    public bool HasAudioSessions => AudioSessions.Count > 0;

    public bool IsSimulator => simulator is not null;

    public bool IsDebugMode => session.Config.Settings.DebugMode;

    public IReadOnlyList<int> SimulatorButtons { get; } = Enumerable.Range(1, SimulatedDevice.Buttons).ToArray();

    public string Port => runtime.Endpoint ?? "–";

    public string Firmware => runtime.Device?.Firmware ?? "–";

    public string Protocol => runtime.Device is { } device ? device.ProtocolVersion.ToString() : "–";

    public string Hardware => runtime.Device is { } device ? $"{device.Buttons} Tasten, {device.Sliders} Slider" : "–";

    public string ConfigPath => session.Service.ConfigPath;

    [ObservableProperty]
    private string nowPlayingText = "–";

    [ObservableProperty]
    private string foregroundText = "–";

    public void Attach(INowPlayingSource media)
    {
        void Show(NowPlaying track) => NowPlayingText = !track.HasTrack
            ? "Nichts"
            : $"{(track.Artist.Length > 0 ? track.Artist + " – " : "")}{track.Title}{(track.IsPlaying ? "" : " (pausiert)")}{(track.Source.Length > 0 ? $", über {track.Source}" : "")}";

        Post(() => Show(media.Current));
        media.Changed += track => Post(() => Show(track));
    }

    public void Attach(IForegroundSource foreground)
    {
        Post(() => ForegroundText = foreground.Current ?? "–");
        foreground.Changed += name => Post(() => ForegroundText = name);
    }

    /// <summary>Adds an informational line, e.g. from the shortcut recorder (any thread).</summary>
    public void Info(string text) => Post(() => Add(LogKind.Info, text));

    /// <summary>Adds a warning that stays visible whatever the filters (any thread).</summary>
    public void Warning(string text) => Post(() => Add(LogKind.Warning, text));

    [RelayCommand]
    private void Clear()
    {
        all.Clear();
        Entries.Clear();
    }

    [RelayCommand]
    private void RefreshAudio()
    {
        AudioSessions.Clear();
        foreach (var name in listAudioSessions())
        {
            AudioSessions.Add(name);
        }

        OnPropertyChanged(nameof(HasAudioSessions));
    }

    [RelayCommand]
    private void PressButton(int id) => simulator?.PressButton(id);

    [RelayCommand]
    private void SimulateUnplug() => simulator?.Disconnect();

    partial void OnSimulatorSliderChanged(int value) => simulator?.MoveSlider(1, value);

    partial void OnShowDeviceChanged(bool value) => Rebuild();

    partial void OnShowAppChanged(bool value) => Rebuild();

    partial void OnShowActionsChanged(bool value) => Rebuild();

    private void RefreshConnection()
    {
        OnPropertyChanged(nameof(Port));
        OnPropertyChanged(nameof(Firmware));
        OnPropertyChanged(nameof(Protocol));
        OnPropertyChanged(nameof(Hardware));
    }

    private void RefreshProblems()
    {
        ConfigProblems.Clear();
        if (session.Service.LoadError is { } error)
        {
            ConfigProblems.Add($"config.json wurde nicht geladen, es gelten die Standardwerte. {error}");
        }

        foreach (var issue in session.Service.Issues)
        {
            ConfigProblems.Add(issue.ToString());
        }

        OnPropertyChanged(nameof(HasConfigProblems));
    }

    private void Add(LogKind kind, string text)
    {
        var entry = new LogEntry(DateTime.Now, kind, text);
        all.Insert(0, entry);
        if (all.Count > MaxEntries)
        {
            all.RemoveAt(all.Count - 1);
        }

        if (IsVisible(entry))
        {
            Entries.Insert(0, entry);
            if (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }
        }
    }

    private void Rebuild()
    {
        Entries.Clear();
        foreach (var entry in all.Where(IsVisible))
        {
            Entries.Add(entry);
        }
    }

    private bool IsVisible(LogEntry entry) => entry.Kind switch
    {
        LogKind.FromDevice => ShowDevice,
        LogKind.ToDevice => ShowApp,
        LogKind.Action => ShowActions,
        _ => true
    };

    private static void Post(Action action) => Dispatcher.UIThread.Post(action);
}
