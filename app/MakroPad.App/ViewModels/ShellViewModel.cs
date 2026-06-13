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
/// Root view model for the device-centric shell. The app runtime orchestration lives in the
/// <c>ShellViewModel.Runtime.cs</c> partial; this partial adds the workflow-ordered navigation
/// between the Pad, Display, Connection, Settings and Diagnostics sections and the configurable
/// pad layout (button rows/columns and slider count).
/// </summary>
public partial class ShellViewModel
{
    public PadViewModel Pad { get; private set; } = null!;

    [ObservableProperty]
    private AppSection selectedSection = AppSection.Pad;

    [ObservableProperty]
    private int padButtonRows = 4;

    [ObservableProperty]
    private int padButtonColumns = 5;

    [ObservableProperty]
    private int padSliderCount = 3;

    public int PadButtonTotal => PadButtonRows * PadButtonColumns;

    partial void OnPadButtonRowsChanged(int value)
    {
        OnPropertyChanged(nameof(PadButtonTotal));
        ApplyPadLayout();
    }

    partial void OnPadButtonColumnsChanged(int value)
    {
        OnPropertyChanged(nameof(PadButtonTotal));
        ApplyPadLayout();
    }

    partial void OnPadSliderCountChanged(int value)
    {
        ApplyPadLayout();
    }

    private void ApplyPadLayout()
    {
        if (isLoadingConfig)
        {
            return;
        }

        Pad.ApplyLayout(PadButtonRows, PadButtonColumns, PadSliderCount);
    }

    /// <summary>Loads the saved pad layout into the editor and rebuilds the grid.</summary>
    private void LoadPadLayout(MakroPad.Core.Configuration.AppSettingsConfig settings)
    {
        PadButtonRows = settings.ButtonRows;
        PadButtonColumns = settings.ButtonColumns;
        PadSliderCount = settings.SliderCount;
        Pad.ApplyLayout(PadButtonRows, PadButtonColumns, PadSliderCount);
    }

    partial void OnSelectedSectionChanged(AppSection value)
    {
        OnPropertyChanged(nameof(IsPadSelected));
        OnPropertyChanged(nameof(IsDisplaySelected));
        OnPropertyChanged(nameof(IsConnectionSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
        OnPropertyChanged(nameof(IsDiagnosticsSelected));
        IsDisplayEditorOpen = false;
    }

    /// <summary>True while the dedicated single-rule editor page is shown inside Display.</summary>
    [ObservableProperty]
    private bool isDisplayEditorOpen;

    [RelayCommand]
    private void CreateDisplayRule()
    {
        // Reset the editor to defaults, then add a fresh rule and open the editor page for it.
        DisplayTriggersEditor.SelectedDisplayTrigger = null;
        DisplayTriggersEditor.AddDisplayTriggerCommand.Execute(null);
        IsDisplayEditorOpen = true;
    }

    [RelayCommand]
    private void EditDisplayRule()
    {
        if (DisplayTriggersEditor.SelectedDisplayTrigger is not null)
        {
            IsDisplayEditorOpen = true;
        }
    }

    [RelayCommand]
    private void CloseDisplayEditor() => IsDisplayEditorOpen = false;

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
