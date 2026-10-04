using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.Core.Actions;
using MakroPad.Core.Device;
using MakroPad.Core.Runtime;
using MakroPad.Device;

namespace MakroPad.App;

/// <summary>M1 window: connection status, protocol log and (in simulator mode) virtual inputs.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private const int MaxLogLines = 300;

    private readonly MakroPadRuntime runtime;
    private readonly SimulatedDevice? simulator;

    [ObservableProperty]
    private string statusText = "Suche MakroPad …";

    [ObservableProperty]
    private int sliderValue;

    public MainViewModel(MakroPadRuntime runtime, SimulatedDevice? simulator)
    {
        this.runtime = runtime;
        this.simulator = simulator;

        runtime.StatusChanged += _ => Post(UpdateStatus);
        runtime.LineLogged += (direction, line) => Post(() => AddLog(direction == LineDirection.FromDevice ? "← " : "→ ", line));
        runtime.ActionCompleted += (binding, result) => Post(() => AddLog("⚙ ", Describe(binding, result)));
    }

    public bool IsSimulator => simulator is not null;

    public IReadOnlyList<int> SimulatorButtons { get; } = Enumerable.Range(1, SimulatedDevice.Buttons).ToArray();

    public ObservableCollection<string> Log { get; } = [];

    [RelayCommand]
    private void PressButton(int id) => simulator?.PressButton(id);

    [RelayCommand]
    private void SimulateUnplug() => simulator?.Disconnect();

    [RelayCommand]
    private void ClearLog() => Log.Clear();

    partial void OnSliderValueChanged(int value) => simulator?.MoveSlider(1, value);

    private void UpdateStatus()
    {
        StatusText = runtime.Status switch
        {
            ConnectionStatus.Connected => $"Verbunden mit {runtime.Endpoint} (Firmware {runtime.Device?.Firmware}, {runtime.Device?.Buttons} Tasten, {runtime.Device?.Sliders} Slider)",
            ConnectionStatus.Unresponsive => $"{runtime.Endpoint} antwortet nicht",
            ConnectionStatus.Incompatible => $"{runtime.Endpoint}: inkompatible Firmware (Protokoll {runtime.Device?.ProtocolVersion})",
            _ => "Suche MakroPad …"
        };
    }

    private void AddLog(string prefix, string text)
    {
        Log.Insert(0, $"{DateTime.Now:HH:mm:ss.fff}  {prefix}{text}");
        while (Log.Count > MaxLogLines)
        {
            Log.RemoveAt(Log.Count - 1);
        }
    }

    private static string Describe(Binding binding, ActionResult result)
        => $"{binding.Input} → {binding.Action}: {(result.Success ? "ok" : "Fehler")} – {result.Message}";

    private static void Post(Action action) => Dispatcher.UIThread.Post(action);
}
