using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MakroPad.Core.Configuration;

namespace MakroPad.App.ViewModels;

/// <summary>One idle screen in the list, edited live (no separate apply step).</summary>
public partial class IdleScreenItemViewModel : ObservableObject
{
    private readonly Action<IdleScreenItemViewModel>? onChanged;

    public IdleScreenItemViewModel(IdleScreenConfig config, Action<IdleScreenItemViewModel>? onChanged = null)
    {
        this.onChanged = onChanged;
        name = string.IsNullOrWhiteSpace(config.Name) ? "Idle" : config.Name;
        enabled = config.Enabled;
        template = config.Template;
        templateMode = string.IsNullOrWhiteSpace(config.TemplateMode) ? "markdown" : config.TemplateMode;
        activation = string.Equals(config.Activation, "process", StringComparison.OrdinalIgnoreCase) ? "process" : "default";
        processName = config.ProcessName;
    }

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private bool enabled;

    [ObservableProperty]
    private string template;

    [ObservableProperty]
    private string templateMode;

    [ObservableProperty]
    private string activation;

    [ObservableProperty]
    private string processName;

    partial void OnNameChanged(string value) => Notify();

    partial void OnEnabledChanged(bool value) => Notify();

    partial void OnTemplateChanged(string value) => Notify();

    partial void OnTemplateModeChanged(string value) => Notify();

    partial void OnActivationChanged(string value) => Notify();

    partial void OnProcessNameChanged(string value) => Notify();

    public string ActivationDisplay => string.Equals(Activation, "process", StringComparison.OrdinalIgnoreCase)
        ? $"App: {(string.IsNullOrWhiteSpace(ProcessName) ? "?" : ProcessName)}"
        : "Standard";

    public string EnabledDisplay => Enabled ? "" : "aus";

    public IdleScreenConfig ToConfig() => new()
    {
        Name = Name.Trim(),
        Enabled = Enabled,
        Template = Template,
        TemplateMode = TemplateMode,
        Activation = Activation,
        ProcessName = ProcessName.Trim()
    };

    private void Notify()
    {
        OnPropertyChanged(nameof(ActivationDisplay));
        OnPropertyChanged(nameof(EnabledDisplay));
        onChanged?.Invoke(this);
    }
}
