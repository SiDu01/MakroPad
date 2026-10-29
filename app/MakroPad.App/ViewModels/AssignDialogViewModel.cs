using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.App.Services;
using MakroPad.Core.Actions;
using MakroPad.Core.Device;
using MakroPad.Core.Runtime;

namespace MakroPad.App.ViewModels;

/// <summary>
/// Quick editor for one key, opened by <c>binding.assign</c>. Edits stay drafts (one per gesture,
/// kept while switching tabs) until <see cref="SaveCommand"/> writes them all at once.
/// </summary>
public sealed partial class AssignDialogViewModel : ObservableObject
{
    private readonly ConfigSession session;
    private readonly Dictionary<string, BindingInspectorViewModel> drafts = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private string? error;

    public AssignDialogViewModel(KeyViewModel key, ConfigSession session, MakroPadRuntime runtime, ParameterFactory factory)
    {
        this.session = session;
        BindingInspectorViewModel Draft(string input)
        {
            if (!drafts.TryGetValue(input, out var draft))
            {
                draft = new BindingInspectorViewModel(key, input, InputKinds.Button, session, runtime, factory) { ShowTitle = false, Deferred = true };
                drafts[input] = draft;
            }

            return draft;
        }

        bool IsBound(string input) => drafts.TryGetValue(input, out var draft)
            ? draft.IsBound
            : session.Config.FindProfile(runtime.Profiles.Active.Name)?.Bindings.Any(b => string.Equals(b.Input, input, StringComparison.OrdinalIgnoreCase)) == true;

        Editor = new KeyInspectorViewModel(key, Gesture.Press, Draft, IsBound, inputSwitch: null);
    }

    public KeyInspectorViewModel Editor { get; }

    public string Title => $"{Editor.Title} belegen";

    public event Action? Saved;

    /// <summary>Raised when the window should close (saved or cancelled).</summary>
    public event Action? Closed;

    [RelayCommand]
    private void Save()
    {
        Error = session.Update(config => drafts.Values.Aggregate(config, (edited, draft) => draft.Apply(edited)));
        if (Error is null)
        {
            Saved?.Invoke();
            Closed?.Invoke();
        }
    }

    [RelayCommand]
    private void Cancel() => Closed?.Invoke();
}
