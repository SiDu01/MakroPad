using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.App.Services;
using MakroPad.Core.Configuration;
using MakroPad.Core.Runtime;

namespace MakroPad.App.ViewModels;

public sealed record ProfileRuleRow(string Process, string Profile);

public sealed record FallbackOption(string? Profile)
{
    public string Name => Profile ?? "Profil beibehalten";
}

/// <summary>Rules "program in the foreground → profile", edited on the settings page.</summary>
public sealed partial class AutoProfileViewModel : ObservableObject
{
    private readonly ConfigSession session;
    private readonly IForegroundSource foreground;
    private bool loading;

    [ObservableProperty]
    private bool enabled;

    [ObservableProperty]
    private FallbackOption? fallback;

    [ObservableProperty]
    private string newProcess = "";

    [ObservableProperty]
    private string? newProfile;

    [ObservableProperty]
    private string? error;

    public AutoProfileViewModel(ConfigSession session, IForegroundSource foreground)
    {
        this.session = session;
        this.foreground = foreground;
        Load();
        session.Changed += _ => Load();
        foreground.Changed += _ => Dispatcher.UIThread.Post(RefreshRecent);
        RefreshRecent();
    }

    public ObservableCollection<ProfileRuleRow> Rules { get; } = [];

    public ObservableCollection<string> ProfileNames { get; } = [];

    public ObservableCollection<FallbackOption> FallbackOptions { get; } = [];

    /// <summary>Programs recently in the foreground, without the ones that already have a rule.</summary>
    public ObservableCollection<string> Recent { get; } = [];

    public bool HasRules => Rules.Count > 0;

    public bool HasRecent => Recent.Count > 0;

    partial void OnEnabledChanged(bool value) => Save(config => config with { AutoProfile = config.AutoProfile with { Enabled = value } });

    partial void OnFallbackChanged(FallbackOption? value) =>
        Save(config => config with { AutoProfile = config.AutoProfile with { FallbackProfile = value?.Profile } });

    [RelayCommand]
    private void AddRule()
    {
        if (NewProfile is null)
        {
            Error = "Wähle das Profil, das aktiv werden soll.";
            return;
        }

        var process = NewProcess;
        if (Save(config => ConfigEditor.SetProfileRule(config, new ProfileRule(process, NewProfile))))
        {
            NewProcess = "";
        }
    }

    [RelayCommand]
    private void RemoveRule(ProfileRuleRow row) => Save(config => ConfigEditor.RemoveProfileRule(config, row.Process));

    [RelayCommand]
    private void UseProcess(string process) => NewProcess = process;

    private bool Save(Func<MakroPadConfig, MakroPadConfig> edit)
    {
        if (loading)
        {
            return false;
        }

        Error = session.Update(edit);
        return Error is null;
    }

    private void Load()
    {
        loading = true;
        var settings = session.Config.AutoProfile;
        Enabled = settings.Enabled;

        Rules.Clear();
        foreach (var rule in settings.Rules)
        {
            Rules.Add(new ProfileRuleRow(rule.Process, rule.Profile));
        }

        var keepNew = NewProfile;
        ProfileNames.Clear();
        FallbackOptions.Clear();
        FallbackOptions.Add(new FallbackOption(null));
        foreach (var profile in session.Config.Profiles)
        {
            ProfileNames.Add(profile.Name);
            FallbackOptions.Add(new FallbackOption(profile.Name));
        }

        NewProfile = keepNew is not null && ProfileNames.Contains(keepNew) ? keepNew : ProfileNames.FirstOrDefault();
        Fallback = FallbackOptions.FirstOrDefault(o => string.Equals(o.Profile, settings.FallbackProfile, StringComparison.OrdinalIgnoreCase)) ?? FallbackOptions[0];
        loading = false;

        OnPropertyChanged(nameof(HasRules));
        RefreshRecent();
    }

    private void RefreshRecent()
    {
        var own = Environment.ProcessPath is { } path ? Path.GetFileNameWithoutExtension(path) : "";
        Recent.Clear();
        foreach (var process in foreground.Recent.Where(p =>
                     !string.Equals(p, own, StringComparison.OrdinalIgnoreCase)
                     && !Rules.Any(r => string.Equals(r.Process, p, StringComparison.OrdinalIgnoreCase))))
        {
            Recent.Add(process);
        }

        OnPropertyChanged(nameof(HasRecent));
    }
}
