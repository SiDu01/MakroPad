using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.App.Services;
using MakroPad.Core.Actions;
using MakroPad.Core.Configuration;
using MakroPad.Core.Device;
using MakroPad.Core.Runtime;

namespace MakroPad.App.ViewModels;

public sealed record ActionOption(ActionDefinition? Definition)
{
    public string Name => Definition?.DisplayName ?? "Nichts";

    public string Category => Definition?.Category ?? "";

    public string Description => Definition?.Description ?? "Die Eingabe löst keine Aktion aus.";
}

/// <summary>Edits what one key or slider does in the active profile. Every change is saved right away.</summary>
public sealed partial class BindingInspectorViewModel : ObservableObject
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(400);

    private readonly ConfigSession session;
    private readonly MakroPadRuntime runtime;
    private readonly IFilePicker picker;
    private readonly IShortcutRecorder recorder;
    private readonly Func<IReadOnlyList<string>> listAudioSessions;
    private readonly DispatcherTimer saveTimer;
    private bool loading;

    [ObservableProperty]
    private ActionOption selectedAction;

    [ObservableProperty]
    private string status = "";

    public BindingInspectorViewModel(
        PadElementViewModel element,
        string input,
        InputKinds kind,
        ConfigSession session,
        MakroPadRuntime runtime,
        IFilePicker picker,
        IShortcutRecorder recorder,
        Func<IReadOnlyList<string>> listAudioSessions)
    {
        Element = element;
        this.recorder = recorder;
        Input = input;
        Kind = kind;
        this.session = session;
        this.runtime = runtime;
        this.picker = picker;
        this.listAudioSessions = listAudioSessions;
        saveTimer = new DispatcherTimer { Interval = SaveDelay };
        saveTimer.Tick += (_, _) =>
        {
            saveTimer.Stop();
            Save();
        };

        Actions = [new ActionOption(null), .. ActionCatalog.All.Where(a => a.Inputs.HasFlag(kind)).Select(a => new ActionOption(a))];

        loading = true;
        var binding = CurrentBinding;
        selectedAction = Actions.FirstOrDefault(a => string.Equals(a.Definition?.Id, binding?.Action, StringComparison.OrdinalIgnoreCase)) ?? Actions[0];
        BuildParameters(binding);
        loading = false;
        RefreshIssues();
    }

    public PadElementViewModel Element { get; }

    public string Input { get; }

    public InputKinds Kind { get; }

    public string Title => Element.Title;

    public string ProfileName => runtime.Profiles.Active.Name;

    public IReadOnlyList<ActionOption> Actions { get; }

    public ObservableCollection<ParameterViewModel> Parameters { get; } = [];

    public ObservableCollection<string> Issues { get; } = [];

    public bool HasIssues => Issues.Count > 0;

    public bool IsBound => SelectedAction.Definition is not null;

    public bool CanTest => IsBound && (Kind == InputKinds.Button || Element is FaderViewModel);

    private Binding? CurrentBinding => session.Config.FindProfile(ProfileName)?.Bindings
        .FirstOrDefault(b => string.Equals(b.Input, Input, StringComparison.OrdinalIgnoreCase));

    partial void OnSelectedActionChanged(ActionOption value)
    {
        if (loading)
        {
            return;
        }

        // Keep values the new action understands too, e.g. the display label.
        var previous = Parameters.ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);
        BuildParameters(null, previous);
        OnPropertyChanged(nameof(IsBound));
        OnPropertyChanged(nameof(CanTest));
        ScheduleSave();
    }

    [RelayCommand]
    private void Remove() => SelectedAction = Actions[0];

    [RelayCommand]
    private async Task TestAsync()
    {
        saveTimer.Stop();
        Save();
        if (CurrentBinding is not { } binding)
        {
            return;
        }

        InputEvent input = Element is FaderViewModel fader ? new SliderInput(fader.Id, fader.Value) : new ButtonInput(((KeyViewModel)Element).Id, true);
        await runtime.TriggerAsync(binding, input);
    }

    /// <summary>Writes pending edits immediately (e.g. before the selection changes).</summary>
    public void Flush()
    {
        recorder.Cancel();
        if (saveTimer.IsEnabled)
        {
            saveTimer.Stop();
            Save();
        }
    }

    private void BuildParameters(Binding? binding, IReadOnlyDictionary<string, string>? carryOver = null)
    {
        foreach (var parameter in Parameters)
        {
            parameter.Edited -= ScheduleSave;
        }

        Parameters.Clear();
        foreach (var definition in SelectedAction.Definition?.Parameters ?? [])
        {
            ParameterViewModel parameter = (SelectedAction.Definition!.Id, definition.Name) switch
            {
                ("keyboard.shortcut", "keys") => new ShortcutParameterViewModel(definition, recorder),
                ("app.volume", "process") => new ProcessParameterViewModel(definition, listAudioSessions),
                ("system.openApp", "path") => new PathParameterViewModel(definition, picker),
                ("profile.set", "name") => new ProfileParameterViewModel(definition, [.. session.Config.Profiles.Select(p => p.Name)]),
                _ => new TextParameterViewModel(definition)
            };
            parameter.Value = binding?.GetParameter(definition.Name)
                ?? (carryOver is not null && carryOver.TryGetValue(definition.Name, out var kept) ? kept : "");
            parameter.Edited += ScheduleSave;
            Parameters.Add(parameter);
        }
    }

    private void ScheduleSave()
    {
        if (loading)
        {
            return;
        }

        Status = "";
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void Save()
    {
        string? error;
        if (SelectedAction.Definition is not { } definition)
        {
            error = session.Update(config => ConfigEditor.RemoveBinding(config, ProfileName, Input));
        }
        else
        {
            var parameters = Parameters
                .Where(p => !string.IsNullOrWhiteSpace(p.Value))
                .ToDictionary(p => p.Name, p => p.Value.Trim(), StringComparer.OrdinalIgnoreCase);
            var binding = new Binding(Input, definition.Id, parameters.Count == 0 ? null : parameters);
            error = session.Update(config => ConfigEditor.SetBinding(config, ProfileName, binding));
        }

        Status = error ?? "Gespeichert";
        RefreshIssues();
    }

    private void RefreshIssues()
    {
        Issues.Clear();
        var suffix = $"/ {Input}";
        foreach (var issue in session.Service.Issues.Where(i =>
                     i.Location.StartsWith($"Profil \"{ProfileName}\"", StringComparison.OrdinalIgnoreCase)
                     && i.Location.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
        {
            Issues.Add(issue.Message);
        }

        OnPropertyChanged(nameof(HasIssues));
    }
}

/// <summary>Edits which OLED overlays appear; shown when the display is selected.</summary>
public sealed partial class DisplayInspectorViewModel : ObservableObject
{
    private readonly ConfigSession session;
    private bool loading = true;

    [ObservableProperty]
    private bool actions;

    [ObservableProperty]
    private bool volume;

    [ObservableProperty]
    private bool errors;

    [ObservableProperty]
    private bool profileChange;

    public DisplayInspectorViewModel(DisplayElementViewModel element, ConfigSession session)
    {
        Element = element;
        this.session = session;
        var feedback = session.Config.Settings.Feedback;
        actions = feedback.Actions;
        volume = feedback.Volume;
        errors = feedback.Errors;
        profileChange = feedback.ProfileChange;
        loading = false;
    }

    public DisplayElementViewModel Element { get; }

    partial void OnActionsChanged(bool value) => Save();

    partial void OnVolumeChanged(bool value) => Save();

    partial void OnErrorsChanged(bool value) => Save();

    partial void OnProfileChangeChanged(bool value) => Save();

    private void Save()
    {
        if (loading)
        {
            return;
        }

        session.Update(config => config with
        {
            Settings = config.Settings with
            {
                Feedback = new FeedbackSettings { Actions = Actions, Volume = Volume, Errors = Errors, ProfileChange = ProfileChange }
            }
        });
    }
}
