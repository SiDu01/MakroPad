using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MakroPad.App.ViewModels;

public enum AppSection
{
    Pad,
    Display,
    Connection,
    Settings,
    Diagnostics
}

/// <summary>
/// Root view model for the device-centric shell. Inherits the proven app runtime
/// orchestration from <see cref="MainWindowViewModel"/> and adds the workflow-ordered
/// navigation between the Pad, Display, Connection, Settings and Diagnostics sections.
/// </summary>
public partial class ShellViewModel
{
    public PadViewModel Pad { get; private set; } = null!;

    [ObservableProperty]
    private AppSection selectedSection = AppSection.Pad;

    partial void OnSelectedSectionChanged(AppSection value)
    {
        OnPropertyChanged(nameof(IsPadSelected));
        OnPropertyChanged(nameof(IsDisplaySelected));
        OnPropertyChanged(nameof(IsConnectionSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
        OnPropertyChanged(nameof(IsDiagnosticsSelected));
    }

    public bool IsPadSelected => SelectedSection == AppSection.Pad;

    public bool IsDisplaySelected => SelectedSection == AppSection.Display;

    public bool IsConnectionSelected => SelectedSection == AppSection.Connection;

    public bool IsSettingsSelected => SelectedSection == AppSection.Settings;

    public bool IsDiagnosticsSelected => SelectedSection == AppSection.Diagnostics;

    [RelayCommand]
    private void ShowPad() => SelectedSection = AppSection.Pad;

    [RelayCommand]
    private void ShowDisplay() => SelectedSection = AppSection.Display;

    [RelayCommand]
    private void ShowConnection() => SelectedSection = AppSection.Connection;

    [RelayCommand]
    private void ShowSettings() => SelectedSection = AppSection.Settings;

    [RelayCommand]
    private void ShowDiagnostics() => SelectedSection = AppSection.Diagnostics;
}
