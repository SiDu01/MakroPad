using System.Collections.ObjectModel;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
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

public partial class ShellViewModel : ViewModelBase
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
    private readonly AppRuntimeLoopService runtimeLoopService = new();
    private readonly IdleScreenOrchestrator idleScreenOrchestrator = new(new ProcessPresenceProbe());
    private IMakroPadConnection? activeConnection => deviceSessionService.ActiveConnection;
    private MakroPadConfig currentConfig = MakroPadConfig.CreateDefault();
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
    private string configPath = "";

    [ObservableProperty]
    private bool autoConnect = true;

    [ObservableProperty]
    private bool isDarkTheme = true;

    public string ThemeToggleHint => IsDarkTheme
        ? "Zu hellem Design wechseln"
        : "Zu dunklem Design wechseln";

    public string ActiveProfileName => ProfilesEditor.ActiveProfileName;

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
        RefreshDisplaySettingsRuntime();
        RefreshDisplayTriggerEditorRuntime();
        _ = HandleConnectionStatusDisplayAsync(value);
    }

    public ObservableCollection<string> AvailablePorts { get; } = [];

    public ObservableCollection<string> EventLog => eventLogService.Entries;

    public AudioSessionsViewModel AudioSessionsViewModel { get; }

    public NowPlayingViewModel NowPlayingViewModel { get; }

    public string NowPlayingStatus => NowPlayingViewModel.NowPlayingStatus;

    public ObservableCollection<ApplicationAudioSession> AudioSessions => AudioSessionsViewModel.AudioSessions;

    public string AudioSessionsStatus => AudioSessionsViewModel.AudioSessionsStatus;

    public IAsyncRelayCommand RefreshAudioSessionsCommand => AudioSessionsViewModel.RefreshAudioSessionsCommand;

    public ProfilesEditorViewModel ProfilesEditor { get; }

    public DisplaySettingsViewModel DisplaySettings { get; }

    public DisplayTriggersEditorViewModel DisplayTriggersEditor { get; }

    public IReadOnlyList<int> AvailableButtonIds { get; } =
        Enumerable.Range(1, 20).ToArray();

    public IReadOnlyList<int> AvailableSliderIds { get; } =
        Enumerable.Range(1, 3).ToArray();

    /// <summary>
    /// Raised on the UI thread whenever an input (button or slider) fires, whether from real
    /// hardware or the simulator. Carries the input id, e.g. "button:7" or "slider:1".
    /// </summary>
    public event Action<string>? InputActivated;

    /// <summary>
    /// Simulates a single input on the simulator connection. Accepts "button:N" or "slider:N".
    /// Returns false when no simulator connection is active.
    /// </summary>
    public bool TrySimulateInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var parts = input.Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var id))
        {
            return false;
        }

        if (parts[0].Equals("button", StringComparison.OrdinalIgnoreCase))
        {
            return deviceSessionService.TrySimulateButtonPress(id);
        }

        if (parts[0].Equals("slider", StringComparison.OrdinalIgnoreCase))
        {
            LastSliderValue = LastSliderValue >= 4000 ? 0 : LastSliderValue + 512;
            return deviceSessionService.TrySimulateSliderChange(id, LastSliderValue);
        }

        return false;
    }

    public ShellViewModel()
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
        ProfilesEditor = new ProfilesEditorViewModel(
            profileConfigService,
            bindingEditorService,
            actionDefinitionCatalog,
            AudioSessionsViewModel.AvailableAudioProcessNames);
        ProfilesEditor.Changed += OnProfilesEditorChanged;
        DisplaySettings = new DisplaySettingsViewModel(deviceDisplaySyncService);
        DisplaySettings.Changed += OnDisplaySettingsChanged;
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
            ProfilesEditor.ActionEditor.NotifyAudioProcessNamesChanged();
        NowPlayingViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(NowPlayingViewModel.NowPlayingStatus))
            {
                OnPropertyChanged(nameof(NowPlayingStatus));
                RefreshDisplaySettingsRuntime();
            }
        };
        NowPlayingViewModel.TrackChanged += (_, _) =>
        {
            RefreshDisplaySettingsRuntime();
            RefreshDisplayTriggerEditorRuntime();
        };
        RefreshPorts();
        runtimeLoopService.Start(
            AudioSessionsViewModel,
            NowPlayingViewModel,
            () => activeConnection,
            DefaultScreenUsesMediaPlaceholder,
            HasMediaChangedDisplayTrigger,
            HandleMediaChangedDisplayTriggerAsync,
            SendDeviceConfigAsync,
            AddLog);
        Pad = new PadViewModel(ProfilesEditor, TrySimulateInput);
        InputActivated += Pad.OnInputActivated;
        _ = LoadConfigAsync();
        ApplyThemeVariant(IsDarkTheme);
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        ApplyThemeVariant(value);
        OnPropertyChanged(nameof(ThemeToggleHint));
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
    }

    private static void ApplyThemeVariant(bool isDark)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = isDark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
    }

    public void Shutdown()
    {
        runtimeLoopService.Dispose();
        displayTriggerOrchestrator.Dispose();
        idleScreenOrchestrator.Dispose();
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
            ProfilesEditor.Load(currentConfig);
            LoadPadLayout(currentConfig.AppSettings);
            idleScreenOrchestrator.Configure(
                currentConfig.AppSettings.IdleScreens,
                currentConfig.AppSettings.IdleRotationSeconds);
            idleScreenOrchestrator.Start(_ => SendDeviceConfigAsync());
            OnPropertyChanged(nameof(ActiveProfileName));
            AutoConnect = currentConfig.AppSettings.AutoConnect;
            DisplaySettings.Load(currentConfig);
            NowPlayingViewModel.SetEnabled(DisplaySettings.EnableNowPlayingDisplay);
            LoadDisplayTriggersEditor();

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
            currentConfig = ProfilesEditor.ExportConfig();
            var displayConfig = DisplaySettings.ApplyTo(currentConfig, SelectedPort);

            currentConfig = displayConfig with
            {
                ActiveProfile = ActiveProfileName,
                AppSettings = displayConfig.AppSettings with
                {
                    AutoConnect = AutoConnect,
                    DisplayTriggers = displayTriggers,
                    ButtonRows = PadButtonRows,
                    ButtonColumns = PadButtonColumns,
                    SliderCount = PadSliderCount
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

    private void OnProfilesEditorChanged(object? sender, ProfilesEditorChangedEventArgs args)
    {
        currentConfig = args.Config;
        OnPropertyChanged(nameof(ActiveProfileName));

        if (!string.IsNullOrWhiteSpace(args.Message))
        {
            AddLog(args.Message);
        }

        if (!string.IsNullOrWhiteSpace(args.SecondaryMessage))
        {
            AddLog(args.SecondaryMessage);
        }

        if (!args.ActiveProfileChanged)
        {
            return;
        }

        RefreshDisplaySettingsRuntime();
        RefreshDisplayTriggerEditorRuntime();
        if (!isLoadingConfig)
        {
            _ = HandleActiveProfileDisplayAsync(ActiveProfileName);
        }
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

    private void OnDisplaySettingsChanged(object? sender, EventArgs args)
    {
        NowPlayingViewModel.SetEnabled(DisplaySettings.EnableNowPlayingDisplay);
        if (isLoadingConfig)
        {
            return;
        }

        _ = SendDeviceConfigAsync();
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

        var lines = DisplayTriggerRenderer.RenderTemplateLines(
            trigger,
            new DisplayTriggerRenderContext
            {
                Input = trigger.Input,
                Profile = ActiveProfileName,
                Status = ConnectionStatus,
                ConnectionStatus = ConnectionStatus,
                MediaArtist = NowPlayingViewModel.LastSentTrack?.Artist ?? "",
                MediaTitle = NowPlayingViewModel.LastSentTrack?.Title ?? ""
            });

        if (lines.Count == 0)
        {
            AddLog("Display trigger produced no display lines.");
            return;
        }

        try
        {
            if (DisplayTriggerNormalizer.NormalizeTemplateMode(trigger.TemplateMode) == "layout")
            {
                await deviceDisplaySyncService.SendTemporaryLayoutAsync(
                    activeConnection,
                    DisplayTriggerRenderer.RenderLayoutScreen(
                        trigger,
                        new DisplayTriggerRenderContext
                        {
                            Input = trigger.Input,
                            Profile = ActiveProfileName,
                            Status = ConnectionStatus,
                            ConnectionStatus = ConnectionStatus,
                            MediaArtist = NowPlayingViewModel.LastSentTrack?.Artist ?? "",
                            MediaTitle = NowPlayingViewModel.LastSentTrack?.Title ?? ""
                        }),
                    DisplayTriggerNormalizer.NormalizeDurationMs(trigger.DurationMs));
            }
            else
            {
                await deviceDisplaySyncService.SendTemporaryTemplateAsync(
                    activeConnection,
                    lines,
                    DisplayTriggerNormalizer.NormalizeDurationMs(trigger.DurationMs));
            }

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
            InputActivated?.Invoke(button.Input);
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
            InputActivated?.Invoke(slider.Input);
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
            await deviceDisplaySyncService.SendConfigAsync(activeConnection, CreateIdleDeviceState());
            RefreshDisplaySettingsRuntime();
        }
        catch (Exception ex)
        {
            AddLog($"Device config send failed: {ex.Message}");
        }
    }

    private DeviceDisplayState CreateIdleDeviceState()
    {
        return IdleScreenDeviceState.Build(
            idleScreenOrchestrator.Current,
            ActiveProfileName,
            ConnectionStatus,
            DisplaySettings.ActionResultTimeoutMs,
            DisplaySettings.EnableNowPlayingDisplay,
            NowPlayingViewModel.LastSentTrack);
    }

    private async Task DisconnectActiveConnectionAsync()
    {
        await deviceSessionService.DisconnectAsync();
    }

    private void AddLog(string message)
    {
        eventLogService.Add(message);
    }

    private static string GetDefaultConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "MakroPad", "config.json");
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
        return (idleScreenOrchestrator.Current?.Template ?? "")
            .Contains("{media.", StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshDisplaySettingsRuntime()
    {
        DisplaySettings.SetRuntime(
            ActiveProfileName,
            ConnectionStatus,
            NowPlayingViewModel.LastSentTrack);
    }

}
