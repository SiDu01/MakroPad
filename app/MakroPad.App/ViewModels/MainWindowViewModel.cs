using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.Device;

namespace MakroPad.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SimulatedMakroPadConnection deviceConnection = new();

    [ObservableProperty]
    private string connectionStatus = "Disconnected";

    [ObservableProperty]
    private string? selectedPort;

    [ObservableProperty]
    private string displayText = "Hello MakroPad";

    [ObservableProperty]
    private int lastSliderValue;

    public ObservableCollection<string> AvailablePorts { get; } = [];

    public ObservableCollection<string> EventLog { get; } = [];

    public MainWindowViewModel()
    {
        deviceConnection.EventReceived += OnDeviceEventReceived;
        RefreshPorts();
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts.Clear();

        foreach (var port in deviceConnection.GetAvailablePorts())
        {
            AvailablePorts.Add(port);
        }

        SelectedPort ??= AvailablePorts.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (SelectedPort is null)
        {
            AddLog("No device port selected.");
            return;
        }

        ConnectionStatus = "Connecting";
        await deviceConnection.ConnectAsync(SelectedPort);
        ConnectionStatus = "Connected";
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await deviceConnection.DisconnectAsync();
        ConnectionStatus = "Disconnected";
    }

    [RelayCommand]
    private async Task SendDisplayMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(DisplayText))
        {
            return;
        }

        await deviceConnection.SendDisplayMessageAsync(DisplayText);
    }

    [RelayCommand]
    private void SimulateButton()
    {
        deviceConnection.SimulateButtonPress(1);
    }

    [RelayCommand]
    private void SimulateSlider()
    {
        LastSliderValue = LastSliderValue >= 4000 ? 0 : LastSliderValue + 512;
        deviceConnection.SimulateSliderChange(1, LastSliderValue);
    }

    private void OnDeviceEventReceived(object? sender, DeviceEvent deviceEvent)
    {
        if (deviceEvent.Message?.Type.Equals("slider", StringComparison.OrdinalIgnoreCase) == true &&
            int.TryParse(deviceEvent.Message.GetValue("value"), out var sliderValue))
        {
            LastSliderValue = sliderValue;
        }

        AddLog($"{deviceEvent.Timestamp:HH:mm:ss}  {deviceEvent.Direction}  {deviceEvent.Line}");
    }

    private void AddLog(string message)
    {
        EventLog.Insert(0, message);

        while (EventLog.Count > 200)
        {
            EventLog.RemoveAt(EventLog.Count - 1);
        }
    }
}
