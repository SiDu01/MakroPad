using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.Core.Runtime;

namespace MakroPad.App.ViewModels;

public enum AppPage
{
    Pad,
    Diagnostics,
    Settings
}

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly MakroPadRuntime runtime;

    [ObservableProperty]
    private AppPage page = AppPage.Pad;

    public ShellViewModel(MakroPadRuntime runtime, PadViewModel pad, DiagnosticsViewModel diagnostics, SettingsViewModel settings)
    {
        this.runtime = runtime;
        Pad = pad;
        Diagnostics = diagnostics;
        Settings = settings;
        runtime.StatusChanged += _ => Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(ConnectionText));
            OnPropertyChanged(nameof(IsConnected));
        });
    }

    public PadViewModel Pad { get; }

    public DiagnosticsViewModel Diagnostics { get; }

    public SettingsViewModel Settings { get; }

    public object CurrentPage => Page switch
    {
        AppPage.Diagnostics => Diagnostics,
        AppPage.Settings => Settings,
        _ => Pad
    };

    public bool IsPad => Page == AppPage.Pad;

    public bool IsDiagnostics => Page == AppPage.Diagnostics;

    public bool IsSettings => Page == AppPage.Settings;

    public bool IsConnected => runtime.Status == ConnectionStatus.Connected;

    public string ConnectionText => runtime.Status switch
    {
        ConnectionStatus.Connected => $"Verbunden über {runtime.Endpoint}",
        ConnectionStatus.Unresponsive => $"{runtime.Endpoint} antwortet nicht",
        ConnectionStatus.Incompatible => "Firmware passt nicht zur App",
        _ => "Suche das MakroPad …"
    };

    [RelayCommand]
    private void Show(AppPage target) => Page = target;

    partial void OnPageChanged(AppPage value)
    {
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(IsPad));
        OnPropertyChanged(nameof(IsDiagnostics));
        OnPropertyChanged(nameof(IsSettings));
    }
}
