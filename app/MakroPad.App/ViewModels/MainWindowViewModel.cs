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
    private readonly MakroPadConfigStore configStore;
    private readonly ActionResolver actionResolver = new();
    private readonly ActionExecutor actionExecutor = new();
    private IMakroPadConnection? activeConnection;
    private MakroPadConfig currentConfig = MakroPadConfig.CreateDefault();

    [ObservableProperty]
    private string connectionStatus = "Disconnected";

    [ObservableProperty]
    private string? selectedPort;

    [ObservableProperty]
    private string displayText = "Hello MakroPad";

    [ObservableProperty]
    private int lastSliderValue;

    [ObservableProperty]
    private string activeProfileName = "Default";

    [ObservableProperty]
    private string configPath = "";

    partial void OnActiveProfileNameChanged(string value)
    {
        currentConfig = currentConfig with { ActiveProfile = value };
    }

    public ObservableCollection<string> AvailablePorts { get; } = [];

    public ObservableCollection<string> EventLog { get; } = [];

    public ObservableCollection<string> ProfileNames { get; } = [];

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

            ProfileNames.Clear();
            foreach (var profile in currentConfig.Profiles)
            {
                ProfileNames.Add(profile.Name);
            }

            AddLog($"Config loaded: {ConfigPath}");
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
            currentConfig = currentConfig with { ActiveProfile = ActiveProfileName };
            await configStore.SaveAsync(currentConfig);
            AddLog($"Config saved: {ConfigPath}");
        }
        catch (Exception ex)
        {
            AddLog($"Config save failed: {ex.Message}");
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

            await ExecuteConfiguredActionAsync($"button:{id}");
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
                await ExecuteConfiguredActionAsync($"slider:{id}", value);
            }
        }
    }

    private async Task ExecuteConfiguredActionAsync(string input, string? value = null)
    {
        var binding = actionResolver.Resolve(currentConfig, input);
        if (binding is null)
        {
            AddLog($"No action binding for {input} in profile {currentConfig.ActiveProfile}.");
            return;
        }

        var result = await actionExecutor.ExecuteAsync(new ActionExecutionContext(input, binding, value));
        AddLog(result.Succeeded
            ? $"Action matched: {input} -> {binding.Action} ({result.Message})"
            : $"Action failed: {input} -> {binding.Action} ({result.Message})");

        if (activeConnection is not null)
        {
            var status = result.Succeeded ? "ok" : "failed";
            var line = MakroPad.Core.Protocol.ProtocolCodec.Format(
                "action-result",
                new Dictionary<string, string?>
                {
                    ["input"] = input,
                    ["status"] = status,
                    ["message"] = result.Message
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

    private static string GetDefaultConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "MakroPad", "config.json");
    }
}
