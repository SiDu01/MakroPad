using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.App.Services;
using MakroPad.Core.Actions;
using MakroPad.Core.Configuration;
using MakroPad.Core.Device;
using MakroPad.Core.Display;
using MakroPad.Core.Runtime;

namespace MakroPad.App.ViewModels;

/// <summary>The device page: the pad drawn as it is built, profile tabs and the inspector.</summary>
public sealed partial class PadViewModel : ObservableObject
{
    private static readonly TimeSpan PressGlow = TimeSpan.FromMilliseconds(180);

    private readonly ConfigSession session;
    private readonly MakroPadRuntime runtime;
    private readonly IFilePicker picker;
    private readonly Func<IReadOnlyList<string>> listAudioSessions;

    [ObservableProperty]
    private PadElementViewModel? selected;

    [ObservableProperty]
    private object? inspector;

    [ObservableProperty]
    private string? profileError;

    [ObservableProperty]
    private bool isRenaming;

    [ObservableProperty]
    private string renameText = "";

    public PadViewModel(ConfigSession session, MakroPadRuntime runtime, OledMirror oled, IFilePicker picker, Func<IReadOnlyList<string>> listAudioSessions)
    {
        this.session = session;
        this.runtime = runtime;
        this.picker = picker;
        this.listAudioSessions = listAudioSessions;
        Oled = oled;

        // 5x4 grid, the OLED takes the top-right 2x2 block (docs/HARDWARE.md).
        var id = 1;
        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 5; column++)
            {
                if (row < 2 && column >= 3)
                {
                    continue;
                }

                Keys.Add(new KeyViewModel(id++, row, column));
            }
        }

        for (var slider = 1; slider <= 3; slider++)
        {
            Faders.Add(new FaderViewModel(slider));
        }

        runtime.InputReceived += input => Dispatcher.UIThread.Post(() => ShowInput(input));
        // Reloading the same profile (after every save) must not rebuild the inspector the user is typing in.
        var shownProfile = runtime.Profiles.Active.Name;
        runtime.Profiles.ActiveChanged += (profile, _) => Dispatcher.UIThread.Post(() =>
        {
            var switched = !string.Equals(profile.Name, shownProfile, StringComparison.OrdinalIgnoreCase);
            shownProfile = profile.Name;
            Refresh(rebuildInspector: switched);
        });
        session.Changed += external => Refresh(rebuildInspector: external);
        Refresh(rebuildInspector: false);
    }

    public ObservableCollection<KeyViewModel> Keys { get; } = [];

    public ObservableCollection<FaderViewModel> Faders { get; } = [];

    public DisplayElementViewModel Display { get; } = new();

    public OledMirror Oled { get; }

    public ObservableCollection<ProfileTabViewModel> Profiles { get; } = [];

    public string ActiveProfile => runtime.Profiles.Active.Name;

    public bool CanDeleteProfile => session.Config.Profiles.Count > 1;

    public bool HasSelection => Selected is not null;

    [RelayCommand]
    private void Select(PadElementViewModel element)
    {
        if (ReferenceEquals(Selected, element))
        {
            return;
        }

        (Inspector as BindingInspectorViewModel)?.Flush();
        if (Selected is not null)
        {
            Selected.IsSelected = false;
        }

        Selected = element;
        element.IsSelected = true;
        Inspector = CreateInspector(element);
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    private void ClearSelection()
    {
        (Inspector as BindingInspectorViewModel)?.Flush();
        if (Selected is not null)
        {
            Selected.IsSelected = false;
        }

        Selected = null;
        Inspector = null;
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    private void ActivateProfile(ProfileTabViewModel tab)
    {
        (Inspector as BindingInspectorViewModel)?.Flush();
        runtime.Profiles.Set(tab.Name);
    }

    [RelayCommand]
    private void AddProfile() => EditProfiles(config =>
    {
        var name = ConfigEditor.UniqueProfileName(config, "Neues Profil");
        return (ConfigEditor.AddProfile(config, name), name);
    });

    [RelayCommand]
    private void DuplicateProfile() => EditProfiles(config =>
    {
        var source = config.FindProfile(ActiveProfile);
        var name = ConfigEditor.UniqueProfileName(config, $"{ActiveProfile} Kopie");
        return (ConfigEditor.AddProfile(config, name, source), name);
    });

    [RelayCommand]
    private void DeleteProfile() => EditProfiles(config => (ConfigEditor.DeleteProfile(config, ActiveProfile), null));

    [RelayCommand]
    private void MoveProfileLeft() => EditProfiles(config => (ConfigEditor.MoveProfile(config, ActiveProfile, -1), ActiveProfile));

    [RelayCommand]
    private void MoveProfileRight() => EditProfiles(config => (ConfigEditor.MoveProfile(config, ActiveProfile, 1), ActiveProfile));

    [RelayCommand]
    private void StartRename()
    {
        RenameText = ActiveProfile;
        ProfileError = null;
        IsRenaming = true;
    }

    [RelayCommand]
    private void CommitRename()
    {
        var oldName = ActiveProfile;
        var newName = RenameText.Trim();
        IsRenaming = false;
        if (newName == oldName)
        {
            return;
        }

        EditProfiles(config => (ConfigEditor.RenameProfile(config, oldName, newName), newName));
    }

    [RelayCommand]
    private void CancelRename() => IsRenaming = false;

    private void EditProfiles(Func<MakroPadConfig, (MakroPadConfig Config, string? Activate)> edit)
    {
        (Inspector as BindingInspectorViewModel)?.Flush();
        string? activate = null;
        ProfileError = session.Update(config =>
        {
            var (next, name) = edit(config);
            activate = name;
            return next;
        });

        if (ProfileError is null && activate is not null)
        {
            runtime.Profiles.Set(activate);
        }
    }

    private object CreateInspector(PadElementViewModel element) => element switch
    {
        KeyViewModel key => new BindingInspectorViewModel(key, key.Input, InputKinds.Button, session, runtime, picker, listAudioSessions),
        FaderViewModel fader => new BindingInspectorViewModel(fader, fader.Input, InputKinds.Slider, session, runtime, picker, listAudioSessions),
        DisplayElementViewModel display => new DisplayInspectorViewModel(display, session),
        _ => throw new ArgumentOutOfRangeException(nameof(element))
    };

    private void Refresh(bool rebuildInspector)
    {
        var profile = session.Config.FindProfile(ActiveProfile) ?? runtime.Profiles.Active;
        foreach (var key in Keys)
        {
            key.Legend = BindingText.Legend(Find(profile, key.Input));
        }

        foreach (var fader in Faders)
        {
            fader.Legend = BindingText.Legend(Find(profile, fader.Input));
        }

        Profiles.Clear();
        foreach (var candidate in session.Config.Profiles)
        {
            Profiles.Add(new ProfileTabViewModel(candidate.Name)
            {
                IsActive = string.Equals(candidate.Name, ActiveProfile, StringComparison.OrdinalIgnoreCase)
            });
        }

        OnPropertyChanged(nameof(ActiveProfile));
        OnPropertyChanged(nameof(CanDeleteProfile));
        if (rebuildInspector && Selected is not null)
        {
            Inspector = CreateInspector(Selected);
        }
    }

    private static Binding? Find(ProfileConfig profile, string input)
        => profile.Bindings.FirstOrDefault(b => string.Equals(b.Input, input, StringComparison.OrdinalIgnoreCase));

    private void ShowInput(InputEvent input)
    {
        switch (input)
        {
            case ButtonInput button when Keys.FirstOrDefault(k => k.Id == button.Id) is { } key:
                key.IsPressed = button.Pressed;
                break;
            case SliderInput slider when Faders.FirstOrDefault(f => f.Id == slider.Id) is { } fader:
                fader.Value = slider.Value;
                fader.IsPressed = true;
                DispatcherTimer.RunOnce(() => fader.IsPressed = false, PressGlow);
                break;
        }
    }
}
