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

/// <summary>An inspector that may hold unsaved edits.</summary>
public interface IFlushable
{
    void Flush();
}

/// <summary>Builds the right editor for each action parameter; shared by bindings and macro steps.</summary>
public sealed class ParameterFactory(
    IFilePicker picker,
    IShortcutRecorder recorder,
    Func<IReadOnlyList<string>> listAudioSessions,
    Func<IReadOnlyList<string>> listProfiles)
{
    public IShortcutRecorder Recorder => recorder;

    public ParameterViewModel Create(string actionId, ParameterDefinition definition) => (actionId, definition.Name) switch
    {
        ("keyboard.shortcut", "keys") => new ShortcutParameterViewModel(definition, recorder),
        ("app.volume", "process") => new ProcessParameterViewModel(definition, listAudioSessions),
        ("system.openApp", "path") => new PathParameterViewModel(definition, picker),
        ("profile.set", "name") => new ProfileParameterViewModel(definition, listProfiles()),
        ("keyboard.text", "text") or ("http.request", "body") => new TextParameterViewModel(definition, multiline: true),
        _ => new TextParameterViewModel(definition)
    };

    /// <summary>Editors for all parameters of an action, filled from <paramref name="values"/>.</summary>
    public List<ParameterViewModel> CreateAll(ActionDefinition? action, Func<string, string?> values, bool includeLabel = true)
    {
        var result = new List<ParameterViewModel>();
        if (action is null)
        {
            return result;
        }

        foreach (var definition in action.Parameters)
        {
            if (!includeLabel && definition.Name == "label")
            {
                continue;
            }

            var parameter = Create(action.Id, definition);
            parameter.Value = values(definition.Name) ?? "";
            result.Add(parameter);
        }

        return result;
    }

    public static Dictionary<string, string>? Collect(IEnumerable<ParameterViewModel> parameters)
    {
        // Multi-line text is typed or sent as written, so its spaces and line breaks count.
        static string Clean(ParameterViewModel p) => p is TextParameterViewModel { IsMultiline: true } ? p.Value : p.Value.Trim();
        var values = parameters
            .Where(p => Clean(p).Length > 0)
            .ToDictionary(p => p.Name, Clean, StringComparer.OrdinalIgnoreCase);
        return values.Count == 0 ? null : values;
    }
}

/// <summary>One step of a macro in the editor.</summary>
public sealed partial class MacroStepViewModel : ObservableObject
{
    private readonly ParameterFactory factory;
    private bool loading;

    /// <summary>A hand-written step whose action the editor does not know; saved unchanged until the user edits it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnknownHint))]
    private MacroStep? unknown;

    [ObservableProperty]
    private ActionOption selectedAction;

    [ObservableProperty]
    private int number;

    public MacroStepViewModel(MacroStep step, ParameterFactory factory)
    {
        this.factory = factory;
        loading = true;
        var known = Options.FirstOrDefault(o => string.Equals(o.Definition?.Id, step.Action, StringComparison.OrdinalIgnoreCase));
        Unknown = known is null ? step : null;
        selectedAction = known ?? Options[0];
        Build(step.GetParameter);
        loading = false;
    }

    public static IReadOnlyList<ActionOption> Options { get; } = [.. ActionCatalog.MacroStepActions.Select(a => new ActionOption(a))];

    public ObservableCollection<ParameterViewModel> Parameters { get; } = [];

    public bool IsDelay => SelectedAction.Definition == ActionCatalog.Delay;

    public string? UnknownHint => Unknown is { } step
        ? $"Unbekannte Aktion „{step.Action}“ aus config.json, wird unverändert gespeichert. Wähle eine Aktion, um den Schritt zu ersetzen."
        : null;

    /// <summary>Raised when anything in the step changed.</summary>
    public event Action? Edited;

    // Options are built from ActionCatalog.MacroStepActions, so every step option has a definition.
    public MacroStep ToStep() => Unknown ?? new(SelectedAction.Definition!.Id, ParameterFactory.Collect(Parameters));

    partial void OnSelectedActionChanged(ActionOption value)
    {
        if (loading)
        {
            return;
        }

        Unknown = null;
        var previous = Parameters.ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);
        Build(name => previous.GetValueOrDefault(name));
        OnPropertyChanged(nameof(IsDelay));
        Edited?.Invoke();
    }

    private void Build(Func<string, string?> values)
    {
        foreach (var parameter in Parameters)
        {
            parameter.Edited -= OnParameterEdited;
        }

        Parameters.Clear();
        // Labels only show up on keys and overlays, which a single step never does.
        foreach (var parameter in factory.CreateAll(SelectedAction.Definition, values, includeLabel: false))
        {
            parameter.Edited += OnParameterEdited;
            Parameters.Add(parameter);
        }
    }

    private void OnParameterEdited()
    {
        Unknown = null;
        Edited?.Invoke();
    }
}

/// <summary>Edits what one input does in the active profile. Every change is saved right away unless <see cref="Deferred"/>.</summary>
public sealed partial class BindingInspectorViewModel : ObservableObject, IFlushable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(400);

    private readonly ConfigSession session;
    private readonly MakroPadRuntime runtime;
    private readonly ParameterFactory factory;
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
        ParameterFactory factory)
    {
        Element = element;
        Input = input;
        Kind = kind;
        this.session = session;
        this.runtime = runtime;
        this.factory = factory;
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
        BuildParameters(binding is null ? _ => null : binding.GetParameter);
        foreach (var step in binding?.Steps ?? [])
        {
            AddStepViewModel(step);
        }

        loading = false;
        RefreshIssues();
    }

    public PadElementViewModel Element { get; }

    public string Input { get; }

    public InputKinds Kind { get; }

    public string Title => Element.Title;

    /// <summary>False when a surrounding inspector (keys with gesture tabs) shows the title itself.</summary>
    public bool ShowTitle { get; init; } = true;

    /// <summary>On/off switch for a slider; keys have theirs above the gesture tabs.</summary>
    public InputSwitchViewModel? Switch { get; init; }

    /// <summary>Keeps edits as a draft; the owner writes them with <see cref="Apply"/> (quick editor window).</summary>
    public bool Deferred { get; init; }

    public string ProfileName => runtime.Profiles.Active.Name;

    public IReadOnlyList<ActionOption> Actions { get; }

    public ObservableCollection<ParameterViewModel> Parameters { get; } = [];

    public ObservableCollection<MacroStepViewModel> Steps { get; } = [];

    public ObservableCollection<string> Issues { get; } = [];

    public bool HasIssues => Issues.Count > 0;

    public bool IsBound => SelectedAction.Definition is not null;

    /// <summary>Macro and toggle both edit a step list.</summary>
    public bool IsMacro => SelectedAction.Definition is { } definition && ActionCatalog.HasSteps(definition.Id);

    /// <summary>A toggle runs one step per press, so a pause step makes no sense there.</summary>
    public bool CanAddDelay => SelectedAction.Definition?.Id != "toggle";

    public bool CanAddStep => Steps.Count < ActionCatalog.MaxMacroSteps;

    public bool CanTest => IsBound;

    private Binding? CurrentBinding => session.Config.FindProfile(ProfileName)?.Bindings
        .FirstOrDefault(b => string.Equals(b.Input, Input, StringComparison.OrdinalIgnoreCase));

    /// <summary>The binding as currently edited; null for "Nichts".</summary>
    private Binding? Draft => SelectedAction.Definition is { } definition
        ? new Binding(Input, definition.Id, ParameterFactory.Collect(Parameters), IsMacro ? Steps.Select(s => s.ToStep()).ToList() : null)
        : null;

    /// <summary>Applies this inspector's edit to <paramref name="config"/>.</summary>
    public MakroPadConfig Apply(MakroPadConfig config) => Draft is { } binding
        ? ConfigEditor.SetBinding(config, ProfileName, binding)
        : ConfigEditor.RemoveBinding(config, ProfileName, Input);

    partial void OnSelectedActionChanged(ActionOption value)
    {
        if (loading)
        {
            return;
        }

        // Keep values the new action understands too, e.g. the display label.
        var previous = Parameters.ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);
        BuildParameters(name => previous.GetValueOrDefault(name));
        // A toggle needs two sides to switch between.
        var minimumSteps = CanAddDelay ? 1 : 2;
        while (IsMacro && Steps.Count < minimumSteps)
        {
            AddStepViewModel(new MacroStep("keyboard.shortcut"));
        }

        OnPropertyChanged(nameof(IsBound));
        OnPropertyChanged(nameof(IsMacro));
        OnPropertyChanged(nameof(CanAddDelay));
        OnPropertyChanged(nameof(CanTest));
        ScheduleSave();
    }

    [RelayCommand]
    private void Remove() => SelectedAction = Actions[0];

    [RelayCommand]
    private void AddStep()
    {
        AddStepViewModel(new MacroStep("keyboard.shortcut"));
        ScheduleSave();
    }

    [RelayCommand]
    private void AddDelay()
    {
        AddStepViewModel(new MacroStep("delay", new Dictionary<string, string> { ["ms"] = "200" }));
        ScheduleSave();
    }

    [RelayCommand]
    private void RemoveStep(MacroStepViewModel step)
    {
        step.Edited -= ScheduleSave;
        Steps.Remove(step);
        Renumber();
        ScheduleSave();
    }

    [RelayCommand]
    private void MoveStepUp(MacroStepViewModel step) => MoveStep(step, -1);

    [RelayCommand]
    private void MoveStepDown(MacroStepViewModel step) => MoveStep(step, 1);

    [RelayCommand]
    private async Task TestAsync()
    {
        if (!Deferred)
        {
            saveTimer.Stop();
            Save();
        }

        // A draft is tried out without saving it.
        if ((Deferred ? Draft : CurrentBinding) is not { } binding)
        {
            return;
        }

        InputEvent input = Element is FaderViewModel fader ? new SliderInput(fader.Id, fader.Value) : new ButtonInput(((KeyViewModel)Element).Id, true);
        await runtime.TriggerAsync(binding, input);
    }

    /// <summary>Writes pending edits immediately (e.g. before the selection changes).</summary>
    public void Flush()
    {
        factory.Recorder.Cancel();
        if (saveTimer.IsEnabled)
        {
            saveTimer.Stop();
            Save();
        }
    }

    private void MoveStep(MacroStepViewModel step, int offset)
    {
        var index = Steps.IndexOf(step);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= Steps.Count)
        {
            return;
        }

        Steps.Move(index, target);
        Renumber();
        ScheduleSave();
    }

    private void AddStepViewModel(MacroStep step)
    {
        var viewModel = new MacroStepViewModel(step, factory);
        viewModel.Edited += ScheduleSave;
        Steps.Add(viewModel);
        Renumber();
    }

    private void Renumber()
    {
        for (var index = 0; index < Steps.Count; index++)
        {
            Steps[index].Number = index + 1;
        }

        OnPropertyChanged(nameof(CanAddStep));
    }

    private void BuildParameters(Func<string, string?> values)
    {
        foreach (var parameter in Parameters)
        {
            parameter.Edited -= ScheduleSave;
        }

        Parameters.Clear();
        foreach (var parameter in factory.CreateAll(SelectedAction.Definition, values))
        {
            parameter.Edited += ScheduleSave;
            Parameters.Add(parameter);
        }
    }

    private void ScheduleSave()
    {
        if (loading || Deferred)
        {
            return;
        }

        Status = "";
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void Save()
    {
        var error = session.Update(Apply);
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

public sealed partial class GestureTabViewModel(Gesture gesture, string name) : ObservableObject
{
    public Gesture Gesture { get; } = gesture;

    public string Name { get; } = name;

    [ObservableProperty]
    private bool isActive;

    /// <summary>The gesture has an action in the active profile.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleName))]
    private bool isBound;

    /// <summary>Screen readers cannot see the dot that marks a bound gesture.</summary>
    public string AccessibleName => IsBound ? $"{Name}, belegt" : Name;
}

/// <summary>A key has three gestures (press, long, double); each tab edits one of them.</summary>
public sealed partial class KeyInspectorViewModel : ObservableObject, IFlushable
{
    private readonly KeyViewModel key;
    private readonly Func<string, BindingInspectorViewModel> createInspector;
    private readonly Func<string, bool> isBound;

    [ObservableProperty]
    private BindingInspectorViewModel current;

    public KeyInspectorViewModel(KeyViewModel key, Gesture gesture, Func<string, BindingInspectorViewModel> createInspector, Func<string, bool> isBound, InputSwitchViewModel? inputSwitch)
    {
        this.key = key;
        Switch = inputSwitch;
        this.createInspector = createInspector;
        this.isBound = isBound;
        Tabs =
        [
            new GestureTabViewModel(Gesture.Press, "Drücken"),
            new GestureTabViewModel(Gesture.LongPress, "Lang"),
            new GestureTabViewModel(Gesture.DoublePress, "Doppelt")
        ];
        current = createInspector(InputFor(gesture));
        Mark(gesture);
    }

    public IReadOnlyList<GestureTabViewModel> Tabs { get; }

    /// <summary>Null in the quick editor: a key that can be pressed there is active anyway.</summary>
    public InputSwitchViewModel? Switch { get; }

    public Gesture Gesture => Tabs.First(t => t.IsActive).Gesture;

    public string Title => key.Title;

    public string ProfileName => Current.ProfileName;

    /// <summary>Explains the delay a normal press gets once another gesture is bound.</summary>
    public bool ShowsDelayHint => Tabs.Skip(1).Any(t => t.IsBound);

    public void Flush() => Current.Flush();

    [RelayCommand]
    private void SelectTab(GestureTabViewModel tab)
    {
        if (tab.IsActive)
        {
            return;
        }

        Current.Flush();
        Current = createInspector(InputFor(tab.Gesture));
        Mark(tab.Gesture);
    }

    /// <summary>Refreshes the "bound" marks after a save.</summary>
    public void RefreshMarks()
    {
        foreach (var tab in Tabs)
        {
            tab.IsBound = isBound(InputFor(tab.Gesture));
        }

        OnPropertyChanged(nameof(ShowsDelayHint));
    }

    private void Mark(Gesture gesture)
    {
        foreach (var tab in Tabs)
        {
            tab.IsActive = tab.Gesture == gesture;
        }

        RefreshMarks();
    }

    private string InputFor(Gesture gesture) => new GestureInput(key.Id, gesture).InputId;
}

/// <summary>Turns one key or slider on or off in all profiles (<c>settings.disabledInputs</c>).</summary>
public sealed partial class InputSwitchViewModel : ObservableObject
{
    private readonly string input;
    private readonly ConfigSession session;

    [ObservableProperty]
    private bool isActive;

    public InputSwitchViewModel(string input, ConfigSession session)
    {
        this.input = input;
        this.session = session;
        isActive = !session.Config.Settings.DisabledInputs.Contains(input, StringComparer.OrdinalIgnoreCase);
    }

    partial void OnIsActiveChanged(bool value)
    {
        session.Update(config =>
        {
            var others = config.Settings.DisabledInputs.Where(i => !string.Equals(i, input, StringComparison.OrdinalIgnoreCase));
            return config with { Settings = config.Settings with { DisabledInputs = value ? [.. others] : [.. others, input] } };
        });
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

    [ObservableProperty]
    private bool trackChange;

    [ObservableProperty]
    private bool displayEnabled;

    public DisplayInspectorViewModel(DisplayElementViewModel element, ConfigSession session)
    {
        Element = element;
        this.session = session;
        var feedback = session.Config.Settings.Feedback;
        actions = feedback.Actions;
        volume = feedback.Volume;
        errors = feedback.Errors;
        profileChange = feedback.ProfileChange;
        trackChange = feedback.TrackChange;
        displayEnabled = session.Config.Settings.DisplayEnabled;
        loading = false;
    }

    public DisplayElementViewModel Element { get; }

    partial void OnActionsChanged(bool value) => Save();

    partial void OnVolumeChanged(bool value) => Save();

    partial void OnErrorsChanged(bool value) => Save();

    partial void OnProfileChangeChanged(bool value) => Save();

    partial void OnTrackChangeChanged(bool value) => Save();

    partial void OnDisplayEnabledChanged(bool value) => Save();

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
                DisplayEnabled = DisplayEnabled,
                Feedback = new FeedbackSettings { Actions = Actions, Volume = Volume, Errors = Errors, ProfileChange = ProfileChange, TrackChange = TrackChange }
            }
        });
    }
}
