using System;
using System.Collections.Generic;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.App.Services;

namespace MakroPad.App.ViewModels;

public partial class ActionEditorViewModel : ViewModelBase
{
    private readonly ActionDefinitionCatalog actionDefinitionCatalog;
    private readonly BindingEditorService bindingEditorService;
    private bool isRefreshing;

    public ActionEditorViewModel(
        ActionDefinitionCatalog actionDefinitionCatalog,
        BindingEditorService bindingEditorService,
        IReadOnlyCollection<string> availableAudioProcessNames)
    {
        this.actionDefinitionCatalog = actionDefinitionCatalog;
        this.bindingEditorService = bindingEditorService;
        AvailableAudioProcessNames = availableAudioProcessNames;
        RefreshHints();
    }

    public IReadOnlyList<string> AvailableActions => actionDefinitionCatalog.ActionIds;

    public IReadOnlyCollection<string> AvailableAudioProcessNames { get; }

    [ObservableProperty]
    private string action = "media.playPause";

    partial void OnActionChanged(string value)
    {
        RefreshHints();
        RefreshStructuredParameterFromRaw();
        OnPropertyChanged(nameof(HasStructuredParameter));
        OnPropertyChanged(nameof(IsAppVolumeAction));
        OnPropertyChanged(nameof(IsKeyboardShortcutAction));
        OnPropertyChanged(nameof(IsOpenAppAction));
        OnPropertyChanged(nameof(ShowsStructuredTextInput));
        OnPropertyChanged(nameof(StructuredParameterLabel));
        OnPropertyChanged(nameof(StructuredParameterValidationMessage));
    }

    [ObservableProperty]
    private string parameters = "";

    partial void OnParametersChanged(string value)
    {
        if (isRefreshing)
        {
            return;
        }

        RefreshStructuredParameterFromRaw();
        OnPropertyChanged(nameof(StructuredParameterValidationMessage));
    }

    [ObservableProperty]
    private string structuredParameterValue = "";

    partial void OnStructuredParameterValueChanged(string value)
    {
        if (isRefreshing)
        {
            return;
        }

        UpdateRawParametersFromStructuredValue();
        OnPropertyChanged(nameof(StructuredParameterValidationMessage));
    }

    [ObservableProperty]
    private string parameterPlaceholder = "";

    [ObservableProperty]
    private string parameterHint = "";

    [ObservableProperty]
    private string actionDescription = "";

    [ObservableProperty]
    private bool isRecordingShortcut;

    [ObservableProperty]
    private string shortcutRecordingStatus = "";

    public bool HasStructuredParameter => GetStructuredParameterKey(Action) is not null;

    public bool IsAppVolumeAction => Action.Equals("app.volume", StringComparison.OrdinalIgnoreCase);

    public bool IsKeyboardShortcutAction => Action.Equals("keyboard.shortcut", StringComparison.OrdinalIgnoreCase);

    public bool IsOpenAppAction => Action.Equals("system.openApp", StringComparison.OrdinalIgnoreCase);

    public bool ShowsStructuredTextInput => HasStructuredParameter && !IsAppVolumeAction;

    public string StructuredParameterValidationMessage
    {
        get
        {
            if (!HasStructuredParameter || string.IsNullOrWhiteSpace(StructuredParameterValue))
            {
                return "";
            }

            if (Action.Equals("system.openUrl", StringComparison.OrdinalIgnoreCase)
                && (!Uri.TryCreate(StructuredParameterValue, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            {
                return "URL must start with http:// or https://.";
            }

            if (IsAppVolumeAction
                && !AvailableAudioProcessNames.Contains(StructuredParameterValue, StringComparer.OrdinalIgnoreCase))
            {
                return "Process is not in the current audio session list.";
            }

            return "";
        }
    }

    public string StructuredParameterLabel =>
        actionDefinitionCatalog.TryGet(Action, out var definition)
            ? definition.StructuredParameterLabel
            : "Parameter";

    public void Load(string? action, string? parameters)
    {
        isRefreshing = true;
        Action = action ?? AvailableActions.FirstOrDefault() ?? "";
        Parameters = parameters ?? "";
        isRefreshing = false;
        RefreshHints();
        RefreshStructuredParameterFromRaw();
    }

    public void NotifyAudioProcessNamesChanged()
    {
        OnPropertyChanged(nameof(StructuredParameterValidationMessage));
    }

    [RelayCommand]
    private void UseParameterExample()
    {
        Parameters = string.IsNullOrWhiteSpace(ParameterPlaceholder)
            ? ""
            : ParameterPlaceholder;
    }

    [RelayCommand]
    private void StartShortcutRecording()
    {
        Action = "keyboard.shortcut";
        ShortcutRecordingStatus = "Press a shortcut. Esc cancels.";
        IsRecordingShortcut = true;
    }

    public void CompleteShortcutRecording(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            CancelShortcutRecording();
            return;
        }

        Action = "keyboard.shortcut";
        Parameters = $"keys={shortcut}";
        ShortcutRecordingStatus = $"Recorded: {shortcut}";
        IsRecordingShortcut = false;
    }

    public void CancelShortcutRecording()
    {
        IsRecordingShortcut = false;
        ShortcutRecordingStatus = "Recording cancelled.";
    }

    public void SetOpenAppPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Action = "system.openApp";
        Parameters = $"path={path}";
    }

    private void RefreshHints()
    {
        var action = Action.Trim();
        if (!actionDefinitionCatalog.TryGet(action, out var definition))
        {
            ActionDescription = "Custom or unsupported action.";
            ParameterHint = "Use key=value pairs separated by commas.";
            ParameterPlaceholder = "key=value";
            return;
        }

        ActionDescription = definition.Description;
        ParameterHint = definition.ParameterHint;
        ParameterPlaceholder = definition.ParameterPlaceholder;
    }

    private void RefreshStructuredParameterFromRaw()
    {
        var key = GetStructuredParameterKey(Action);
        isRefreshing = true;
        StructuredParameterValue = key is null
            ? ""
            : bindingEditorService.ParseParameters(Parameters, Action).GetValueOrDefault(key, "");
        isRefreshing = false;
    }

    private void UpdateRawParametersFromStructuredValue()
    {
        var key = GetStructuredParameterKey(Action);
        if (key is null)
        {
            return;
        }

        isRefreshing = true;
        Parameters = string.IsNullOrWhiteSpace(StructuredParameterValue)
            ? ""
            : $"{key}={StructuredParameterValue.Trim()}";
        isRefreshing = false;
    }

    private string? GetStructuredParameterKey(string action)
    {
        return actionDefinitionCatalog.TryGet(action, out var definition)
            ? definition.StructuredParameterKey
            : null;
    }
}
