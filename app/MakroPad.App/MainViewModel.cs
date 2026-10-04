using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.Core.Actions;
using MakroPad.Core.Configuration;
using MakroPad.Core.Device;
using MakroPad.Core.Runtime;
using MakroPad.Device;

namespace MakroPad.App;

/// <summary>Status window until the pad editor arrives in M3: connection, profile, config problems, log.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private const int MaxLogLines = 300;

    private readonly MakroPadRuntime runtime;
    private readonly SimulatedDevice? simulator;
    private readonly ConfigService config;

    [ObservableProperty]
    private string statusText = "Suche MakroPad …";

    [ObservableProperty]
    private string profileText = "";

    [ObservableProperty]
    private int sliderValue;

    public MainViewModel(MakroPadRuntime runtime, SimulatedDevice? simulator, ConfigService config)
    {
        this.runtime = runtime;
        this.simulator = simulator;
        this.config = config;

        runtime.StatusChanged += _ => Post(UpdateStatus);
        runtime.LineLogged += (direction, line) => Post(() => AddLog(direction == LineDirection.FromDevice ? "← " : "→ ", line));
        runtime.ActionCompleted += (binding, result) => Post(() => AddLog("⚙ ", Describe(binding, result)));
        runtime.Profiles.ActiveChanged += (_, _) => Post(UpdateProfile);
        UpdateProfile();
    }

    public bool IsSimulator => simulator is not null;

    public string ConfigPath => config.ConfigPath;

    public IReadOnlyList<int> SimulatorButtons { get; } = Enumerable.Range(1, SimulatedDevice.Buttons).ToArray();

    public ObservableCollection<string> ConfigProblems { get; } = [];

    public bool HasConfigProblems => ConfigProblems.Count > 0;

    public ObservableCollection<string> Log { get; } = [];

    /// <summary>Shows load errors and validation issues of the current config, plus extra notices.</summary>
    public void ReportConfig(IEnumerable<string> notices)
    {
        foreach (var notice in notices)
        {
            AddLog("ℹ ", notice);
        }

        ConfigProblems.Clear();
        if (config.LoadError is { } error)
        {
            ConfigProblems.Add($"config.json wurde nicht geladen, es gelten die Standardwerte: {error}");
        }

        foreach (var issue in config.Issues)
        {
            ConfigProblems.Add(issue.ToString());
        }

        foreach (var problem in ConfigProblems)
        {
            AddLog("⚠ ", problem);
        }

        OnPropertyChanged(nameof(HasConfigProblems));
        UpdateProfile();
    }

    [RelayCommand]
    private void PressButton(int id) => simulator?.PressButton(id);

    [RelayCommand]
    private void SimulateUnplug() => simulator?.Disconnect();

    [RelayCommand]
    private void ClearLog() => Log.Clear();

    [RelayCommand]
    private void NextProfile() => runtime.Profiles.Next();

    [RelayCommand]
    private void OpenConfig()
    {
        // .json may have no associated program; Notepad is always there.
        if (!TryStart(new ProcessStartInfo(config.ConfigPath) { UseShellExecute = true }))
        {
            TryStart(new ProcessStartInfo("notepad.exe", $"\"{config.ConfigPath}\"") { UseShellExecute = true });
        }
    }

    [RelayCommand]
    private void OpenConfigFolder() => TryStart(new ProcessStartInfo(config.Directory) { UseShellExecute = true });

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

    private void UpdateProfile()
    {
        var all = runtime.Profiles.Profiles;
        ProfileText = $"Profil: {runtime.Profiles.Active.Name} ({runtime.Profiles.Active.Bindings.Count} Belegungen, {all.Count} Profil{(all.Count == 1 ? "" : "e")})";
    }

    private void AddLog(string prefix, string text)
    {
        Log.Insert(0, $"{DateTime.Now:HH:mm:ss.fff}  {prefix}{text}");
        while (Log.Count > MaxLogLines)
        {
            Log.RemoveAt(Log.Count - 1);
        }
    }

    private bool TryStart(ProcessStartInfo startInfo)
    {
        try
        {
            using var _ = Process.Start(startInfo);
            return true;
        }
        catch (Win32Exception ex)
        {
            AddLog("⚠ ", ex.Message);
            return false;
        }
    }

    private static string Describe(Binding binding, ActionResult result)
        => $"{binding.Input} → {binding.Action}: {(result.Success ? "ok" : "Fehler")} – {result.Message}";

    private static void Post(Action action) => Dispatcher.UIThread.Post(action);
}
