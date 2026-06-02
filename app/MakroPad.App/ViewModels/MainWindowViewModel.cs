using System.Collections.ObjectModel;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.App.Services;
using MakroPad.Actions;
using MakroPad.Core.Configuration;
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
    private readonly Dictionary<string, int> lastDisplayTriggerPercentByInput = new(StringComparer.OrdinalIgnoreCase);

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

        DisplayTriggerEditorEnabled = value?.Enabled ?? true;
        DisplayTriggerEditorInput = value?.Input ?? "slider:1";
        DisplayTriggerEditorChangePercent = value?.ChangePercent ?? 5;
        DisplayTriggerEditorDurationMs = value?.DurationMs ?? 3000;
        DisplayTriggerEditorTemplateMode = value?.TemplateMode ?? "normal";
        DisplayTriggerEditorTemplate = value?.Template ?? "Slider {slider.id}\n{slider.percent}%";
        DisplayTriggerEditorValidationMessage = "";
    }

    [ObservableProperty]
    private bool displayTriggerEditorEnabled = true;

    [ObservableProperty]
    private string displayTriggerEditorInput = "slider:1";

    [ObservableProperty]
    private int displayTriggerEditorChangePercent = 5;

    [ObservableProperty]
    private int displayTriggerEditorDurationMs = 3000;

    [ObservableProperty]
    private string displayTriggerEditorTemplateMode = "normal";

    [ObservableProperty]
    private string displayTriggerEditorTemplate = "Slider {slider.id}\n{slider.percent}%";

    [ObservableProperty]
    private string displayTriggerEditorValidationMessage = "";

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
        _ = SendDeviceConfigAsync();
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

    public IReadOnlyList<int> AvailableButtonIds { get; } =
        Enumerable.Range(1, 20).ToArray();

    public IReadOnlyList<int> AvailableSliderIds { get; } =
        Enumerable.Range(1, 3).ToArray();

    public IReadOnlyList<string> AvailableDisplayTemplateModes { get; } = ["normal", "html"];

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

            if (activeConnection.State == DeviceConnectionState.Connected)
            {
                await SendDeviceConfigAsync();
            }
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
            currentConfig = await configStore.LoadOrCreateDefaultAsync();
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
    }

    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        try
        {
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
                    DisplayTriggers = CreateDisplayTriggerConfigsFromList(),
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

        var input = trigger.Input;
        var sliderValue = input.StartsWith("slider:", StringComparison.OrdinalIgnoreCase)
            ? LastSliderValue
            : (int?)null;
        var percent = sliderValue is null ? (int?)null : MapSliderValueToPercent(sliderValue.Value);
        var lines = RenderDisplayTriggerLines(trigger, input, sliderValue, percent);

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
            await HandleConfiguredInputAsync(input);
            await HandleDisplayTriggersAsync(input);
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
                await HandleConfiguredInputAsync(input, value);
                if (hasSliderValue)
                {
                    await HandleDisplayTriggersAsync(input, sliderValue);
                }
            }
        }
    }

    private async Task HandleDisplayTriggersAsync(string input, int? sliderValue = null)
    {
        var triggers = currentConfig.AppSettings.DisplayTriggers
            .Where(candidate => candidate.Enabled
                && candidate.Input.Equals(input, StringComparison.OrdinalIgnoreCase))
            .Select(NormalizeDisplayTrigger)
            .ToList();

        if (triggers.Count == 0)
        {
            if (sliderValue is not null)
            {
                lastDisplayTriggerPercentByInput[input] = MapSliderValueToPercent(sliderValue.Value);
            }
            return;
        }

        var percent = sliderValue is null ? (int?)null : MapSliderValueToPercent(sliderValue.Value);
        if (percent is not null)
        {
            if (!lastDisplayTriggerPercentByInput.TryGetValue(input, out var previousPercent))
            {
                lastDisplayTriggerPercentByInput[input] = percent.Value;
                return;
            }

            var threshold = triggers.Min(trigger => NormalizeDisplayTriggerChangePercent(trigger.ChangePercent));
            if ((threshold == 0 && percent == previousPercent)
                || (threshold > 0 && Math.Abs(percent.Value - previousPercent) < threshold))
            {
                return;
            }

            lastDisplayTriggerPercentByInput[input] = percent.Value;
        }

        try
        {
            await SendDisplayTriggerSequenceAsync(triggers, input, sliderValue, percent);
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
        int? percent)
    {
        var sentCount = 0;
        for (var index = 0; index < triggers.Count; index++)
        {
            var trigger = triggers[index];
            var lines = RenderDisplayTriggerLines(trigger, input, sliderValue, percent);
            if (lines.Count == 0)
            {
                continue;
            }

            var durationMs = NormalizeDisplayTriggerDurationMs(trigger.DurationMs);
            await deviceDisplaySyncService.SendTemporaryTemplateAsync(activeConnection, lines, durationMs);
            sentCount++;

            if (index < triggers.Count - 1 && durationMs > 0)
            {
                await Task.Delay(durationMs);
            }
        }

        if (sentCount > 1)
        {
            AddLog($"Display trigger sequence sent: {input} ({sentCount} screens)");
        }
    }

    private async Task HandleConfiguredInputAsync(string input, string? value = null)
    {
        var result = await actionPipeline.HandleInputAsync(currentConfig, input, value);

        if (result.Kind == ActionPipelineResultKind.Throttled)
        {
            return;
        }

        AddLog(FormatActionResult(result));

        if (activeConnection is not null && result.ShouldSendFeedback && result.ExecutionResult is not null)
        {
            var status = result.ExecutionResult.Succeeded ? "ok" : "failed";
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
            }
            catch (Exception ex)
            {
                AddLog($"Action feedback failed: {ex.Message}");
            }
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
                trigger.Input,
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
        lastDisplayTriggerPercentByInput.Clear();
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
                Input = trigger.Input,
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

        var input = DisplayTriggerEditorInput.Trim();
        if (!IsSupportedInput(input))
        {
            SetDisplayTriggerEditorError("Input must look like button:1 or slider:1.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(DisplayTriggerEditorTemplate))
        {
            SetDisplayTriggerEditorError("Display content is required.");
            return false;
        }

        trigger = NormalizeDisplayTrigger(new DisplayTriggerConfig
        {
            Enabled = DisplayTriggerEditorEnabled,
            Input = input,
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
        return trigger with
        {
            Input = string.IsNullOrWhiteSpace(trigger.Input) ? "slider:1" : trigger.Input.Trim(),
            ChangePercent = NormalizeDisplayTriggerChangePercent(trigger.ChangePercent),
            DurationMs = NormalizeDisplayTriggerDurationMs(trigger.DurationMs),
            TemplateMode = NormalizeDisplayTriggerTemplateMode(trigger.TemplateMode),
            Template = string.IsNullOrWhiteSpace(trigger.Template)
                ? "Slider {slider.id}\n{slider.percent}%"
                : trigger.Template.Trim()
        };
    }

    private static int NormalizeDisplayTriggerChangePercent(int value)
    {
        return Math.Clamp(value, 0, 100);
    }

    private static int NormalizeDisplayTriggerDurationMs(int value)
    {
        return Math.Clamp(value, 0, 60000);
    }

    private static string NormalizeDisplayTriggerTemplateMode(string? value)
    {
        return value?.Equals("html", StringComparison.OrdinalIgnoreCase) == true ? "html" : "normal";
    }

    private static bool TryParseInputId(string input, out int id)
    {
        id = 0;
        var separatorIndex = input.IndexOf(':');
        return separatorIndex >= 0
            && int.TryParse(input[(separatorIndex + 1)..], out id)
            && id > 0;
    }

    private static int MapSliderValueToPercent(int sliderValue)
    {
        return Math.Clamp((int)Math.Round(Math.Clamp(sliderValue, 0, 4095) / 4095.0 * 100), 0, 100);
    }

    private List<string> RenderDisplayTriggerLines(DisplayTriggerConfig trigger, string input, int? sliderValue, int? percent)
    {
        var text = ReplaceDisplayTriggerPlaceholders(trigger.Template, input, sliderValue, percent);
        if (NormalizeDisplayTriggerTemplateMode(trigger.TemplateMode) == "html")
        {
            text = RenderHtmlLikeDisplayText(text);
        }

        return text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 0)
            .Take(5)
            .ToList();
    }

    private string ReplaceDisplayTriggerPlaceholders(string template, string input, int? sliderValue, int? percent)
    {
        var sliderId = TryParseInputId(input, out var id) ? id.ToString() : "";
        return template
            .Replace("{input}", input, StringComparison.OrdinalIgnoreCase)
            .Replace("{slider.id}", sliderId, StringComparison.OrdinalIgnoreCase)
            .Replace("{slider.value}", sliderValue?.ToString() ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{slider.percent}", percent?.ToString() ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{profile}", ActiveProfileName, StringComparison.OrdinalIgnoreCase)
            .Replace("{status}", ConnectionStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static string RenderHtmlLikeDisplayText(string html)
    {
        var text = Regex.Replace(html, "(?i)<\\s*br\\s*/?\\s*>", "\n");
        text = Regex.Replace(text, "(?i)</\\s*(p|div|h[1-6]|li|tr)\\s*>", "\n");
        text = Regex.Replace(text, "(?i)<\\s*li\\s*>", "- ");
        text = Regex.Replace(text, "<[^>]+>", "");
        return WebUtility.HtmlDecode(text);
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
    string Input,
    int ChangePercent,
    int DurationMs,
    string TemplateMode,
    string Template)
{
    public string EnabledDisplay => Enabled ? "Yes" : "No";

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
}
