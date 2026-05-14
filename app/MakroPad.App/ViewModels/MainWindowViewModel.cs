using System.Collections.ObjectModel;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.Core.Configuration;
using MakroPad.Device;

namespace MakroPad.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string SimulatorPort = "Simulator";

    private readonly SimulatedMakroPadConnection simulatedConnection = new();
    private readonly SerialMakroPadConnection serialConnection = new();
    private readonly MakroPadConfigStore configStore;
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

            ConnectionStatus = "Connecting";
            await activeConnection.ConnectAsync(SelectedPort);
            ConnectionStatus = "Connected";
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

    private void HandleDeviceEvent(DeviceEvent deviceEvent)
    {
        if (deviceEvent.Message?.Type.Equals("slider", StringComparison.OrdinalIgnoreCase) == true &&
            int.TryParse(deviceEvent.Message.GetValue("value"), out var sliderValue))
        {
            LastSliderValue = sliderValue;
        }

        AddLog($"{deviceEvent.Timestamp:HH:mm:ss}  {deviceEvent.Direction}  {deviceEvent.Line}");
    }

    private async Task DisconnectActiveConnectionAsync()
    {
        if (activeConnection is null)
        {
            return;
        }

        activeConnection.EventReceived -= OnDeviceEventReceived;
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
