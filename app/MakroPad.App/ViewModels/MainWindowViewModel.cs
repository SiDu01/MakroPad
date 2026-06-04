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
using MakroPad.Core.DisplayTriggers;
using MakroPad.Core.Input;
using MakroPad.Device;

namespace MakroPad.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly DeviceSessionService deviceSessionService = new();
    private readonly MakroPadConfigService configService;
    private readonly DeviceDisplaySyncService deviceDisplaySyncService;
    private readonly ActionDefinitionCatalog actionDefinitionCatalog = new();
    private readonly BindingEditorService bindingEditorService;
    private readonly ProfileConfigService profileConfigService = new();
    private readonly ProtocolInputDecoder protocolInputDecoder = new();
    private readonly EventLogService eventLogService = new();
    private readonly DisplayTriggerOrchestrator displayTriggerOrchestrator;
    private readonly ActionPipeline actionPipeline = new(new ActionResolver(), new ActionExecutor());
    private readonly ActionFeedbackCoordinator actionFeedbackCoordinator;
    private readonly CancellationTokenSource nowPlayingCancellation = new();
    private readonly CancellationTokenSource audioSessionsCancellation = new();
    private IMakroPadConnection? activeConnection => deviceSessionService.ActiveConnection;
    private MakroPadConfig currentConfig = MakroPadConfig.CreateDefault();
    private bool isRefreshingEditor;
    private bool isLoadingConfig;

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
        RefreshDisplayTriggerEditorRuntime();
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
        ActionEditor.Load(value?.Action, value?.Parameters);
    }

    [ObservableProperty]
    private string bindingEditorInput = "";

    [ObservableProperty]
    private string bindingEditorValidationMessage = "";

    partial void OnActiveProfileNameChanged(string value)
    {
        ProfileEditorName = value;
        currentConfig = currentConfig with { ActiveProfile = value };
        RefreshProfileBindings();
        RefreshOledPreview();
        RefreshDisplayTriggerEditorRuntime();
        if (!isLoadingConfig)
        {
            _ = HandleActiveProfileDisplayAsync(value);
        }
    }

    public string SelectedPortDisplay => string.IsNullOrWhiteSpace(SelectedPort)
        ? "No port selected"
        : SelectedPort;

    public bool IsSimulatorSelected => SelectedPort == DeviceSessionService.SimulatorPort;

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
        RefreshDisplayTriggerEditorRuntime();
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

    public ObservableCollection<string> EventLog => eventLogService.Entries;

    public AudioSessionsViewModel AudioSessionsViewModel { get; }

    public NowPlayingViewModel NowPlayingViewModel { get; }

    public string NowPlayingStatus => NowPlayingViewModel.NowPlayingStatus;

    public ObservableCollection<ApplicationAudioSession> AudioSessions => AudioSessionsViewModel.AudioSessions;

    public string AudioSessionsStatus => AudioSessionsViewModel.AudioSessionsStatus;

    public IAsyncRelayCommand RefreshAudioSessionsCommand => AudioSessionsViewModel.RefreshAudioSessionsCommand;

    public ObservableCollection<string> ProfileNames { get; } = [];

    public ObservableCollection<BindingViewModel> ProfileBindings { get; } = [];

    public ActionEditorViewModel ActionEditor { get; }

    public DisplayTriggersEditorViewModel DisplayTriggersEditor { get; }

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
        configService = new MakroPadConfigService(new MakroPadConfigStore(ConfigPath));
        deviceDisplaySyncService = new DeviceDisplaySyncService(SystemVolumeControllerFactory.Create());
        displayTriggerOrchestrator = new DisplayTriggerOrchestrator(
            new DeviceDisplayTriggerSink(deviceDisplaySyncService, () => activeConnection));
        actionFeedbackCoordinator = new ActionFeedbackCoordinator(
            actionPipeline,
            displayTriggerOrchestrator,
            deviceSessionService);
        bindingEditorService = new BindingEditorService(actionDefinitionCatalog);
        AudioSessionsViewModel = new AudioSessionsViewModel(ApplicationVolumeControllerFactory.Create());
        ActionEditor = new ActionEditorViewModel(
            actionDefinitionCatalog,
            bindingEditorService,
            AudioSessionsViewModel.AvailableAudioProcessNames);
        DisplayTriggersEditor = new DisplayTriggersEditorViewModel();
        DisplayTriggersEditor.Changed += OnDisplayTriggersEditorChanged;
        DisplayTriggersEditor.TestRequested += OnDisplayTriggerTestRequested;
        deviceSessionService.EventReceived += OnDeviceEventReceived;
        deviceSessionService.StateChanged += OnDeviceStateChanged;
        NowPlayingViewModel = new NowPlayingViewModel(new ProcessWindowNowPlayingProvider());
        AudioSessionsViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AudioSessionsViewModel.AudioSessionsStatus))
            {
                OnPropertyChanged(nameof(AudioSessionsStatus));
            }
        };
        AudioSessionsViewModel.SessionsRefreshed += (_, _) =>
            ActionEditor.NotifyAudioProcessNamesChanged();
        NowPlayingViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(NowPlayingViewModel.NowPlayingStatus))
            {
                OnPropertyChanged(nameof(NowPlayingStatus));
                RefreshOledPreview();
            }
        };
        NowPlayingViewModel.TrackChanged += (_, _) =>
        {
            RefreshOledPreview();
            RefreshDisplayTriggerEditorRuntime();
        };
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
        foreach (var port in deviceSessionService.GetAvailablePorts())
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
            ConnectionStatus = "Connecting";
            await deviceSessionService.ConnectAsync(SelectedPort);
            ConnectionStatus = deviceSessionService.State.ToString();

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
            await deviceSessionService.SendDisplayMessageAsync(DisplayText);
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
            var loadResult = await configService.LoadAsync();
            currentConfig = loadResult.Config;
            ActiveProfileName = currentConfig.ActiveProfile;
            AutoConnect = currentConfig.AppSettings.AutoConnect;
            EnableNowPlayingDisplay = currentConfig.AppSettings.EnableNowPlayingDisplay;
            VolumeSliderDeadbandPercent = NormalizeVolumeSliderDeadbandPercent(currentConfig.AppSettings.VolumeSliderDeadbandPercent);
            LoadDisplayTriggersEditor();
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
            if (loadResult.WasMigrated)
            {
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
            if (!DisplayTriggersEditor.TryCommitPendingEdit())
            {
                AddLog("Config save stopped because the selected display trigger is invalid.");
                return;
            }

            var displayTriggers = DisplayTriggersEditor.Export().ToList();

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
                    LastPort = SelectedPort == DeviceSessionService.SimulatorPort ? currentConfig.AppSettings.LastPort : SelectedPort
                }
            };
            await configService.SaveAsync(currentConfig);
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

        RefreshPorts();

        AddLog("Scanning for MakroPad...");
        var detectedPort = await deviceSessionService.DetectMakroPadPortAsync(currentConfig.AppSettings.LastPort);

        if (detectedPort is null)
        {
            AddLog("No MakroPad device found.");
            return;
        }

        SelectedPort = detectedPort;
        AddLog($"MakroPad detected on {detectedPort}.");
        await Task.Delay(250);
        await ConnectAsync();

        if (deviceSessionService.State == DeviceConnectionState.Connected)
        {
            currentConfig = currentConfig with
            {
                AppSettings = currentConfig.AppSettings with { LastPort = detectedPort }
            };

            await configService.SaveAsync(currentConfig);
        }
    }

    [RelayCommand]
    private void SimulateButton()
    {
        if (deviceSessionService.TrySimulateButtonPress(SimulatorButtonId))
        {
            return;
        }

        AddLog("Button simulation is only available on the simulator connection.");
    }

    [RelayCommand]
    private void SimulateSlider()
    {
        LastSliderValue = LastSliderValue >= 4000 ? 0 : LastSliderValue + 512;
        if (deviceSessionService.TrySimulateSliderChange(SimulatorSliderId, LastSliderValue))
        {
            return;
        }

        AddLog("Slider simulation is only available on the simulator connection.");
    }

    [RelayCommand]
    private void AddProfile()
    {
        var result = profileConfigService.AddProfile(currentConfig, ProfileEditorName);
        if (!result.IsSuccess || result.Value is null)
        {
            AddLog(result.ErrorMessage);
            return;
        }

        currentConfig = result.Value;
        RefreshProfileNames();
        ActiveProfileName = currentConfig.ActiveProfile;
        AddLog($"Profile added: {currentConfig.ActiveProfile}");
    }

    [RelayCommand]
    private void RenameProfile()
    {
        var oldName = currentConfig.ActiveProfile;
        var result = profileConfigService.RenameActiveProfile(currentConfig, ProfileEditorName);
        if (!result.IsSuccess || result.Value is null)
        {
            AddLog(result.ErrorMessage);
            return;
        }

        currentConfig = result.Value;
        RefreshProfileNames();
        ActiveProfileName = currentConfig.ActiveProfile;
        if (!oldName.Equals(currentConfig.ActiveProfile, StringComparison.OrdinalIgnoreCase))
        {
            AddLog($"Profile renamed: {oldName} -> {currentConfig.ActiveProfile}");
        }
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        var deletedName = currentConfig.ActiveProfile;
        var result = profileConfigService.DeleteActiveProfile(currentConfig);
        if (!result.IsSuccess || result.Value is null)
        {
            AddLog(result.ErrorMessage);
            return;
        }

        currentConfig = result.Value;
        RefreshProfileNames();
        ActiveProfileName = currentConfig.ActiveProfile;
        AddLog($"Profile deleted: {deletedName}");
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

    private void OnDisplayTriggersEditorChanged(object? sender, EventArgs args)
    {
        currentConfig = currentConfig with
        {
            AppSettings = currentConfig.AppSettings with
            {
                DisplayTriggers = DisplayTriggersEditor.Export().ToList()
            }
        };
        displayTriggerOrchestrator.ResetInputTracking();
    }

    private void OnDisplayTriggerTestRequested(object? sender, DisplayTriggerTestRequestedEventArgs args)
    {
        _ = TestDisplayTriggerAsync(args.Trigger);
    }

    private async Task TestDisplayTriggerAsync(DisplayTriggerConfig trigger)
    {
        if (activeConnection is null || activeConnection.State != DeviceConnectionState.Connected)
        {
            AddLog("No active device connection.");
            return;
        }

        var lines = DisplayTriggerRenderer.FitPreviewLines(
            DisplayTriggerRenderer.RenderLines(
                trigger,
                new DisplayTriggerRenderContext
                {
                    Input = trigger.Input,
                    Profile = ActiveProfileName,
                    Status = ConnectionStatus,
                    ConnectionStatus = ConnectionStatus,
                    MediaArtist = NowPlayingViewModel.LastSentTrack?.Artist ?? "",
                    MediaTitle = NowPlayingViewModel.LastSentTrack?.Title ?? ""
                }));

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
                DisplayTriggerNormalizer.NormalizeDurationMs(trigger.DurationMs));
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
        if (!protocolInputDecoder.TryDecode(message, out var inputEvent) || inputEvent is null)
        {
            return;
        }

        if (inputEvent is ButtonReleasedInputEvent button)
        {
            var showedActionResult = await HandleConfiguredInputAsync(button.Input);
            if (!showedActionResult)
            {
                await HandleDisplayTriggersAsync(button.Input);
            }
            return;
        }

        if (inputEvent is SliderChangedInputEvent slider)
        {
            LastSliderValue = slider.Value;
            var showedActionResult = await HandleConfiguredInputAsync(slider.Input, slider.Value.ToString());
            if (!showedActionResult)
            {
                await HandleDisplayTriggersAsync(slider.Input, slider.Value);
            }
        }
    }

    private async Task HandleDisplayTriggersAsync(string input, int? sliderValue = null)
    {
        try
        {
            var result = await displayTriggerOrchestrator.HandleInputAsync(
                currentConfig.AppSettings.DisplayTriggers,
                CreateDisplayTriggerRuntimeState(),
                input,
                sliderValue);
            LogDisplayTriggerSequence(input, result);
        }
        catch (Exception ex)
        {
            AddLog($"Display trigger failed: {ex.Message}");
        }
    }

    private async Task<bool> HandleConfiguredInputAsync(string input, string? value = null)
    {
        var result = await actionFeedbackCoordinator.HandleAsync(
            currentConfig,
            CreateDisplayTriggerRuntimeState(),
            input,
            value);

        if (!string.IsNullOrWhiteSpace(result.LogMessage))
        {
            AddLog(result.LogMessage);
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            AddLog(result.ErrorMessage);
        }

        return result.SuppressInputDisplayTriggers;
    }

    private bool HasMediaChangedDisplayTrigger()
    {
        return displayTriggerOrchestrator.HasMediaChangedTrigger(currentConfig.AppSettings.DisplayTriggers);
    }

    private async Task HandleMediaChangedDisplayTriggerAsync(
        NowPlayingTrack track,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await displayTriggerOrchestrator.HandleMediaChangedAsync(
                currentConfig.AppSettings.DisplayTriggers,
                CreateDisplayTriggerRuntimeState(),
                track,
                cancellationToken);
            LogDisplayTriggerSequence("mediaChanged", result);
        }
        catch (Exception ex)
        {
            AddLog($"Media changed display trigger failed: {ex.Message}");
        }
    }

    private async Task HandleConnectionChangedDisplayTriggersAsync(string connectionStatus)
    {
        try
        {
            var result = await displayTriggerOrchestrator.HandleConnectionChangedAsync(
                currentConfig.AppSettings.DisplayTriggers,
                CreateDisplayTriggerRuntimeState(),
                connectionStatus);
            LogDisplayTriggerSequence("connectionChanged", result);
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

        try
        {
            var result = await displayTriggerOrchestrator.HandleProfileChangedAsync(
                currentConfig.AppSettings.DisplayTriggers,
                CreateDisplayTriggerRuntimeState());
            LogDisplayTriggerSequence("profileChanged", result);
        }
        catch (Exception ex)
        {
            AddLog($"Profile changed display trigger failed for {profileName}: {ex.Message}");
        }
    }

    private DisplayTriggerRuntimeState CreateDisplayTriggerRuntimeState()
    {
        return new DisplayTriggerRuntimeState(
            ActiveProfileName,
            ConnectionStatus,
            NowPlayingViewModel.LastSentTrack);
    }

    private void LogDisplayTriggerSequence(string source, DisplayTriggerDispatchResult result)
    {
        if (result.SentScreenCount > 1)
        {
            AddLog($"Display trigger sequence sent: {source} ({result.SentScreenCount} screens)");
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

    private async Task DisconnectActiveConnectionAsync()
    {
        await deviceSessionService.DisconnectAsync();
    }

    private void AddLog(string message)
    {
        eventLogService.Add(message);
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
        var result = profileConfigService.FindActiveProfile(currentConfig);
        return result.IsSuccess ? result.Value : null;
    }

    private void UpdateActiveProfile(ProfileConfig updatedProfile)
    {
        var result = profileConfigService.UpdateActiveProfile(currentConfig, updatedProfile);
        if (!result.IsSuccess || result.Value is null)
        {
            AddLog(result.ErrorMessage);
            return;
        }

        currentConfig = result.Value;
    }

    private bool TryCreateEditorBinding(out ActionBinding binding)
    {
        binding = new ActionBinding();
        BindingEditorValidationMessage = "";
        var result = bindingEditorService.CreateBinding(BindingEditorInput, ActionEditor.Action, ActionEditor.Parameters);
        if (!result.IsSuccess || result.Binding is null)
        {
            SetBindingEditorError(result.Error);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(result.Warning))
        {
            AddLog(result.Warning);
        }

        binding = result.Binding;
        return true;
    }

    private void SetBindingEditorError(string message)
    {
        BindingEditorValidationMessage = message;
        AddLog(message);
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

    private void LoadDisplayTriggersEditor()
    {
        DisplayTriggersEditor.Load(currentConfig.AppSettings.DisplayTriggers);
        RefreshDisplayTriggerEditorRuntime();
        displayTriggerOrchestrator.ResetInputTracking();
    }

    private void RefreshDisplayTriggerEditorRuntime()
    {
        DisplayTriggersEditor.SetPreviewRuntime(
            ActiveProfileName,
            ConnectionStatus,
            LastSliderValue,
            NowPlayingViewModel.LastSentTrack);
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

}
