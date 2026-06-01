using System.Collections.ObjectModel;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
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
    private readonly ActionPipeline actionPipeline = new(new ActionResolver(), new ActionExecutor());
    private readonly INowPlayingProvider nowPlayingProvider = new ProcessWindowNowPlayingProvider();
    private readonly CancellationTokenSource nowPlayingCancellation = new();
    private IMakroPadConnection? activeConnection;
    private MakroPadConfig currentConfig = MakroPadConfig.CreateDefault();
    private NowPlayingTrack? lastSentNowPlayingTrack;
    private bool isRefreshingEditor;

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

    partial void OnEnableNowPlayingDisplayChanged(bool value)
    {
        if (!value)
        {
            lastSentNowPlayingTrack = null;
        }

        _ = SendDeviceConfigAsync();
    }

    [ObservableProperty]
    private string nowPlayingStatus = "Now Playing idle";

    [ObservableProperty]
    private int actionResultTimeoutMs = 3000;

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
    }

    [ObservableProperty]
    private string bindingEditorParameters = "";

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
    }

    public ObservableCollection<string> AvailablePorts { get; } = [];

    public ObservableCollection<string> EventLog { get; } = [];

    public ObservableCollection<string> ProfileNames { get; } = [];

    public ObservableCollection<BindingViewModel> ProfileBindings { get; } = [];

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

    public MainWindowViewModel()
    {
        ConfigPath = GetDefaultConfigPath();
        configStore = new MakroPadConfigStore(ConfigPath);
        RefreshActionEditorHints();
        RefreshPorts();
        _ = RunNowPlayingLoopAsync(nowPlayingCancellation.Token);
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
            ActionResultTimeoutMs = currentConfig.Hardware.Display.ActionResultTimeoutMs;

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
                        ActionResultTimeoutMs = Math.Max(0, ActionResultTimeoutMs)
                    }
                },
                AppSettings = currentConfig.AppSettings with
                {
                    AutoConnect = AutoConnect,
                    EnableNowPlayingDisplay = EnableNowPlayingDisplay,
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

            await HandleConfiguredInputAsync($"button:{id}");
            return;
        }

        if (message.Type.Equals("slider", StringComparison.OrdinalIgnoreCase))
        {
            var id = message.GetValue("id");
            var value = message.GetValue("value");

            if (int.TryParse(value, out var sliderValue))
            {
                LastSliderValue = sliderValue;
            }

            if (id is not null)
            {
                await HandleConfiguredInputAsync($"slider:{id}", value);
            }
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
        if (activeConnection is null || activeConnection.State != DeviceConnectionState.Connected)
        {
            return;
        }

        var line = MakroPad.Core.Protocol.ProtocolCodec.Format(
            "config",
            new Dictionary<string, string?>
            {
                ["profile"] = ActiveProfileName,
                ["actionResultTimeoutMs"] = Math.Max(0, ActionResultTimeoutMs).ToString(),
                ["preferredDisplay"] = EnableNowPlayingDisplay && lastSentNowPlayingTrack is not null
                    ? "media"
                    : "profile"
            });

        try
        {
            await activeConnection.SendLineAsync(line);
        }
        catch (Exception ex)
        {
            AddLog($"Device config send failed: {ex.Message}");
        }
    }

    private async Task RunNowPlayingLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(cancellationToken);
                await UpdateNowPlayingAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() => NowPlayingStatus = $"Now Playing failed: {ex.Message}");
            }
        }
    }

    private async Task UpdateNowPlayingAsync(CancellationToken cancellationToken)
    {
        if (!EnableNowPlayingDisplay)
        {
            lastSentNowPlayingTrack = null;
            Dispatcher.UIThread.Post(() => NowPlayingStatus = "Now Playing disabled");
            return;
        }

        if (activeConnection is null || activeConnection.State != DeviceConnectionState.Connected)
        {
            Dispatcher.UIThread.Post(() => NowPlayingStatus = "Now Playing waiting for device");
            return;
        }

        var track = await nowPlayingProvider.GetCurrentTrackAsync(cancellationToken);
        if (track is null)
        {
            if (lastSentNowPlayingTrack is not null)
            {
                lastSentNowPlayingTrack = null;
                await SendDeviceConfigAsync();
            }

            Dispatcher.UIThread.Post(() => NowPlayingStatus = "No Spotify track detected");
            return;
        }

        Dispatcher.UIThread.Post(() => NowPlayingStatus = $"{track.Artist} - {track.Title}");

        if (track == lastSentNowPlayingTrack)
        {
            return;
        }

        var line = MakroPad.Core.Protocol.ProtocolCodec.Format(
            "display",
            new Dictionary<string, string?>
            {
                ["mode"] = "media",
                ["artist"] = track.Artist,
                ["title"] = track.Title
            });

        try
        {
            await activeConnection.SendLineAsync(line, cancellationToken);
            lastSentNowPlayingTrack = track;
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => AddLog($"Now Playing display failed: {ex.Message}"));
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

    private sealed record ActionDefinition(
        string Id,
        string Description,
        string ParameterHint,
        string ParameterPlaceholder,
        IReadOnlyList<string> RequiredParameters);
}
