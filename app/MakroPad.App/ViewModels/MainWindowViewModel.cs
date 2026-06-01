using System.Collections.ObjectModel;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.Actions;
using MakroPad.Core.Configuration;
using MakroPad.Device;

namespace MakroPad.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string SimulatorPort = "Simulator";

    private readonly SimulatedMakroPadConnection simulatedConnection = new();
    private readonly SerialMakroPadConnection serialConnection = new();
    private readonly MakroPadDeviceProbe deviceProbe = new();
    private readonly MakroPadConfigStore configStore;
    private readonly ActionPipeline actionPipeline = new(new ActionResolver(), new ActionExecutor());
    private IMakroPadConnection? activeConnection;
    private MakroPadConfig currentConfig = MakroPadConfig.CreateDefault();
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
    private string activeProfileName = "Default";

    [ObservableProperty]
    private string configPath = "";

    [ObservableProperty]
    private bool autoConnect = true;

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

    [ObservableProperty]
    private string bindingEditorParameters = "";

    partial void OnActiveProfileNameChanged(string value)
    {
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
    [
        "media.playPause",
        "media.nextTrack",
        "media.previousTrack",
        "media.stop",
        "media.volumeUp",
        "media.volumeDown",
        "media.mute",
        "system.volume",
        "system.openUrl",
        "system.openApp",
        "keyboard.shortcut",
        "log"
    ];

    public MainWindowViewModel()
    {
        ConfigPath = GetDefaultConfigPath();
        configStore = new MakroPadConfigStore(ConfigPath);
        RefreshPorts();
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
            ActionResultTimeoutMs = currentConfig.Hardware.Display.ActionResultTimeoutMs;

            ProfileNames.Clear();
            foreach (var profile in currentConfig.Profiles)
            {
                ProfileNames.Add(profile.Name);
            }

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
            simulatedConnection.SimulateButtonPress(1);
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
            simulatedConnection.SimulateSliderChange(1, LastSliderValue);
            return;
        }

        AddLog("Slider simulation is only available on the simulator connection.");
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
                ["actionResultTimeoutMs"] = Math.Max(0, ActionResultTimeoutMs).ToString()
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

        if (string.IsNullOrWhiteSpace(input))
        {
            AddLog("Binding input is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            AddLog("Binding action is required.");
            return false;
        }

        binding = new ActionBinding
        {
            Input = input,
            Action = action,
            Parameters = ParseParameters(BindingEditorParameters, action)
        };

        return true;
    }

    private static Dictionary<string, string> ParseParameters(string parameters, string action)
    {
        var result = new Dictionary<string, string>();

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
}
