using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.App.Services;
using MakroPad.Core.Configuration;

namespace MakroPad.App.ViewModels;

public sealed record ThemeOption(string Value, string Name);

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigSession session;
    private bool loading;

    [ObservableProperty]
    private bool startInTray;

    [ObservableProperty]
    private bool startWithWindows;

    [ObservableProperty]
    private int sliderDeadbandPercent;

    [ObservableProperty]
    private bool invertSliders;

    [ObservableProperty]
    private ThemeOption theme;

    [ObservableProperty]
    private bool debugMode;

    [ObservableProperty]
    private int longPressMs;

    [ObservableProperty]
    private int doublePressMs;

    [ObservableProperty]
    private string? status;

    public SettingsViewModel(ConfigSession session, AutoProfileViewModel autoProfile)
    {
        this.session = session;
        AutoProfile = autoProfile;
        theme = Themes[0];
        Load();
        session.Changed += external =>
        {
            if (external)
            {
                Load();
            }
        };
    }

    public IReadOnlyList<ThemeOption> Themes { get; } =
    [
        new("system", "Wie Windows"),
        new("light", "Hell"),
        new("dark", "Dunkel")
    ];

    public AutoProfileViewModel AutoProfile { get; }

    public string ConfigPath => session.Service.ConfigPath;

    /// <summary>Raised when the theme setting changed so the app can switch immediately.</summary>
    public event Action<string>? ThemeChanged;

    partial void OnStartInTrayChanged(bool value) => Save();

    partial void OnStartWithWindowsChanged(bool value) => Save();

    partial void OnSliderDeadbandPercentChanged(int value) => Save();

    partial void OnInvertSlidersChanged(bool value) => Save();

    partial void OnDebugModeChanged(bool value) => Save();

    partial void OnLongPressMsChanged(int value) => Save();

    partial void OnDoublePressMsChanged(int value) => Save();

    partial void OnThemeChanged(ThemeOption value)
    {
        Save();
        if (!loading)
        {
            ThemeChanged?.Invoke(value.Value);
        }
    }

    [RelayCommand]
    private void OpenConfig()
    {
        // .json may have no associated program; Notepad is always there.
        if (!TryStart(new ProcessStartInfo(ConfigPath) { UseShellExecute = true }))
        {
            TryStart(new ProcessStartInfo("notepad.exe", $"\"{ConfigPath}\"") { UseShellExecute = true });
        }
    }

    [RelayCommand]
    private void OpenFolder() => TryStart(new ProcessStartInfo(session.Service.Directory) { UseShellExecute = true });

    private void Load()
    {
        loading = true;
        var settings = session.Config.Settings;
        StartInTray = settings.StartInTray;
        StartWithWindows = settings.StartWithWindows;
        SliderDeadbandPercent = settings.SliderDeadbandPercent;
        InvertSliders = settings.InvertSliders;
        Theme = Themes.FirstOrDefault(t => t.Value == settings.Theme) ?? Themes[0];
        DebugMode = settings.DebugMode;
        LongPressMs = settings.LongPressMs;
        DoublePressMs = settings.DoublePressMs;
        loading = false;
    }

    private void Save()
    {
        if (loading)
        {
            return;
        }

        Status = session.Update(config => config with
        {
            Settings = config.Settings with
            {
                StartInTray = StartInTray,
                StartWithWindows = StartWithWindows,
                SliderDeadbandPercent = Math.Clamp(SliderDeadbandPercent, 0, 25),
                InvertSliders = InvertSliders,
                Theme = Theme.Value,
                DebugMode = DebugMode,
                LongPressMs = Math.Clamp(LongPressMs, 200, 3000),
                DoublePressMs = Math.Clamp(DoublePressMs, 100, 1000)
            }
        });
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
            Status = ex.Message;
            return false;
        }
    }
}
