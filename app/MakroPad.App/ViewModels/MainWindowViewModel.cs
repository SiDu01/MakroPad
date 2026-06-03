using System.Collections.ObjectModel;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.App.Services;
using MakroPad.Actions;
using MakroPad.Core.Configuration;
using MakroPad.Core.DisplayTriggers;
using MakroPad.Device;

namespace MakroPad.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string SimulatorPort = "Simulator";
    private static readonly IReadOnlyList<ActionDefinition> ActionDefinitions =
    [
        new("media.playPause", "Media Play/Pause", "No parameters required.", "", []),
        new("media.nextTrack", "Next media track", "No parameters required.", "", []),
        new("media.previousTrack", "Previous media track", "No parameters required.", "", []),
        new("media.stop", "Stop media playback", "No parameters required.", "", []),
        new("media.volumeUp", "Increase system volume", "No parameters required.", "", []),
        new("media.volumeDown", "Decrease system volume", "No parameters required.", "", []),
        new("media.mute", "Toggle system mute", "No parameters required.", "", []),
        new("system.volume", "Map slider value to system volume", "No parameters required. This action uses the incoming slider value.", "", []),
        new("app.volume", "Map slider value to one app's volume", "Required parameter: process. Example: process=Spotify. This action uses the incoming slider value.", "process=Spotify", ["process"]),
        new("system.openUrl", "Open a URL with the default browser", "Required parameter: url. Example: url=https://example.com", "url=https://example.com", ["url"]),
        new("system.openApp", "Open an app or file through the OS shell", "Required parameter: path. Example: path=notepad.exe", "path=notepad.exe", ["path"]),
        new("keyboard.shortcut", "Send a keyboard shortcut", "Required parameter: keys. Supported examples: Ctrl+C, Ctrl+Shift+Esc, Alt+Tab, Win+E, F1.", "keys=Ctrl+Shift+Esc", ["keys"]),
        new("log", "Write a message to the app log", "Optional parameter: message. Example: message=Hello MakroPad", "message=Hello MakroPad", [])
    ];
    private static readonly IReadOnlyDictionary<string, ActionDefinition> ActionDefinitionLookup =
        ActionDefinitions.ToDictionary(definition => definition.Id, StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyList<DisplayTriggerPreset> DisplayTriggerPresets =
    [
        new("Slider value", "input", "slider:1", "any", "any", "any", 0, 100, 5, 1500, "normal", "Slider {slider.id}\n{slider.percent}%"),
        new("Button pressed", "input", "button:1", "any", "any", "any", 0, 100, 0, 1500, "normal", "Input\n{input}"),
        new("Action completed", "actionResult", "*", "ok", "any", "any", 0, 100, 0, 3000, "normal", "Action\n{action.message}"),
        new("Action failed", "actionResult", "*", "failed", "any", "any", 0, 100, 0, 3000, "normal", "Action failed\n{action.message}"),
        new("Now playing", "mediaChanged", "*", "any", "any", "any", 0, 100, 0, 0, "normal", "Media\n{media.artist}\n{media.title}"),
        new("Connection status", "connectionChanged", "*", "any", "any", "any", 0, 100, 0, 2000, "normal", "MakroPad\n{connection.status}"),
        new("Profile changed", "profileChanged", "*", "any", "any", "any", 0, 100, 0, 2000, "normal", "Profile\n{profile}"),
        new("HTML list", "input", "button:1", "any", "any", "any", 0, 100, 0, 2500, "html", "<h1>{profile}</h1><p>{input}</p><ul><li>{status}</li></ul>")
    ];

    private readonly SimulatedMakroPadConnection simulatedConnection = new();
    private readonly SerialMakroPadConnection serialConnection = new();
    private readonly MakroPadDeviceProbe deviceProbe = new();
    private readonly MakroPadConfigStore configStore;
    private readonly DeviceDisplaySyncService deviceDisplaySyncService;
    private readonly ActionPipeline actionPipeline = new(new ActionResolver(), new ActionExecutor());
    private readonly CancellationTokenSource nowPlayingCancellation = new();
    private readonly CancellationTokenSource audioSessionsCancellation = new();
    private IMakroPadConnection? activeConnection;
    private MakroPadConfig currentConfig = MakroPadConfig.CreateDefault();
    private bool isRefreshingEditor;
    private bool isRefreshingDisplayTriggerEditor;
    private bool isLoadingConfig;
    private readonly DisplayTriggerInputTracker displayTriggerInputTracker = new();
    private readonly object displayTriggerSequenceLock = new();
    private CancellationTokenSource? displayTriggerSequenceCancellation;

    [ObservableProperty]
    private string connectionStatus = "Disconnected";

    [ObservableProperty]
    private string? selectedPort;

    partial void OnSelectedPortChanged(string? value)
    {
        OnPropertyChanged(nameof(SelectedPortDisplay));
        OnPropertyChanged(nameof(IsSimulatorSelected));
    }

    [ObservableProperty]
    private string displayText = "Hello MakroPad";

    [ObservableProperty]
    private int lastSliderValue;

    partial void OnLastSliderValueChanged(int value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private int simulatorButtonId = 1;

    [ObservableProperty]
    private int simulatorSliderId = 1;

    [ObservableProperty]
    private string activeProfileName = "Default";

    [ObservableProperty]
    private string configPath = "";

    [ObservableProperty]
    private string profileEditorName = "Default";

    [ObservableProperty]
    private bool autoConnect = true;

    [ObservableProperty]
    private bool enableNowPlayingDisplay = true;

    [ObservableProperty]
    private int volumeSliderDeadbandPercent = 2;

    [ObservableProperty]
    private DisplayTriggerViewModel? selectedDisplayTrigger;

    partial void OnSelectedDisplayTriggerChanged(DisplayTriggerViewModel? value)
    {
        if (isRefreshingDisplayTriggerEditor)
        {
            return;
        }

        isRefreshingDisplayTriggerEditor = true;
        try
        {
            DisplayTriggerEditorEnabled = value?.Enabled ?? true;
            DisplayTriggerEditorEventType = value?.EventType ?? "input";
            DisplayTriggerEditorInput = value?.Input ?? "slider:1";
            DisplayTriggerEditorActionStatus = value?.ActionStatus ?? "any";
            DisplayTriggerEditorConnectionStatus = value?.ConnectionStatus ?? "any";
            DisplayTriggerEditorCondition = value?.Condition ?? "any";
            DisplayTriggerEditorMinPercent = value?.MinPercent ?? 0;
            DisplayTriggerEditorMaxPercent = value?.MaxPercent ?? 100;
            DisplayTriggerEditorChangePercent = value?.ChangePercent ?? 5;
            DisplayTriggerEditorDurationMs = value?.DurationMs ?? 3000;
            DisplayTriggerEditorTemplateMode = value?.TemplateMode ?? "normal";
            DisplayTriggerEditorTemplate = value?.Template ?? "Slider {slider.id}\n{slider.percent}%";
            DisplayTriggerEditorValidationMessage = "";
        }
        finally
        {
            isRefreshingDisplayTriggerEditor = false;
        }

        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private bool displayTriggerEditorEnabled = true;

    partial void OnDisplayTriggerEditorEnabledChanged(bool value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private string displayTriggerEditorEventType = "input";

    public string DisplayTriggerPlaceholderHint => NormalizeDisplayTriggerEventType(DisplayTriggerEditorEventType) switch
    {
        "actionResult" => "{}Placeholders: {action.status}, {action.name}, {action.message}, {input}, {profile}, {status}.",
        "mediaChanged" => "{}Placeholders: {media.artist}, {media.title}, {media.track}, {profile}, {status}.",
        "connectionChanged" => "{}Placeholders: {connection.status}, {profile}, {status}.",
        "profileChanged" => "{}Placeholders: {profile}, {status}.",
        _ => "{}Placeholders: {input}, {slider.id}, {slider.value}, {slider.percent}, {profile}, {status}."
    };

    public bool IsInputDisplayTrigger =>
        NormalizeDisplayTriggerEventType(DisplayTriggerEditorEventType) == "input";

    public bool IsSliderInputDisplayTrigger =>
        IsInputDisplayTrigger
        && DisplayTriggerEditorInput.StartsWith("slider:", StringComparison.OrdinalIgnoreCase);

    public bool IsActionResultDisplayTrigger =>
        NormalizeDisplayTriggerEventType(DisplayTriggerEditorEventType) == "actionResult";

    public bool IsMediaChangedDisplayTrigger =>
        NormalizeDisplayTriggerEventType(DisplayTriggerEditorEventType) == "mediaChanged";

    public bool IsConnectionChangedDisplayTrigger =>
        NormalizeDisplayTriggerEventType(DisplayTriggerEditorEventType) == "connectionChanged";

    public bool ShowsDisplayTriggerInputMatch =>
        IsInputDisplayTrigger || IsActionResultDisplayTrigger;

    public bool ShowsDisplayTriggerPreviewValues =>
        IsSliderInputDisplayTrigger
        || IsActionResultDisplayTrigger
        || IsMediaChangedDisplayTrigger
        || IsConnectionChangedDisplayTrigger;

    partial void OnDisplayTriggerEditorEventTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsInputDisplayTrigger));
        OnPropertyChanged(nameof(IsSliderInputDisplayTrigger));
        OnPropertyChanged(nameof(IsActionResultDisplayTrigger));
        OnPropertyChanged(nameof(IsMediaChangedDisplayTrigger));
        OnPropertyChanged(nameof(IsConnectionChangedDisplayTrigger));
        OnPropertyChanged(nameof(ShowsDisplayTriggerInputMatch));
        OnPropertyChanged(nameof(ShowsDisplayTriggerPreviewValues));
        OnPropertyChanged(nameof(DisplayTriggerPlaceholderHint));
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private string displayTriggerEditorInput = "slider:1";

    partial void OnDisplayTriggerEditorInputChanged(string value)
    {
        OnPropertyChanged(nameof(IsSliderInputDisplayTrigger));
        OnPropertyChanged(nameof(ShowsDisplayTriggerPreviewValues));
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private string displayTriggerEditorCondition = "any";

    partial void OnDisplayTriggerEditorConditionChanged(string value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private int displayTriggerEditorMinPercent;

    [ObservableProperty]
    private string displayTriggerEditorActionStatus = "any";

    partial void OnDisplayTriggerEditorActionStatusChanged(string value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private string displayTriggerEditorConnectionStatus = "any";

    partial void OnDisplayTriggerEditorConnectionStatusChanged(string value)
    {
        RefreshDisplayTriggerPreview();
    }

    partial void OnDisplayTriggerEditorMinPercentChanged(int value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private int displayTriggerEditorMaxPercent = 100;

    partial void OnDisplayTriggerEditorMaxPercentChanged(int value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private int displayTriggerEditorChangePercent = 5;

    partial void OnDisplayTriggerEditorChangePercentChanged(int value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private int displayTriggerEditorDurationMs = 3000;

    partial void OnDisplayTriggerEditorDurationMsChanged(int value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private string displayTriggerEditorTemplateMode = "normal";

    partial void OnDisplayTriggerEditorTemplateModeChanged(string value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private string displayTriggerEditorTemplate = "Slider {slider.id}\n{slider.percent}%";

    partial void OnDisplayTriggerEditorTemplateChanged(string value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private string displayTriggerEditorValidationMessage = "";

    [ObservableProperty]
    private DisplayTriggerPreset? selectedDisplayTriggerPreset;

    [ObservableProperty]
    private int displayTriggerPreviewPercent = 50;

    partial void OnDisplayTriggerPreviewPercentChanged(int value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private int displayTriggerPreviewPreviousPercent = 40;

    partial void OnDisplayTriggerPreviewPreviousPercentChanged(int value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private string displayTriggerPreviewActionStatus = "ok";

    partial void OnDisplayTriggerPreviewActionStatusChanged(string value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private string displayTriggerPreviewActionName = "media.playPause";

    partial void OnDisplayTriggerPreviewActionNameChanged(string value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private string displayTriggerPreviewActionMessage = "Action completed";

    partial void OnDisplayTriggerPreviewActionMessageChanged(string value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private string displayTriggerPreviewMediaArtist = "Artist";

    partial void OnDisplayTriggerPreviewMediaArtistChanged(string value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private string displayTriggerPreviewMediaTitle = "Track title";

    partial void OnDisplayTriggerPreviewMediaTitleChanged(string value)
    {
        RefreshDisplayTriggerPreview();
    }

    [ObservableProperty]
    private string displayTriggerPreviewConnectionStatus = "connected";

    partial void OnDisplayTriggerPreviewConnectionStatusChanged(string value)
    {
        RefreshDisplayTriggerPreview();
    }

    public string DisplayTriggerPreviewMatchStatus
    {
        get
        {
            if (!TryCreateEditorDisplayTriggerForPreview(out var trigger))
            {
                return "Preview unavailable";
            }

            var preview = CreateDisplayTriggerPreviewContext(trigger);
            var matches = NormalizeDisplayTriggerEventType(trigger.EventType) switch
            {
                "actionResult" => NormalizeActionStatus(trigger.ActionStatus) == "any"
                    || NormalizeActionStatus(trigger.ActionStatus) == NormalizeActionStatus(preview.ActionStatus),
                "connectionChanged" => NormalizeConnectionStatus(trigger.ConnectionStatus) == "any"
                    || NormalizeConnectionStatus(trigger.ConnectionStatus) == NormalizeConnectionStatus(preview.ConnectionStatus),
                "mediaChanged" => true,
                _ when preview.Percent is not null =>
                    DisplayTriggerMatcher.ThresholdMatches(
                        trigger.ChangePercent,
                        preview.Percent.Value,
                        NormalizePercent(DisplayTriggerPreviewPreviousPercent))
                    && DisplayTriggerConditionMatches(
                        trigger,
                        preview.Percent.Value,
                        NormalizePercent(DisplayTriggerPreviewPreviousPercent)),
                _ => true
            };

            return matches ? "Trigger would fire" : "Trigger would not fire";
        }
    }

    public string DisplayTriggerEditorContentWarning
    {
        get
        {
            var placeholders = Regex.Matches(DisplayTriggerEditorTemplate ?? "", "\\{[^{}]+\\}")
                .Select(match => match.Value.ToLowerInvariant())
                .Distinct()
                .ToList();
            var known = new HashSet<string>(
            [
                "{input}", "{slider.id}", "{slider.value}", "{slider.percent}",
                "{action.status}", "{action.name}", "{action.message}",
                "{media.artist}", "{media.title}", "{media.track}",
                "{connection.status}", "{profile}", "{status}"
            ], StringComparer.OrdinalIgnoreCase);
            var unknown = placeholders.Where(placeholder => !known.Contains(placeholder)).ToList();
            if (unknown.Count > 0)
            {
                return $"Unknown placeholder: {string.Join(", ", unknown)}";
            }

            var eventType = NormalizeDisplayTriggerEventType(DisplayTriggerEditorEventType);
            var foreignPrefixes = eventType switch
            {
                "actionResult" => new[] { "{slider.", "{media.", "{connection." },
                "mediaChanged" => new[] { "{slider.", "{action.", "{connection.", "{input}" },
                "connectionChanged" => new[] { "{slider.", "{action.", "{media.", "{input}" },
                "profileChanged" => new[] { "{slider.", "{action.", "{media.", "{connection.", "{input}" },
                _ => new[] { "{action.", "{media.", "{connection." }
            };
            if (placeholders.Any(placeholder => foreignPrefixes.Any(prefix => placeholder.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
            {
                return "Content uses placeholders that are not populated by this event type.";
            }

            var renderedLines = DisplayTriggerEditorTemplate?
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? [];
            if (renderedLines.Length > 5)
            {
                return "Only the first five non-empty display lines are shown.";
            }

            if (DisplayTriggerNormalizer.NormalizeTemplateMode(DisplayTriggerEditorTemplateMode) == "normal"
                && renderedLines.Any(line => line.Length > DisplayTriggerRenderer.MaxPreviewCharacters))
            {
                return $"OLED preview clips lines longer than {DisplayTriggerRenderer.MaxPreviewCharacters} characters.";
            }

            return "";
        }
    }

    public bool IsDisplayTriggerEditorDirty
    {
        get
        {
            if (SelectedDisplayTrigger is null || !TryCreateEditorDisplayTriggerForPreview(out var editorTrigger))
            {
                return false;
            }

            var selectedTrigger = NormalizeDisplayTrigger(new DisplayTriggerConfig
            {
                Enabled = SelectedDisplayTrigger.Enabled,
                EventType = SelectedDisplayTrigger.EventType,
                Input = SelectedDisplayTrigger.Input,
                ActionStatus = SelectedDisplayTrigger.ActionStatus,
                ConnectionStatus = SelectedDisplayTrigger.ConnectionStatus,
                Condition = SelectedDisplayTrigger.Condition,
                MinPercent = SelectedDisplayTrigger.MinPercent,
                MaxPercent = SelectedDisplayTrigger.MaxPercent,
                ChangePercent = SelectedDisplayTrigger.ChangePercent,
                DurationMs = SelectedDisplayTrigger.DurationMs,
                TemplateMode = SelectedDisplayTrigger.TemplateMode,
                Template = SelectedDisplayTrigger.Template
            });

            return editorTrigger != selectedTrigger;
        }
    }

    [ObservableProperty]
    private string displayTriggerPreviewLine1 = "";

    [ObservableProperty]
    private string displayTriggerPreviewLine2 = "";

    [ObservableProperty]
    private string displayTriggerPreviewLine3 = "";

    [ObservableProperty]
    private string displayTriggerPreviewLine4 = "";

    [ObservableProperty]
    private string displayTriggerPreviewLine5 = "";

    partial void OnEnableNowPlayingDisplayChanged(bool value)
    {
        NowPlayingViewModel.SetEnabled(value);
        RefreshOledPreview();
        _ = SendDeviceConfigAsync();
    }

    [ObservableProperty]
    private int actionResultTimeoutMs = 3000;

    [ObservableProperty]
    private string defaultScreenTitle = "MakroPad";

    partial void OnDefaultScreenTitleChanged(string value)
    {
        RefreshOledPreview();
    }

    [ObservableProperty]
    private string defaultScreenLine1 = "{profile}";

    partial void OnDefaultScreenLine1Changed(string value)
    {
        RefreshOledPreview();
    }

    [ObservableProperty]
    private string defaultScreenLine2 = "{status}";

    partial void OnDefaultScreenLine2Changed(string value)
    {
        RefreshOledPreview();
    }

    [ObservableProperty]
    private string defaultScreenLine3 = "";

    partial void OnDefaultScreenLine3Changed(string value)
    {
        RefreshOledPreview();
    }

    [ObservableProperty]
    private string defaultScreenLine4 = "";

    partial void OnDefaultScreenLine4Changed(string value)
    {
        RefreshOledPreview();
    }

    [ObservableProperty]
    private int defaultScreenRows = 3;

    partial void OnDefaultScreenRowsChanged(int value)
    {
        RefreshOledPreview();
        OnPropertyChanged(nameof(UsesFiveLineOledLayout));
    }

    [ObservableProperty]
    private BindingViewModel? selectedBinding;

    partial void OnSelectedBindingChanged(BindingViewModel? value)
    {
        if (isRefreshingEditor)
        {
            return;
        }

        BindingEditorInput = value?.Input ?? "";
        BindingEditorAction = value?.Action ?? AvailableActions.FirstOrDefault() ?? "";
        BindingEditorParameters = value?.Parameters ?? "";
    }

    [ObservableProperty]
    private string bindingEditorInput = "";

    [ObservableProperty]
    private string bindingEditorAction = "media.playPause";

    partial void OnBindingEditorActionChanged(string value)
    {
        RefreshActionEditorHints();
        RefreshStructuredParameterFromRaw();
        OnPropertyChanged(nameof(HasStructuredParameter));
        OnPropertyChanged(nameof(IsAppVolumeAction));
        OnPropertyChanged(nameof(IsKeyboardShortcutAction));
        OnPropertyChanged(nameof(IsOpenAppAction));
        OnPropertyChanged(nameof(ShowsStructuredTextInput));
        OnPropertyChanged(nameof(StructuredParameterLabel));
        OnPropertyChanged(nameof(StructuredParameterValidationMessage));
        OnPropertyChanged(nameof(StructuredParameterValidationMessage));
    }

    [ObservableProperty]
    private string bindingEditorParameters = "";

    partial void OnBindingEditorParametersChanged(string value)
    {
        if (isRefreshingEditor)
        {
            return;
        }

        RefreshStructuredParameterFromRaw();
        OnPropertyChanged(nameof(StructuredParameterValidationMessage));
    }

    [ObservableProperty]
    private string structuredParameterValue = "";

    partial void OnStructuredParameterValueChanged(string value)
    {
        if (isRefreshingEditor)
        {
            return;
        }

        UpdateRawParametersFromStructuredValue();
        OnPropertyChanged(nameof(StructuredParameterValidationMessage));
    }

    [ObservableProperty]
    private string bindingParameterPlaceholder = "";

    [ObservableProperty]
    private string bindingParameterHint = "";

    [ObservableProperty]
    private string bindingActionDescription = "";

    [ObservableProperty]
    private string bindingEditorValidationMessage = "";

    [ObservableProperty]
    private bool isRecordingShortcut;

    [ObservableProperty]
    private string shortcutRecordingStatus = "";

    partial void OnActiveProfileNameChanged(string value)
    {
        ProfileEditorName = value;
        currentConfig = currentConfig with { ActiveProfile = value };
        RefreshProfileBindings();
        RefreshOledPreview();
        RefreshDisplayTriggerPreview();
        if (!isLoadingConfig)
        {
            _ = HandleActiveProfileDisplayAsync(value);
        }
    }

    public string SelectedPortDisplay => string.IsNullOrWhiteSpace(SelectedPort)
        ? "No port selected"
        : SelectedPort;

    public bool IsSimulatorSelected => SelectedPort == SimulatorPort;

    public string ConnectionStatusBrush => ConnectionStatus switch
    {
        "Connected" => "Green",
        "Connecting" => "DarkOrange",
        "Unresponsive" => "Firebrick",
        _ => "Gray"
    };

    partial void OnConnectionStatusChanged(string value)
    {
        OnPropertyChanged(nameof(ConnectionStatusBrush));
        RefreshOledPreview();
        RefreshDisplayTriggerPreview();
        _ = HandleConnectionStatusDisplayAsync(value);
    }

    public string OledPreviewTitle => RenderOledPreview().Title;

    public string OledPreviewLine1 => RenderOledPreview().Line1;

    public string OledPreviewLine2 => RenderOledPreview().Line2;

    public string OledPreviewLine3 => RenderOledPreview().Line3;

    public string OledPreviewLine4 => RenderOledPreview().Line4;

    public bool UsesFiveLineOledLayout => DefaultScreenRows == 5;

    public IReadOnlyList<int> AvailableOledLayoutRows { get; } = [3, 5];

    public ObservableCollection<string> AvailablePorts { get; } = [];

    public ObservableCollection<string> EventLog { get; } = [];

    public AudioSessionsViewModel AudioSessionsViewModel { get; }

    public NowPlayingViewModel NowPlayingViewModel { get; }

    public string NowPlayingStatus => NowPlayingViewModel.NowPlayingStatus;

    public ObservableCollection<ApplicationAudioSession> AudioSessions => AudioSessionsViewModel.AudioSessions;

    public ObservableCollection<string> AvailableAudioProcessNames => AudioSessionsViewModel.AvailableAudioProcessNames;

    public string AudioSessionsStatus => AudioSessionsViewModel.AudioSessionsStatus;

    public IAsyncRelayCommand RefreshAudioSessionsCommand => AudioSessionsViewModel.RefreshAudioSessionsCommand;

    public ObservableCollection<string> ProfileNames { get; } = [];

    public ObservableCollection<BindingViewModel> ProfileBindings { get; } = [];

    public ObservableCollection<DisplayTriggerViewModel> DisplayTriggers { get; } = [];

    public IReadOnlyList<string> AvailableActions { get; } =
        ActionDefinitions.Select(definition => definition.Id).ToArray();

    public IReadOnlyList<string> AvailableInputs { get; } =
    [
        ..Enumerable.Range(1, 20).Select(id => $"button:{id}"),
        ..Enumerable.Range(1, 3).Select(id => $"slider:{id}")
    ];

    public IReadOnlyList<string> AvailableDisplayTriggerInputs { get; } =
    [
        "*",
        ..Enumerable.Range(1, 20).Select(id => $"button:{id}"),
        ..Enumerable.Range(1, 3).Select(id => $"slider:{id}")
    ];

    public IReadOnlyList<int> AvailableButtonIds { get; } =
        Enumerable.Range(1, 20).ToArray();

    public IReadOnlyList<int> AvailableSliderIds { get; } =
        Enumerable.Range(1, 3).ToArray();

    public IReadOnlyList<string> AvailableDisplayTemplateModes { get; } = ["normal", "html"];

    public IReadOnlyList<string> AvailableDisplayTriggerEventTypes { get; } = ["input", "actionResult", "mediaChanged", "connectionChanged", "profileChanged"];

    public IReadOnlyList<string> AvailableDisplayTriggerActionStatuses { get; } = ["any", "ok", "failed"];

    public IReadOnlyList<string> AvailableDisplayTriggerConnectionStatuses { get; } =
        ["any", "connected", "connecting", "unresponsive", "disconnected"];

    public IReadOnlyList<string> AvailableDisplayTriggerConditions { get; } = ["any", "inRange", "increasing", "decreasing"];

    public IReadOnlyList<DisplayTriggerPreset> AvailableDisplayTriggerPresets => DisplayTriggerPresets;

    public string DisplayTriggerSequenceInfo
    {
        get
        {
            if (SelectedDisplayTrigger is null)
            {
                return "";
            }

            var matching = DisplayTriggers
                .Where(trigger => trigger.Enabled
                    && trigger.EventType == SelectedDisplayTrigger.EventType
                    && trigger.MatchDisplay == SelectedDisplayTrigger.MatchDisplay)
                .ToList();
            if (matching.Count <= 1)
            {
                return SelectedDisplayTrigger.DurationMs == 0
                    ? "Duration 0 ms keeps this screen visible until another display message arrives."
                    : "";
            }

            var totalDuration = matching.Sum(trigger => trigger.DurationMs);
            return $"{matching.Count} triggers share this match and run in list order. Total configured duration: {totalDuration} ms.";
        }
    }

    public MainWindowViewModel()
    {
        ConfigPath = GetDefaultConfigPath();
        configStore = new MakroPadConfigStore(ConfigPath);
        deviceDisplaySyncService = new DeviceDisplaySyncService(SystemVolumeControllerFactory.Create());
        AudioSessionsViewModel = new AudioSessionsViewModel(ApplicationVolumeControllerFactory.Create());
        NowPlayingViewModel = new NowPlayingViewModel(new ProcessWindowNowPlayingProvider());
        AudioSessionsViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AudioSessionsViewModel.AudioSessionsStatus))
            {
                OnPropertyChanged(nameof(AudioSessionsStatus));
            }
        };
        AudioSessionsViewModel.SessionsRefreshed += (_, _) =>
            OnPropertyChanged(nameof(StructuredParameterValidationMessage));
        NowPlayingViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(NowPlayingViewModel.NowPlayingStatus))
            {
                OnPropertyChanged(nameof(NowPlayingStatus));
                RefreshOledPreview();
            }
        };
        NowPlayingViewModel.TrackChanged += (_, _) => RefreshOledPreview();
        RefreshActionEditorHints();
        RefreshPorts();
        _ = AudioSessionsViewModel.RefreshAudioSessionsAsync();
        _ = AudioSessionsViewModel.RunRefreshLoopAsync(audioSessionsCancellation.Token);
        _ = NowPlayingViewModel.RunLoopAsync(
            () => activeConnection,
            DefaultScreenUsesMediaPlaceholder,
            HasMediaChangedDisplayTrigger,
            HandleMediaChangedDisplayTriggerAsync,
            SendDeviceConfigAsync,
            AddLog,
            nowPlayingCancellation.Token);
        _ = LoadConfigAsync();
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        var previousSelection = SelectedPort;

        AvailablePorts.Clear();
        AvailablePorts.Add(SimulatorPort);

        foreach (var port in serialConnection.GetAvailablePorts())
        {
            AvailablePorts.Add(port);
        }

        SelectedPort = previousSelection is not null && AvailablePorts.Contains(previousSelection)
            ? previousSelection
            : AvailablePorts.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (SelectedPort is null)
        {
            AddLog("No device port selected.");
            return;
        }

        try
        {
            await DisconnectActiveConnectionAsync();

            activeConnection = SelectedPort == SimulatorPort ? simulatedConnection : serialConnection;
            activeConnection.EventReceived += OnDeviceEventReceived;
            activeConnection.StateChanged += OnDeviceStateChanged;

            ConnectionStatus = "Connecting";
            await activeConnection.ConnectAsync(SelectedPort);
            ConnectionStatus = activeConnection.State.ToString();

        }
        catch (Exception ex)
        {
            ConnectionStatus = "Disconnected";
            AddLog($"Connection failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await DisconnectActiveConnectionAsync();
        ConnectionStatus = "Disconnected";
    }

    [RelayCommand]
    private async Task SendDisplayMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(DisplayText))
        {
            return;
        }

        if (activeConnection is null)
        {
            AddLog("No active device connection.");
            return;
        }

        try
        {
            await activeConnection.SendDisplayMessageAsync(DisplayText);
        }
        catch (Exception ex)
        {
            AddLog($"Send failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LoadConfigAsync()
    {
        try
        {
            isLoadingConfig = true;
            currentConfig = await configStore.LoadOrCreateDefaultAsync();
            var loadedConfig = currentConfig;
            currentConfig = EnsureDefaultActionResultDisplayTriggers(currentConfig);
            currentConfig = EnsureDefaultMediaChangedDisplayTrigger(currentConfig);
            var migratedDisplayTriggers = !ReferenceEquals(loadedConfig, currentConfig);
            ActiveProfileName = currentConfig.ActiveProfile;
            AutoConnect = currentConfig.AppSettings.AutoConnect;
            EnableNowPlayingDisplay = currentConfig.AppSettings.EnableNowPlayingDisplay;
            VolumeSliderDeadbandPercent = NormalizeVolumeSliderDeadbandPercent(currentConfig.AppSettings.VolumeSliderDeadbandPercent);
            RefreshDisplayTriggers();
            ActionResultTimeoutMs = currentConfig.Hardware.Display.ActionResultTimeoutMs;
            DefaultScreenRows = OledDefaultScreenRenderer.NormalizeRows(currentConfig.Hardware.Display.DefaultLayoutRows);
            DefaultScreenTitle = currentConfig.Hardware.Display.DefaultTitle;
            DefaultScreenLine1 = currentConfig.Hardware.Display.DefaultLine1;
            DefaultScreenLine2 = currentConfig.Hardware.Display.DefaultLine2;
            DefaultScreenLine3 = currentConfig.Hardware.Display.DefaultLine3;
            DefaultScreenLine4 = currentConfig.Hardware.Display.DefaultLine4;

            RefreshProfileNames();

            RefreshProfileBindings();
            RefreshPorts();
            if (migratedDisplayTriggers)
            {
                await configStore.SaveAsync(currentConfig);
                AddLog("Display trigger defaults migrated and saved.");
            }
            await SendDeviceConfigAsync();

            AddLog($"Config loaded: {ConfigPath}");

            if (AutoConnect)
            {
                await AutoConnectAsync();
            }
        }
        catch (Exception ex)
        {
            AddLog($"Config load failed: {ex.Message}");
        }
        finally
        {
            isLoadingConfig = false;
        }
    }

    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        try
        {
            var displayTriggers = CreateDisplayTriggerConfigsFromList();
            if (SelectedDisplayTrigger is not null)
            {
                if (!TryCreateEditorDisplayTrigger(out var editorTrigger))
                {
                    AddLog("Config save stopped because the selected display trigger is invalid.");
                    return;
                }

                if (SelectedDisplayTrigger.Index >= 0 && SelectedDisplayTrigger.Index < displayTriggers.Count)
                {
                    displayTriggers[SelectedDisplayTrigger.Index] = editorTrigger;
                    UpdateDisplayTriggers(displayTriggers, SelectedDisplayTrigger.Index);
                    displayTriggers = CreateDisplayTriggerConfigsFromList();
                }
            }

            currentConfig = currentConfig with
            {
                ActiveProfile = ActiveProfileName,
                Hardware = currentConfig.Hardware with
                {
                    Display = currentConfig.Hardware.Display with
                    {
                        ActionResultTimeoutMs = Math.Max(0, ActionResultTimeoutMs),
                        DefaultLayoutRows = OledDefaultScreenRenderer.NormalizeRows(DefaultScreenRows),
                        DefaultTitle = OledDefaultScreenRenderer.NormalizeLine(DefaultScreenTitle, "MakroPad"),
                        DefaultLine1 = OledDefaultScreenRenderer.NormalizeLine(DefaultScreenLine1, "{profile}"),
                        DefaultLine2 = OledDefaultScreenRenderer.NormalizeLine(DefaultScreenLine2, "{status}"),
                        DefaultLine3 = DefaultScreenLine3.Trim(),
                        DefaultLine4 = DefaultScreenLine4.Trim()
                    }
                },
                AppSettings = currentConfig.AppSettings with
                {
                    AutoConnect = AutoConnect,
                    EnableNowPlayingDisplay = EnableNowPlayingDisplay,
                    VolumeSliderDeadbandPercent = NormalizeVolumeSliderDeadbandPercent(VolumeSliderDeadbandPercent),
                    DisplayTriggers = displayTriggers,
                    LastPort = SelectedPort == SimulatorPort ? currentConfig.AppSettings.LastPort : SelectedPort
                }
            };
            await configStore.SaveAsync(currentConfig);
            await SendDeviceConfigAsync();
            AddLog($"Config saved: {ConfigPath}");
        }
        catch (Exception ex)
        {
            AddLog($"Config save failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task AutoConnectAsync()
    {
        if (activeConnection is not null)
        {
            AddLog("Already connected.");
            return;
        }

        var ports = serialConnection.GetAvailablePorts();
        RefreshPorts();

        AddLog("Scanning for MakroPad...");
        var detectedPort = await deviceProbe.FindMakroPadPortAsync(
            ports,
            currentConfig.AppSettings.LastPort);

        if (detectedPort is null)
        {
            AddLog("No MakroPad device found.");
            return;
        }

        SelectedPort = detectedPort;
        AddLog($"MakroPad detected on {detectedPort}.");
        await Task.Delay(250);
        await ConnectAsync();

        if (activeConnection?.State == DeviceConnectionState.Connected)
        {
            currentConfig = currentConfig with
            {
                AppSettings = currentConfig.AppSettings with { LastPort = detectedPort }
            };

            await configStore.SaveAsync(currentConfig);
        }
    }

    [RelayCommand]
    private void SimulateButton()
    {
        if (activeConnection == simulatedConnection)
        {
            simulatedConnection.SimulateButtonPress(SimulatorButtonId);
            return;
        }

        AddLog("Button simulation is only available on the simulator connection.");
    }

    [RelayCommand]
    private void SimulateSlider()
    {
        LastSliderValue = LastSliderValue >= 4000 ? 0 : LastSliderValue + 512;
        if (activeConnection == simulatedConnection)
        {
            simulatedConnection.SimulateSliderChange(SimulatorSliderId, LastSliderValue);
            return;
        }

        AddLog("Slider simulation is only available on the simulator connection.");
    }

    [RelayCommand]
    private void AddProfile()
    {
        var name = ProfileEditorName.Trim();
        if (!TryValidateProfileName(name, out var error))
        {
            AddLog(error);
            return;
        }

        if (currentConfig.Profiles.Any(profile => profile.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            AddLog($"Profile already exists: {name}");
            return;
        }

        currentConfig = currentConfig with
        {
            Profiles = [..currentConfig.Profiles, new ProfileConfig { Name = name }],
            ActiveProfile = name
        };

        RefreshProfileNames();
        ActiveProfileName = name;
        AddLog($"Profile added: {name}");
    }

    [RelayCommand]
    private void RenameProfile()
    {
        var name = ProfileEditorName.Trim();
        if (!TryValidateProfileName(name, out var error))
        {
            AddLog(error);
            return;
        }

        var activeProfile = GetActiveProfile();
        if (activeProfile is null)
        {
            AddLog("Active profile not found.");
            return;
        }

        if (activeProfile.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (currentConfig.Profiles.Any(profile => profile.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            AddLog($"Profile already exists: {name}");
            return;
        }

        var oldName = activeProfile.Name;
        currentConfig = currentConfig with
        {
            ActiveProfile = name,
            Profiles = currentConfig.Profiles
                .Select(profile => profile.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase)
                    ? profile with { Name = name }
                    : profile)
                .ToList()
        };

        RefreshProfileNames();
        ActiveProfileName = name;
        AddLog($"Profile renamed: {oldName} -> {name}");
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (currentConfig.Profiles.Count <= 1)
        {
            AddLog("At least one profile is required.");
            return;
        }

        var activeProfile = GetActiveProfile();
        if (activeProfile is null)
        {
            AddLog("Active profile not found.");
            return;
        }

        var remainingProfiles = currentConfig.Profiles
            .Where(profile => !profile.Name.Equals(activeProfile.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var nextProfileName = remainingProfiles.First().Name;

        currentConfig = currentConfig with
        {
            ActiveProfile = nextProfileName,
            Profiles = remainingProfiles
        };

        RefreshProfileNames();
        ActiveProfileName = nextProfileName;
        AddLog($"Profile deleted: {activeProfile.Name}");
    }

    [RelayCommand]
    private void UseParameterExample()
    {
        if (string.IsNullOrWhiteSpace(BindingParameterPlaceholder))
        {
            BindingEditorParameters = "";
            return;
        }

        BindingEditorParameters = BindingParameterPlaceholder;
        BindingEditorValidationMessage = "";
    }

    [RelayCommand]
    private void StartShortcutRecording()
    {
        BindingEditorAction = "keyboard.shortcut";
        BindingEditorValidationMessage = "";
        ShortcutRecordingStatus = "Press a shortcut. Esc cancels.";
        IsRecordingShortcut = true;
    }

    public void CompleteShortcutRecording(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            CancelShortcutRecording();
            return;
        }

        BindingEditorAction = "keyboard.shortcut";
        BindingEditorParameters = $"keys={shortcut}";
        ShortcutRecordingStatus = $"Recorded: {shortcut}";
        BindingEditorValidationMessage = "";
        IsRecordingShortcut = false;
    }

    public void CancelShortcutRecording()
    {
        IsRecordingShortcut = false;
        ShortcutRecordingStatus = "Recording cancelled.";
    }

    public void SetOpenAppPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        BindingEditorAction = "system.openApp";
        BindingEditorParameters = $"path={path}";
        BindingEditorValidationMessage = "";
    }

    [RelayCommand]
    private void ApplyBinding()
    {
        if (SelectedBinding is null)
        {
            AddLog("No binding selected.");
            return;
        }

        if (!TryCreateEditorBinding(out var binding))
        {
            return;
        }

        var profile = GetActiveProfile();
        if (profile is null || SelectedBinding.Index < 0 || SelectedBinding.Index >= profile.Bindings.Count)
        {
            AddLog("Selected binding no longer exists.");
            RefreshProfileBindings();
            return;
        }

        var updatedBindings = profile.Bindings.ToList();
        updatedBindings[SelectedBinding.Index] = binding;

        UpdateActiveProfile(profile with { Bindings = updatedBindings });
        RefreshProfileBindings(SelectedBinding.Index);
        AddLog($"Binding updated: {binding.Input} -> {binding.Action}");
    }

    [RelayCommand]
    private void AddBinding()
    {
        if (!TryCreateEditorBinding(out var binding))
        {
            return;
        }

        var profile = GetActiveProfile();
        if (profile is null)
        {
            AddLog("Active profile not found.");
            return;
        }

        var updatedBindings = profile.Bindings.ToList();
        updatedBindings.Add(binding);

        UpdateActiveProfile(profile with { Bindings = updatedBindings });
        RefreshProfileBindings(updatedBindings.Count - 1);
        AddLog($"Binding added: {binding.Input} -> {binding.Action}");
    }

    [RelayCommand]
    private void RemoveBinding()
    {
        if (SelectedBinding is null)
        {
            AddLog("No binding selected.");
            return;
        }

        var profile = GetActiveProfile();
        if (profile is null || SelectedBinding.Index < 0 || SelectedBinding.Index >= profile.Bindings.Count)
        {
            AddLog("Selected binding no longer exists.");
            RefreshProfileBindings();
            return;
        }

        var removed = profile.Bindings[SelectedBinding.Index];
        var updatedBindings = profile.Bindings.ToList();
        updatedBindings.RemoveAt(SelectedBinding.Index);

        var nextIndex = updatedBindings.Count == 0
            ? -1
            : Math.Min(SelectedBinding.Index, updatedBindings.Count - 1);

        UpdateActiveProfile(profile with { Bindings = updatedBindings });
        RefreshProfileBindings(nextIndex);
        AddLog($"Binding removed: {removed.Input} -> {removed.Action}");
    }

    [RelayCommand]
    private void ApplyDisplayTrigger()
    {
        if (SelectedDisplayTrigger is null)
        {
            AddLog("No display trigger selected.");
            return;
        }

        if (!TryCreateEditorDisplayTrigger(out var trigger))
        {
            return;
        }

        if (SelectedDisplayTrigger.Index < 0 || SelectedDisplayTrigger.Index >= DisplayTriggers.Count)
        {
            AddLog("Selected display trigger no longer exists.");
            RefreshDisplayTriggers();
            return;
        }

        var selectedIndex = SelectedDisplayTrigger.Index;
        var triggers = CreateDisplayTriggerConfigsFromList();
        triggers[selectedIndex] = trigger;
        UpdateDisplayTriggers(triggers, selectedIndex);
        AddLog($"Display trigger updated: {trigger.Input}");
    }

    [RelayCommand]
    private void ResetDisplayTriggerEditor()
    {
        OnSelectedDisplayTriggerChanged(SelectedDisplayTrigger);
        AddLog("Display trigger editor reset.");
    }

    [RelayCommand]
    private void AddDisplayTrigger()
    {
        if (!TryCreateEditorDisplayTrigger(out var trigger))
        {
            return;
        }

        var triggers = CreateDisplayTriggerConfigsFromList();
        triggers.Add(trigger);
        UpdateDisplayTriggers(triggers, triggers.Count - 1);
        AddLog($"Display trigger added: {trigger.Input}");
    }

    [RelayCommand]
    private void DuplicateDisplayTrigger()
    {
        if (SelectedDisplayTrigger is null
            || SelectedDisplayTrigger.Index < 0
            || SelectedDisplayTrigger.Index >= DisplayTriggers.Count)
        {
            AddLog("No display trigger selected.");
            return;
        }

        var triggers = CreateDisplayTriggerConfigsFromList();
        var insertIndex = SelectedDisplayTrigger.Index + 1;
        triggers.Insert(insertIndex, triggers[SelectedDisplayTrigger.Index] with { });
        UpdateDisplayTriggers(triggers, insertIndex);
        AddLog($"Display trigger duplicated: {triggers[insertIndex].Input}");
    }

    [RelayCommand]
    private void ApplyDisplayTriggerPreset()
    {
        if (SelectedDisplayTriggerPreset is null)
        {
            AddLog("No display trigger preset selected.");
            return;
        }

        var preset = SelectedDisplayTriggerPreset;
        DisplayTriggerEditorEnabled = true;
        DisplayTriggerEditorEventType = preset.EventType;
        DisplayTriggerEditorInput = preset.Input;
        DisplayTriggerEditorActionStatus = preset.ActionStatus;
        DisplayTriggerEditorConnectionStatus = preset.ConnectionStatus;
        DisplayTriggerEditorCondition = preset.Condition;
        DisplayTriggerEditorMinPercent = preset.MinPercent;
        DisplayTriggerEditorMaxPercent = preset.MaxPercent;
        DisplayTriggerEditorChangePercent = preset.ChangePercent;
        DisplayTriggerEditorDurationMs = preset.DurationMs;
        DisplayTriggerEditorTemplateMode = preset.TemplateMode;
        DisplayTriggerEditorTemplate = preset.Template;
        DisplayTriggerEditorValidationMessage = "";
        AddLog($"Display trigger preset applied: {preset.Name}");
    }

    [RelayCommand]
    private void RemoveDisplayTrigger()
    {
        if (SelectedDisplayTrigger is null)
        {
            AddLog("No display trigger selected.");
            return;
        }

        if (SelectedDisplayTrigger.Index < 0 || SelectedDisplayTrigger.Index >= DisplayTriggers.Count)
        {
            AddLog("Selected display trigger no longer exists.");
            RefreshDisplayTriggers();
            return;
        }

        var triggers = CreateDisplayTriggerConfigsFromList();
        var removed = triggers[SelectedDisplayTrigger.Index];
        triggers.RemoveAt(SelectedDisplayTrigger.Index);
        var nextIndex = triggers.Count == 0
            ? -1
            : Math.Min(SelectedDisplayTrigger.Index, triggers.Count - 1);

        UpdateDisplayTriggers(triggers, nextIndex);
        AddLog($"Display trigger removed: {removed.Input}");
    }

    [RelayCommand]
    private void MoveDisplayTriggerUp()
    {
        MoveSelectedDisplayTrigger(-1);
    }

    [RelayCommand]
    private void MoveDisplayTriggerDown()
    {
        MoveSelectedDisplayTrigger(1);
    }

    [RelayCommand]
    private async Task TestDisplayTriggerAsync()
    {
        if (!TryCreateEditorDisplayTrigger(out var trigger))
        {
            return;
        }

        if (activeConnection is null || activeConnection.State != DeviceConnectionState.Connected)
        {
            AddLog("No active device connection.");
            return;
        }

        var preview = CreateDisplayTriggerPreviewContext(trigger);
        var lines = RenderDisplayTriggerLines(
            trigger,
            preview.Input,
            preview.SliderValue,
            preview.Percent,
            preview.ActionStatus,
            preview.ActionName,
            preview.ActionMessage,
            preview.MediaArtist,
            preview.MediaTitle,
            preview.ConnectionStatus);

        if (lines.Count == 0)
        {
            AddLog("Display trigger produced no display lines.");
            return;
        }

        try
        {
            await deviceDisplaySyncService.SendTemporaryTemplateAsync(
                activeConnection,
                lines,
                NormalizeDisplayTriggerDurationMs(trigger.DurationMs));
            AddLog($"Display trigger tested: {trigger.Input}");
        }
        catch (Exception ex)
        {
            AddLog($"Display trigger test failed: {ex.Message}");
        }
    }

    private void MoveSelectedDisplayTrigger(int direction)
    {
        if (SelectedDisplayTrigger is null)
        {
            AddLog("No display trigger selected.");
            return;
        }

        var sourceIndex = SelectedDisplayTrigger.Index;
        var targetIndex = sourceIndex + direction;
        if (sourceIndex < 0
            || sourceIndex >= DisplayTriggers.Count
            || targetIndex < 0
            || targetIndex >= DisplayTriggers.Count)
        {
            return;
        }

        var triggers = CreateDisplayTriggerConfigsFromList();
        (triggers[sourceIndex], triggers[targetIndex]) = (triggers[targetIndex], triggers[sourceIndex]);
        UpdateDisplayTriggers(triggers, targetIndex);
        AddLog($"Display trigger moved: {sourceIndex + 1} -> {targetIndex + 1}");
    }

    private void OnDeviceEventReceived(object? sender, DeviceEvent deviceEvent)
    {
        Dispatcher.UIThread.Post(() => HandleDeviceEvent(deviceEvent));
    }

    private void OnDeviceStateChanged(object? sender, DeviceConnectionState state)
    {
        Dispatcher.UIThread.Post(() => ConnectionStatus = state.ToString());
    }

    private void HandleDeviceEvent(DeviceEvent deviceEvent)
    {
        if (deviceEvent.Message is { } message)
        {
            _ = HandleProtocolMessageSafelyAsync(message);
        }

        AddLog($"{deviceEvent.Timestamp:HH:mm:ss}  {deviceEvent.Direction}  {deviceEvent.Line}");
    }

    private async Task HandleProtocolMessageSafelyAsync(MakroPad.Core.Protocol.ProtocolMessage message)
    {
        try
        {
            await HandleProtocolMessageAsync(message);
        }
        catch (Exception ex)
        {
            AddLog($"Protocol handling failed: {ex.Message}");
        }
    }

    private async Task HandleProtocolMessageAsync(MakroPad.Core.Protocol.ProtocolMessage message)
    {
        if (message.Type.Equals("button", StringComparison.OrdinalIgnoreCase))
        {
            var state = message.GetValue("state");
            if (state?.Equals("up", StringComparison.OrdinalIgnoreCase) != true)
            {
                return;
            }

            var id = message.GetValue("id");
            if (id is null)
            {
                return;
            }

            var input = $"button:{id}";
            var showedActionResult = await HandleConfiguredInputAsync(input);
            if (!showedActionResult)
            {
                await HandleDisplayTriggersAsync(input);
            }
            return;
        }

        if (message.Type.Equals("slider", StringComparison.OrdinalIgnoreCase))
        {
            var id = message.GetValue("id");
            var value = message.GetValue("value");
            var hasSliderValue = int.TryParse(value, out var sliderValue);

            if (hasSliderValue)
            {
                LastSliderValue = sliderValue;
            }

            if (id is not null)
            {
                var input = $"slider:{id}";
                var showedActionResult = await HandleConfiguredInputAsync(input, value);
                if (hasSliderValue && !showedActionResult)
                {
                    await HandleDisplayTriggersAsync(input, sliderValue);
                }
            }
        }
    }

    private async Task HandleDisplayTriggersAsync(string input, int? sliderValue = null)
    {
        var allTriggers = currentConfig.AppSettings.DisplayTriggers
            .Select((candidate, index) => (Trigger: NormalizeDisplayTrigger(candidate), Index: index))
            .Where(candidate => candidate.Trigger.Enabled
                && NormalizeDisplayTriggerEventType(candidate.Trigger.EventType) == "input"
                && (candidate.Trigger.Input == "*"
                    || candidate.Trigger.Input.Equals(input, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (allTriggers.Count == 0)
        {
            return;
        }

        var percent = sliderValue is null ? (int?)null : MapSliderValueToPercent(sliderValue.Value);
        if (percent is not null)
        {
            var triggers = new List<DisplayTriggerConfig>();
            foreach (var candidate in allTriggers)
            {
                var trigger = candidate.Trigger;
                var trackerKey = $"{input}|{candidate.Index}";
                if (!displayTriggerInputTracker.ShouldTrigger(trackerKey, trigger, percent.Value))
                {
                    continue;
                }

                triggers.Add(trigger);
            }

            if (triggers.Count == 0)
            {
                return;
            }

            try
            {
                await SendDisplayTriggerSequenceAsync(triggers, input, sliderValue, percent);
            }
            catch (Exception ex)
            {
                AddLog($"Display trigger failed: {ex.Message}");
            }

            return;
        }

        try
        {
            await SendDisplayTriggerSequenceAsync(allTriggers.Select(candidate => candidate.Trigger).ToList(), input, sliderValue, percent);
        }
        catch (Exception ex)
        {
            AddLog($"Display trigger failed: {ex.Message}");
        }
    }

    private async Task SendDisplayTriggerSequenceAsync(
        IReadOnlyList<DisplayTriggerConfig> triggers,
        string input,
        int? sliderValue,
        int? percent,
        string actionStatus = "",
        string actionName = "",
        string actionMessage = "",
        string mediaArtist = "",
        string mediaTitle = "",
        string connectionStatus = "",
        CancellationToken cancellationToken = default)
    {
        var sequenceToken = BeginDisplayTriggerSequence(cancellationToken);
        try
        {
            var sentCount = 0;
            for (var index = 0; index < triggers.Count; index++)
            {
                var trigger = triggers[index];
                var lines = RenderDisplayTriggerLines(
                    trigger,
                    input,
                    sliderValue,
                    percent,
                    actionStatus,
                    actionName,
                    actionMessage,
                    mediaArtist,
                    mediaTitle,
                    connectionStatus);
                if (lines.Count == 0)
                {
                    continue;
                }

                var durationMs = NormalizeDisplayTriggerDurationMs(trigger.DurationMs);
                await deviceDisplaySyncService.SendTemporaryTemplateAsync(activeConnection, lines, durationMs, sequenceToken);
                sentCount++;

                if (index < triggers.Count - 1 && durationMs > 0)
                {
                    await Task.Delay(durationMs, sequenceToken);
                }
            }

            if (sentCount > 1)
            {
                AddLog($"Display trigger sequence sent: {input} ({sentCount} screens)");
            }
        }
        catch (OperationCanceledException) when (sequenceToken.IsCancellationRequested)
        {
            // A newer display event superseded this sequence.
        }
    }

    private CancellationToken BeginDisplayTriggerSequence(CancellationToken cancellationToken)
    {
        lock (displayTriggerSequenceLock)
        {
            displayTriggerSequenceCancellation?.Cancel();
            displayTriggerSequenceCancellation?.Dispose();
            displayTriggerSequenceCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return displayTriggerSequenceCancellation.Token;
        }
    }

    private async Task<bool> HandleConfiguredInputAsync(string input, string? value = null)
    {
        var result = await actionPipeline.HandleInputAsync(currentConfig, input, value);

        if (result.Kind == ActionPipelineResultKind.Throttled)
        {
            return false;
        }

        AddLog(FormatActionResult(result));

        if (activeConnection is not null && result.ShouldSendFeedback && result.ExecutionResult is not null)
        {
            var status = result.ExecutionResult.Succeeded ? "ok" : "failed";
            var actionName = result.Binding?.Action ?? "";
            var handledByDisplayTrigger = await HandleActionResultDisplayTriggersAsync(
                input,
                status,
                actionName,
                result.ExecutionResult.Message);

            if (handledByDisplayTrigger)
            {
                return true;
            }

            var line = MakroPad.Core.Protocol.ProtocolCodec.Format(
                "action-result",
                new Dictionary<string, string?>
                {
                    ["input"] = input,
                    ["status"] = status,
                    ["message"] = result.ExecutionResult.Message
                });

            try
            {
                await activeConnection.SendLineAsync(line);
                return true;
            }
            catch (Exception ex)
            {
                AddLog($"Action feedback failed: {ex.Message}");
            }
        }

        return false;
    }

    private async Task<bool> HandleActionResultDisplayTriggersAsync(
        string input,
        string status,
        string actionName,
        string message)
    {
        var triggers = currentConfig.AppSettings.DisplayTriggers
            .Select(NormalizeDisplayTrigger)
            .Where(candidate => candidate.Enabled
                && NormalizeDisplayTriggerEventType(candidate.EventType) == "actionResult"
                && (candidate.Input == "*"
                    || candidate.Input.Equals(input, StringComparison.OrdinalIgnoreCase))
                && DisplayTriggerMatcher.ActionStatusMatches(candidate, status))
            .ToList();

        if (triggers.Count == 0)
        {
            return false;
        }

        var mediaTrack = NowPlayingViewModel.LastSentTrack;
        if (mediaTrack is not null
            && triggers.Any(trigger => NormalizeDisplayTriggerDurationMs(trigger.DurationMs) > 0))
        {
            triggers.AddRange(
                currentConfig.AppSettings.DisplayTriggers
                    .Select(NormalizeDisplayTrigger)
                    .Where(trigger => trigger.Enabled
                        && NormalizeDisplayTriggerEventType(trigger.EventType) == "mediaChanged"));
        }

        try
        {
            await SendDisplayTriggerSequenceAsync(
                triggers,
                input,
                null,
                null,
                status,
                actionName,
                message,
                mediaTrack?.Artist ?? "",
                mediaTrack?.Title ?? "");
            return true;
        }
        catch (Exception ex)
        {
            AddLog($"Action result display trigger failed: {ex.Message}");
            return false;
        }
    }

    private bool HasMediaChangedDisplayTrigger()
    {
        return currentConfig.AppSettings.DisplayTriggers.Any(
            trigger =>
            {
                var normalized = NormalizeDisplayTrigger(trigger);
                return normalized.Enabled
                    && NormalizeDisplayTriggerEventType(normalized.EventType) == "mediaChanged";
            });
    }

    private async Task HandleMediaChangedDisplayTriggerAsync(
        NowPlayingTrack track,
        CancellationToken cancellationToken)
    {
        var triggers = currentConfig.AppSettings.DisplayTriggers
            .Select(NormalizeDisplayTrigger)
            .Where(trigger => trigger.Enabled
                && NormalizeDisplayTriggerEventType(trigger.EventType) == "mediaChanged")
            .ToList();

        if (triggers.Count == 0)
        {
            return;
        }

        try
        {
            await SendDisplayTriggerSequenceAsync(
                triggers,
                "*",
                null,
                null,
                mediaArtist: track.Artist,
                mediaTitle: track.Title,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            AddLog($"Media changed display trigger failed: {ex.Message}");
        }
    }

    private async Task HandleConnectionChangedDisplayTriggersAsync(string connectionStatus)
    {
        var normalizedStatus = NormalizeConnectionStatus(connectionStatus);
        var triggers = currentConfig.AppSettings.DisplayTriggers
            .Select(NormalizeDisplayTrigger)
            .Where(trigger => trigger.Enabled
                && NormalizeDisplayTriggerEventType(trigger.EventType) == "connectionChanged"
                && DisplayTriggerMatcher.ConnectionStatusMatches(trigger, normalizedStatus))
            .ToList();

        if (triggers.Count == 0)
        {
            return;
        }

        try
        {
            await SendDisplayTriggerSequenceAsync(
                triggers,
                "*",
                null,
                null,
                connectionStatus: connectionStatus);
        }
        catch (Exception ex)
        {
            AddLog($"Connection changed display trigger failed: {ex.Message}");
        }
    }

    private async Task HandleConnectionStatusDisplayAsync(string connectionStatus)
    {
        if (connectionStatus.Equals("Connected", StringComparison.OrdinalIgnoreCase))
        {
            await SendDeviceConfigAsync();
        }

        await HandleConnectionChangedDisplayTriggersAsync(connectionStatus);
    }

    private async Task HandleActiveProfileDisplayAsync(string profileName)
    {
        await SendDeviceConfigAsync();

        var triggers = currentConfig.AppSettings.DisplayTriggers
            .Select(NormalizeDisplayTrigger)
            .Where(trigger => trigger.Enabled
                && NormalizeDisplayTriggerEventType(trigger.EventType) == "profileChanged")
            .ToList();
        if (triggers.Count == 0)
        {
            return;
        }

        try
        {
            await SendDisplayTriggerSequenceAsync(triggers, "*", null, null);
        }
        catch (Exception ex)
        {
            AddLog($"Profile changed display trigger failed for {profileName}: {ex.Message}");
        }
    }

    private async Task SendDeviceConfigAsync()
    {
        try
        {
            await deviceDisplaySyncService.SendConfigAsync(activeConnection, CreateDeviceDisplayState());
            RefreshOledPreview();
        }
        catch (Exception ex)
        {
            AddLog($"Device config send failed: {ex.Message}");
        }
    }

    private static string FormatActionResult(ActionPipelineResult result)
    {
        if (result.Kind == ActionPipelineResultKind.Unbound)
        {
            return result.Message;
        }

        if (result.Binding is null || result.ExecutionResult is null)
        {
            return result.Message;
        }

        return result.ExecutionResult.Succeeded
            ? $"Action matched: {result.Input} -> {result.Binding.Action} ({result.ExecutionResult.Message})"
            : $"Action failed: {result.Input} -> {result.Binding.Action} ({result.ExecutionResult.Message})";
    }

    private async Task DisconnectActiveConnectionAsync()
    {
        if (activeConnection is null)
        {
            return;
        }

        activeConnection.EventReceived -= OnDeviceEventReceived;
        activeConnection.StateChanged -= OnDeviceStateChanged;
        await activeConnection.DisconnectAsync();
        activeConnection = null;
    }

    private void AddLog(string message)
    {
        EventLog.Insert(0, message);

        while (EventLog.Count > 200)
        {
            EventLog.RemoveAt(EventLog.Count - 1);
        }
    }

    private void RefreshProfileNames()
    {
        ProfileNames.Clear();
        foreach (var profile in currentConfig.Profiles)
        {
            ProfileNames.Add(profile.Name);
        }
    }

    private void RefreshProfileBindings(int selectedIndex = 0)
    {
        ProfileBindings.Clear();

        var profile = GetActiveProfile();

        if (profile is null)
        {
            SelectedBinding = null;
            return;
        }

        for (var index = 0; index < profile.Bindings.Count; index++)
        {
            var binding = profile.Bindings[index];
            var parameters = binding.Parameters.Count == 0
                ? ""
                : string.Join(", ", binding.Parameters.Select(pair => $"{pair.Key}={pair.Value}"));

            ProfileBindings.Add(new BindingViewModel(index, binding.Input, binding.Action, parameters));
        }

        isRefreshingEditor = true;
        SelectedBinding = selectedIndex >= 0 && selectedIndex < ProfileBindings.Count
            ? ProfileBindings[selectedIndex]
            : null;
        isRefreshingEditor = false;

        OnSelectedBindingChanged(SelectedBinding);
    }

    private ProfileConfig? GetActiveProfile()
    {
        return currentConfig.Profiles.FirstOrDefault(
            candidate => candidate.Name.Equals(currentConfig.ActiveProfile, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateActiveProfile(ProfileConfig updatedProfile)
    {
        currentConfig = currentConfig with
        {
            Profiles = currentConfig.Profiles
                .Select(profile => profile.Name.Equals(currentConfig.ActiveProfile, StringComparison.OrdinalIgnoreCase)
                    ? updatedProfile
                    : profile)
                .ToList()
        };
    }

    private bool TryCreateEditorBinding(out ActionBinding binding)
    {
        binding = new ActionBinding();

        var input = BindingEditorInput.Trim();
        var action = BindingEditorAction.Trim();
        BindingEditorValidationMessage = "";

        if (string.IsNullOrWhiteSpace(input))
        {
            SetBindingEditorError("Binding input is required.");
            return false;
        }

        if (!IsSupportedInput(input))
        {
            SetBindingEditorError("Input must look like button:1 or slider:1.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            SetBindingEditorError("Binding action is required.");
            return false;
        }

        if (!ActionDefinitionLookup.TryGetValue(action, out var actionDefinition))
        {
            SetBindingEditorError($"Unsupported action: {action}");
            return false;
        }

        var parameters = ParseParameters(BindingEditorParameters, action);
        foreach (var requiredParameter in actionDefinition.RequiredParameters)
        {
            if (!parameters.ContainsKey(requiredParameter))
            {
                SetBindingEditorError($"Missing parameter: {requiredParameter}");
                return false;
            }
        }

        if (action.Equals("system.openUrl", StringComparison.OrdinalIgnoreCase)
            && parameters.TryGetValue("url", out var url)
            && (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            SetBindingEditorError("URL must start with http:// or https://.");
            return false;
        }

        if (actionDefinition.RequiredParameters.Count == 0
            && !action.Equals("log", StringComparison.OrdinalIgnoreCase)
            && parameters.Count > 0)
        {
            AddLog($"Parameters are ignored for {action}.");
        }

        binding = new ActionBinding
        {
            Input = input,
            Action = action,
            Parameters = parameters
        };

        return true;
    }

    private void SetBindingEditorError(string message)
    {
        BindingEditorValidationMessage = message;
        AddLog(message);
    }

    private void RefreshActionEditorHints()
    {
        var action = BindingEditorAction.Trim();
        if (!ActionDefinitionLookup.TryGetValue(action, out var definition))
        {
            BindingActionDescription = "Custom or unsupported action.";
            BindingParameterHint = "Use key=value pairs separated by commas.";
            BindingParameterPlaceholder = "key=value";
            return;
        }

        BindingActionDescription = definition.Description;
        BindingParameterHint = definition.ParameterHint;
        BindingParameterPlaceholder = definition.ParameterPlaceholder;
    }

    private static bool IsSupportedInput(string input)
    {
        var separatorIndex = input.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == input.Length - 1)
        {
            return false;
        }

        var inputType = input[..separatorIndex];
        var id = input[(separatorIndex + 1)..];

        return (inputType.Equals("button", StringComparison.OrdinalIgnoreCase)
                || inputType.Equals("slider", StringComparison.OrdinalIgnoreCase))
            && int.TryParse(id, out var numericId)
            && numericId > 0;
    }

    private static bool TryValidateProfileName(string name, out string error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Profile name is required.";
            return false;
        }

        if (name.Contains(';') || name.Contains('='))
        {
            error = "Profile name cannot contain ';' or '='.";
            return false;
        }

        error = "";
        return true;
    }

    private static Dictionary<string, string> ParseParameters(string parameters, string action)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(parameters))
        {
            return result;
        }

        if (!parameters.Contains('=') && action.Equals("log", StringComparison.OrdinalIgnoreCase))
        {
            result["message"] = parameters.Trim();
            return result;
        }

        foreach (var part in parameters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = part[..separatorIndex].Trim();
            var value = part[(separatorIndex + 1)..].Trim();

            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static string GetDefaultConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "MakroPad", "config.json");
    }

    private static int NormalizeVolumeSliderDeadbandPercent(int value)
    {
        return Math.Clamp(value, 0, 25);
    }

    private void RefreshDisplayTriggers(int selectedIndex = 0)
    {
        DisplayTriggers.Clear();

        var triggers = currentConfig.AppSettings.DisplayTriggers;
        for (var index = 0; index < triggers.Count; index++)
        {
            var trigger = NormalizeDisplayTrigger(triggers[index]);
            DisplayTriggers.Add(new DisplayTriggerViewModel(
                index,
                trigger.Enabled,
                trigger.EventType,
                trigger.Input,
                trigger.ActionStatus,
                trigger.ConnectionStatus,
                trigger.Condition,
                trigger.MinPercent,
                trigger.MaxPercent,
                trigger.ChangePercent,
                trigger.DurationMs,
                trigger.TemplateMode,
                trigger.Template));
        }

        isRefreshingDisplayTriggerEditor = true;
        SelectedDisplayTrigger = selectedIndex >= 0 && selectedIndex < DisplayTriggers.Count
            ? DisplayTriggers[selectedIndex]
            : null;
        isRefreshingDisplayTriggerEditor = false;

        OnSelectedDisplayTriggerChanged(SelectedDisplayTrigger);
        displayTriggerInputTracker.Clear();
    }

    private void UpdateDisplayTriggers(List<DisplayTriggerConfig> triggers, int selectedIndex)
    {
        currentConfig = currentConfig with
        {
            AppSettings = currentConfig.AppSettings with
            {
                DisplayTriggers = triggers.Select(NormalizeDisplayTrigger).ToList()
            }
        };

        RefreshDisplayTriggers(selectedIndex);
    }

    private List<DisplayTriggerConfig> CreateDisplayTriggerConfigsFromList()
    {
        return DisplayTriggers
            .Select(trigger => NormalizeDisplayTrigger(new DisplayTriggerConfig
            {
                Enabled = trigger.Enabled,
                EventType = trigger.EventType,
                Input = trigger.Input,
                ActionStatus = trigger.ActionStatus,
                ConnectionStatus = trigger.ConnectionStatus,
                Condition = trigger.Condition,
                MinPercent = trigger.MinPercent,
                MaxPercent = trigger.MaxPercent,
                ChangePercent = trigger.ChangePercent,
                DurationMs = trigger.DurationMs,
                TemplateMode = trigger.TemplateMode,
                Template = trigger.Template
            }))
            .ToList();
    }

    private bool TryCreateEditorDisplayTrigger(out DisplayTriggerConfig trigger)
    {
        trigger = new DisplayTriggerConfig();
        DisplayTriggerEditorValidationMessage = "";

        var eventType = NormalizeDisplayTriggerEventType(DisplayTriggerEditorEventType);
        var input = eventType is "mediaChanged" or "connectionChanged" or "profileChanged"
            ? "*"
            : DisplayTriggerEditorInput.Trim();
        if (input != "*" && !IsSupportedInput(input))
        {
            SetDisplayTriggerEditorError("Input must be * or look like button:1 or slider:1.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(DisplayTriggerEditorTemplate))
        {
            SetDisplayTriggerEditorError("Display content is required.");
            return false;
        }

        if (eventType == "input"
            && input.StartsWith("slider:", StringComparison.OrdinalIgnoreCase)
            && DisplayTriggerEditorMinPercent > DisplayTriggerEditorMaxPercent)
        {
            SetDisplayTriggerEditorError("Min % must not be greater than Max %.");
            return false;
        }

        if (eventType == "input"
            && !input.StartsWith("slider:", StringComparison.OrdinalIgnoreCase)
            && NormalizeDisplayTriggerCondition(DisplayTriggerEditorCondition) != "any")
        {
            SetDisplayTriggerEditorError("Slider conditions require a slider input.");
            return false;
        }

        trigger = NormalizeDisplayTrigger(new DisplayTriggerConfig
        {
            Enabled = DisplayTriggerEditorEnabled,
            EventType = eventType,
            Input = input,
            ActionStatus = DisplayTriggerEditorActionStatus,
            ConnectionStatus = DisplayTriggerEditorConnectionStatus,
            Condition = DisplayTriggerEditorCondition,
            MinPercent = DisplayTriggerEditorMinPercent,
            MaxPercent = DisplayTriggerEditorMaxPercent,
            ChangePercent = DisplayTriggerEditorChangePercent,
            DurationMs = DisplayTriggerEditorDurationMs,
            TemplateMode = DisplayTriggerEditorTemplateMode,
            Template = DisplayTriggerEditorTemplate
        });

        return true;
    }

    private void SetDisplayTriggerEditorError(string message)
    {
        DisplayTriggerEditorValidationMessage = message;
        AddLog(message);
    }

    private static DisplayTriggerConfig NormalizeDisplayTrigger(DisplayTriggerConfig trigger)
    {
        return DisplayTriggerNormalizer.Normalize(trigger);
    }

    private static int NormalizeDisplayTriggerChangePercent(int value)
    {
        return DisplayTriggerNormalizer.NormalizeChangePercent(value);
    }

    private static int NormalizeDisplayTriggerDurationMs(int value)
    {
        return DisplayTriggerNormalizer.NormalizeDurationMs(value);
    }

    private static int NormalizePercent(int value)
    {
        return DisplayTriggerNormalizer.NormalizePercent(value);
    }

    private static string NormalizeDisplayTriggerEventType(string? value)
    {
        return DisplayTriggerNormalizer.NormalizeEventType(value);
    }

    private static string NormalizeActionStatus(string? value)
    {
        return DisplayTriggerNormalizer.NormalizeActionStatus(value);
    }

    private static string NormalizeConnectionStatus(string? value)
    {
        return DisplayTriggerNormalizer.NormalizeConnectionStatus(value);
    }

    private static string NormalizeDisplayTriggerCondition(string? value)
    {
        return DisplayTriggerNormalizer.NormalizeCondition(value);
    }

    private static bool DisplayTriggerConditionMatches(DisplayTriggerConfig trigger, int percent, int previousPercent)
    {
        return DisplayTriggerMatcher.ConditionMatches(trigger, percent, previousPercent);
    }

    private static MakroPadConfig EnsureDefaultActionResultDisplayTriggers(MakroPadConfig config)
    {
        if (config.AppSettings.DisplayTriggerDefaultsInitialized)
        {
            return config;
        }

        var triggers = config.AppSettings.DisplayTriggers.ToList();
        if (!triggers.Any(trigger => NormalizeDisplayTriggerEventType(trigger.EventType) == "actionResult"))
        {
            triggers.Add(
                new DisplayTriggerConfig
                {
                    Enabled = true,
                    EventType = "actionResult",
                    Input = "*",
                    ActionStatus = "ok",
                    DurationMs = config.Hardware.Display.ActionResultTimeoutMs,
                    Template = "Action\n{action.message}"
                });
            triggers.Add(
                new DisplayTriggerConfig
                {
                    Enabled = true,
                    EventType = "actionResult",
                    Input = "*",
                    ActionStatus = "failed",
                    DurationMs = config.Hardware.Display.ActionResultTimeoutMs,
                    Template = "Action failed\n{action.message}"
                });
        }

        return config with
        {
            AppSettings = config.AppSettings with
            {
                DisplayTriggerDefaultsInitialized = true,
                DisplayTriggers = triggers
            }
        };
    }

    private static MakroPadConfig EnsureDefaultMediaChangedDisplayTrigger(MakroPadConfig config)
    {
        if (config.AppSettings.MediaTriggerDefaultsInitialized)
        {
            return config;
        }

        var triggers = config.AppSettings.DisplayTriggers.ToList();
        if (!triggers.Any(trigger => NormalizeDisplayTriggerEventType(trigger.EventType) == "mediaChanged"))
        {
            triggers.Add(
                new DisplayTriggerConfig
                {
                    Enabled = true,
                    EventType = "mediaChanged",
                    Input = "*",
                    DurationMs = 0,
                    Template = "Media\n{media.artist}\n{media.title}"
                });
        }

        return config with
        {
            AppSettings = config.AppSettings with
            {
                MediaTriggerDefaultsInitialized = true,
                DisplayTriggers = triggers
            }
        };
    }

    private static int MapSliderValueToPercent(int sliderValue)
    {
        return DisplayTriggerRenderer.MapSliderValueToPercent(sliderValue);
    }

    private List<string> RenderDisplayTriggerLines(
        DisplayTriggerConfig trigger,
        string input,
        int? sliderValue,
        int? percent,
        string actionStatus = "",
        string actionName = "",
        string actionMessage = "",
        string mediaArtist = "",
        string mediaTitle = "",
        string connectionStatus = "")
    {
        return DisplayTriggerRenderer.RenderLines(
            trigger,
            new DisplayTriggerRenderContext
            {
                Input = input,
                SliderValue = sliderValue,
                SliderPercent = percent,
                ActionStatus = actionStatus,
                ActionName = actionName,
                ActionMessage = actionMessage,
                MediaArtist = mediaArtist,
                MediaTitle = mediaTitle,
                ConnectionStatus = connectionStatus,
                Profile = ActiveProfileName,
                Status = ConnectionStatus
            }).ToList();
    }

    private void RefreshDisplayTriggerPreview()
    {
        OnPropertyChanged(nameof(DisplayTriggerPreviewMatchStatus));
        OnPropertyChanged(nameof(DisplayTriggerEditorContentWarning));
        OnPropertyChanged(nameof(IsDisplayTriggerEditorDirty));
        OnPropertyChanged(nameof(DisplayTriggerSequenceInfo));

        if (!TryCreateEditorDisplayTriggerForPreview(out var trigger))
        {
            SetDisplayTriggerPreviewLines([]);
            return;
        }

        var preview = CreateDisplayTriggerPreviewContext(trigger);
        SetDisplayTriggerPreviewLines(
            DisplayTriggerRenderer.FitPreviewLines(
                RenderDisplayTriggerLines(
                    trigger,
                    preview.Input,
                    preview.SliderValue,
                    preview.Percent,
                    preview.ActionStatus,
                    preview.ActionName,
                    preview.ActionMessage,
                    preview.MediaArtist,
                    preview.MediaTitle,
                    preview.ConnectionStatus)));
    }

    private DisplayTriggerPreviewContext CreateDisplayTriggerPreviewContext(DisplayTriggerConfig trigger)
    {
        var eventType = NormalizeDisplayTriggerEventType(trigger.EventType);
        var input = trigger.Input == "*" && eventType == "input" ? "slider:1" : trigger.Input;
        var percent = input.StartsWith("slider:", StringComparison.OrdinalIgnoreCase)
            ? NormalizePercent(DisplayTriggerPreviewPercent)
            : (int?)null;
        var sliderValue = percent is null
            ? (int?)null
            : (int)Math.Round(percent.Value / 100.0 * 4095);
        var actionStatus = eventType == "actionResult"
            ? NormalizeActionStatus(DisplayTriggerPreviewActionStatus) == "any"
                ? "ok"
                : NormalizeActionStatus(DisplayTriggerPreviewActionStatus)
            : "";
        var connectionStatus = NormalizeConnectionStatus(DisplayTriggerPreviewConnectionStatus) == "any"
            ? "connected"
            : NormalizeConnectionStatus(DisplayTriggerPreviewConnectionStatus);

        return new DisplayTriggerPreviewContext(
            input,
            sliderValue,
            percent,
            actionStatus,
            DisplayTriggerPreviewActionName,
            DisplayTriggerPreviewActionMessage,
            DisplayTriggerPreviewMediaArtist,
            DisplayTriggerPreviewMediaTitle,
            connectionStatus);
    }

    private bool TryCreateEditorDisplayTriggerForPreview(out DisplayTriggerConfig trigger)
    {
        trigger = NormalizeDisplayTrigger(new DisplayTriggerConfig
        {
            Enabled = DisplayTriggerEditorEnabled,
            EventType = DisplayTriggerEditorEventType,
            Input = string.IsNullOrWhiteSpace(DisplayTriggerEditorInput) ? "slider:1" : DisplayTriggerEditorInput,
            ActionStatus = DisplayTriggerEditorActionStatus,
            ConnectionStatus = DisplayTriggerEditorConnectionStatus,
            Condition = DisplayTriggerEditorCondition,
            MinPercent = DisplayTriggerEditorMinPercent,
            MaxPercent = DisplayTriggerEditorMaxPercent,
            ChangePercent = DisplayTriggerEditorChangePercent,
            DurationMs = DisplayTriggerEditorDurationMs,
            TemplateMode = DisplayTriggerEditorTemplateMode,
            Template = string.IsNullOrWhiteSpace(DisplayTriggerEditorTemplate)
                ? " "
                : DisplayTriggerEditorTemplate
        });

        return true;
    }

    private void SetDisplayTriggerPreviewLines(IReadOnlyList<string> lines)
    {
        DisplayTriggerPreviewLine1 = lines.Count > 0 ? lines[0] : "";
        DisplayTriggerPreviewLine2 = lines.Count > 1 ? lines[1] : "";
        DisplayTriggerPreviewLine3 = lines.Count > 2 ? lines[2] : "";
        DisplayTriggerPreviewLine4 = lines.Count > 3 ? lines[3] : "";
        DisplayTriggerPreviewLine5 = lines.Count > 4 ? lines[4] : "";
    }

    private bool DefaultScreenUsesMediaPlaceholder()
    {
        return deviceDisplaySyncService.UsesMediaPlaceholder(CreateDeviceDisplayState());
    }

    private void RefreshOledPreview()
    {
        OnPropertyChanged(nameof(OledPreviewTitle));
        OnPropertyChanged(nameof(OledPreviewLine1));
        OnPropertyChanged(nameof(OledPreviewLine2));
        OnPropertyChanged(nameof(OledPreviewLine3));
        OnPropertyChanged(nameof(OledPreviewLine4));
    }

    private OledDefaultScreenPreview RenderOledPreview()
    {
        return deviceDisplaySyncService.RenderPreview(CreateDeviceDisplayState());
    }

    private DeviceDisplayState CreateDeviceDisplayState()
    {
        return new DeviceDisplayState(
            ActiveProfileName,
            ConnectionStatus,
            ActionResultTimeoutMs,
            DefaultScreenRows,
            DefaultScreenTitle,
            DefaultScreenLine1,
            DefaultScreenLine2,
            DefaultScreenLine3,
            DefaultScreenLine4,
            EnableNowPlayingDisplay,
            NowPlayingViewModel.LastSentTrack);
    }

    private void RefreshStructuredParameterFromRaw()
    {
        var key = GetStructuredParameterKey(BindingEditorAction);
        isRefreshingEditor = true;
        StructuredParameterValue = key is null
            ? ""
            : ParseParameters(BindingEditorParameters, BindingEditorAction).GetValueOrDefault(key, "");
        isRefreshingEditor = false;
    }

    private void UpdateRawParametersFromStructuredValue()
    {
        var key = GetStructuredParameterKey(BindingEditorAction);
        if (key is null)
        {
            return;
        }

        isRefreshingEditor = true;
        BindingEditorParameters = string.IsNullOrWhiteSpace(StructuredParameterValue)
            ? ""
            : $"{key}={StructuredParameterValue.Trim()}";
        isRefreshingEditor = false;
    }

    private static string? GetStructuredParameterKey(string action)
    {
        return action switch
        {
            "app.volume" => "process",
            "system.openUrl" => "url",
            "system.openApp" => "path",
            "keyboard.shortcut" => "keys",
            "log" => "message",
            _ => null
        };
    }

    public bool HasStructuredParameter => GetStructuredParameterKey(BindingEditorAction) is not null;

    public bool IsAppVolumeAction => BindingEditorAction.Equals("app.volume", StringComparison.OrdinalIgnoreCase);

    public bool IsKeyboardShortcutAction => BindingEditorAction.Equals("keyboard.shortcut", StringComparison.OrdinalIgnoreCase);

    public bool IsOpenAppAction => BindingEditorAction.Equals("system.openApp", StringComparison.OrdinalIgnoreCase);

    public bool ShowsStructuredTextInput => HasStructuredParameter && !IsAppVolumeAction;

    public string StructuredParameterValidationMessage
    {
        get
        {
            if (!HasStructuredParameter || string.IsNullOrWhiteSpace(StructuredParameterValue))
            {
                return "";
            }

            if (BindingEditorAction.Equals("system.openUrl", StringComparison.OrdinalIgnoreCase)
                && (!Uri.TryCreate(StructuredParameterValue, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            {
                return "URL must start with http:// or https://.";
            }

            if (BindingEditorAction.Equals("app.volume", StringComparison.OrdinalIgnoreCase)
                && !AvailableAudioProcessNames.Contains(StructuredParameterValue, StringComparer.OrdinalIgnoreCase))
            {
                return "Process is not in the current audio session list.";
            }

            return "";
        }
    }

    public string StructuredParameterLabel => BindingEditorAction switch
    {
        "app.volume" => "Process",
        "system.openUrl" => "URL",
        "system.openApp" => "App or file",
        "keyboard.shortcut" => "Shortcut",
        "log" => "Message",
        _ => "Parameter"
    };

    private sealed record ActionDefinition(
        string Id,
        string Description,
        string ParameterHint,
        string ParameterPlaceholder,
        IReadOnlyList<string> RequiredParameters);
}

public sealed record DisplayTriggerViewModel(
    int Index,
    bool Enabled,
    string EventType,
    string Input,
    string ActionStatus,
    string ConnectionStatus,
    string Condition,
    int MinPercent,
    int MaxPercent,
    int ChangePercent,
    int DurationMs,
    string TemplateMode,
    string Template)
{
    public string SequenceDisplay => (Index + 1).ToString();

    public string EnabledDisplay => Enabled ? "Yes" : "No";

    public string ChangeDisplay => EventType == "input"
        && Input.StartsWith("slider:", StringComparison.OrdinalIgnoreCase)
            ? $"{ChangePercent}%"
            : "-";

    public string TemplateSummary
    {
        get
        {
            var firstLine = Template
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(firstLine) ? "(empty)" : firstLine;
        }
    }

    public string ConditionDisplay => Condition == "inRange"
        ? $"{Condition} {MinPercent}-{MaxPercent}%"
        : Condition;

    public string MatchDisplay => EventType == "actionResult"
        ? $"{Input} / {ActionStatus}"
        : EventType == "mediaChanged"
            ? "*"
            : EventType == "connectionChanged"
                ? ConnectionStatus
                : EventType == "profileChanged"
                    ? "*"
                    : $"{Input} / {ConditionDisplay}";
}

public sealed record DisplayTriggerPreset(
    string Name,
    string EventType,
    string Input,
    string ActionStatus,
    string ConnectionStatus,
    string Condition,
    int MinPercent,
    int MaxPercent,
    int ChangePercent,
    int DurationMs,
    string TemplateMode,
    string Template);

public sealed record DisplayTriggerPreviewContext(
    string Input,
    int? SliderValue,
    int? Percent,
    string ActionStatus,
    string ActionName,
    string ActionMessage,
    string MediaArtist,
    string MediaTitle,
    string ConnectionStatus);
