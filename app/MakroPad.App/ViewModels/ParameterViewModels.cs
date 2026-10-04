using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.Core.Actions;

namespace MakroPad.App.ViewModels;

/// <summary>One editable action parameter; the view picks a template by the concrete type.</summary>
public abstract partial class ParameterViewModel(ParameterDefinition definition) : ObservableObject
{
    [ObservableProperty]
    private string value = "";

    public ParameterDefinition Definition { get; } = definition;

    public string Name => Definition.Name;

    public string Label => BindingText.ParameterLabel(Definition.Name) + (Definition.Required ? "" : " (optional)");

    public string Hint => Definition.Hint;

    public string Placeholder => Definition.Example;

    public event Action? Edited;

    partial void OnValueChanged(string value) => Edited?.Invoke();
}

public sealed class TextParameterViewModel(ParameterDefinition definition) : ParameterViewModel(definition);

public sealed partial class ShortcutParameterViewModel(ParameterDefinition definition, IShortcutRecorder recorder) : ParameterViewModel(definition)
{
    [ObservableProperty]
    private bool isRecording;

    [ObservableProperty]
    private string? recordError;

    public string RecordText => IsRecording ? "Drücke die Kombination …" : "Aufnehmen";

    async partial void OnIsRecordingChanged(bool value)
    {
        OnPropertyChanged(nameof(RecordText));
        if (!value)
        {
            recorder.Cancel();
            return;
        }

        RecordError = null;
        try
        {
            if (await recorder.RecordAsync() is { } shortcut)
            {
                Value = shortcut;
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            RecordError = $"Aufnahme nicht möglich: {ex.Message}. Tippe die Kombination stattdessen ein.";
        }

        IsRecording = false;
    }
}

public sealed partial class ProcessParameterViewModel : ParameterViewModel
{
    private readonly Func<IReadOnlyList<string>> listSessions;

    public ProcessParameterViewModel(ParameterDefinition definition, Func<IReadOnlyList<string>> listSessions)
        : base(definition)
    {
        this.listSessions = listSessions;
        Refresh();
    }

    public ObservableCollection<string> Sessions { get; } = [];

    public bool HasSessions => Sessions.Count > 0;

    [RelayCommand]
    private void Refresh()
    {
        Sessions.Clear();
        foreach (var session in listSessions())
        {
            Sessions.Add(session);
        }

        OnPropertyChanged(nameof(HasSessions));
    }

    [RelayCommand]
    private void Use(string process) => Value = process;
}

public sealed partial class PathParameterViewModel(ParameterDefinition definition, IFilePicker picker) : ParameterViewModel(definition)
{
    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (await picker.PickProgramAsync() is { } path)
        {
            Value = path;
        }
    }
}

public sealed class ProfileParameterViewModel(ParameterDefinition definition, IReadOnlyList<string> profiles) : ParameterViewModel(definition)
{
    public IReadOnlyList<string> Profiles { get; } = profiles;
}

public interface IFilePicker
{
    Task<string?> PickProgramAsync();
}

/// <summary>Captures the next key combination system-wide (see MakroPad.Windows.ShortcutRecorder).</summary>
public interface IShortcutRecorder
{
    Task<string?> RecordAsync();

    void Cancel();
}
