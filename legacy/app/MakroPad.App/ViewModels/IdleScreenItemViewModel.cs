using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MakroPad.Core.Configuration;

namespace MakroPad.App.ViewModels;

/// <summary>One idle screen in the list, edited live (no separate apply step).</summary>
public partial class IdleScreenItemViewModel : ObservableObject
{
    private readonly Action<IdleScreenItemViewModel>? onChanged;

    // Legacy activation fields are no longer edited but preserved so older configs round-trip.
    private readonly string legacyActivation;
    private readonly string legacyProcessName;

    public IdleScreenItemViewModel(IdleScreenConfig config, Action<IdleScreenItemViewModel>? onChanged = null)
    {
        this.onChanged = onChanged;
        name = string.IsNullOrWhiteSpace(config.Name) ? "Idle" : config.Name;
        enabled = config.Enabled;
        template = config.Template;
        templateMode = string.IsNullOrWhiteSpace(config.TemplateMode) ? "markdown" : config.TemplateMode;
        conditionLogic = string.Equals(config.ConditionLogic, "or", StringComparison.OrdinalIgnoreCase) ? "or" : "and";
        priority = config.Priority;
        exclusive = config.Exclusive;
        activationDelaySeconds = config.ActivationDelaySeconds;
        minDwellSeconds = config.MinDwellSeconds;
        legacyActivation = config.Activation;
        legacyProcessName = config.ProcessName;

        Conditions = new ObservableCollection<IdleScreenConditionViewModel>(
            config.Conditions.Select(c => new IdleScreenConditionViewModel(c, NotifyFromCondition)));
        Conditions.CollectionChanged += (_, _) => Notify();
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
    private string conditionLogic;

    [ObservableProperty]
    private int priority;

    [ObservableProperty]
    private bool exclusive;

    [ObservableProperty]
    private int activationDelaySeconds;

    [ObservableProperty]
    private int minDwellSeconds;

    public ObservableCollection<IdleScreenConditionViewModel> Conditions { get; }

    /// <summary>Adds a condition wired to this screen's change notifications.</summary>
    public void AddCondition(IdleScreenCondition condition) =>
        Conditions.Add(new IdleScreenConditionViewModel(condition, NotifyFromCondition));

    public string ConditionsSummary
    {
        get
        {
            if (Conditions.Count == 0)
            {
                return "Standard";
            }

            var separator = string.Equals(ConditionLogic, "or", StringComparison.OrdinalIgnoreCase) ? " od. " : " u. ";
            var summary = string.Join(separator, Conditions.Select(c => c.Summary));
            if (Priority != 0)
            {
                summary += $" · P{Priority}";
            }

            if (Exclusive)
            {
                summary += " · exkl.";
            }

            return summary;
        }
    }

    public string EnabledDisplay => Enabled ? "" : "aus";

    public IdleScreenConfig ToConfig() => new()
    {
        Name = Name.Trim(),
        Enabled = Enabled,
        Template = Template,
        TemplateMode = TemplateMode,
        Conditions = Conditions.Select(c => c.ToConfig()).ToList(),
        ConditionLogic = ConditionLogic,
        Priority = Priority,
        Exclusive = Exclusive,
        ActivationDelaySeconds = ActivationDelaySeconds,
        MinDwellSeconds = MinDwellSeconds,
        Activation = legacyActivation,
        ProcessName = legacyProcessName
    };

    partial void OnNameChanged(string value) => Notify();

    partial void OnEnabledChanged(bool value) => Notify();

    partial void OnTemplateChanged(string value) => Notify();

    partial void OnTemplateModeChanged(string value) => Notify();

    partial void OnConditionLogicChanged(string value) => Notify();

    partial void OnPriorityChanged(int value) => Notify();

    partial void OnExclusiveChanged(bool value) => Notify();

    partial void OnActivationDelaySecondsChanged(int value) => Notify();

    partial void OnMinDwellSecondsChanged(int value) => Notify();

    private void NotifyFromCondition() => Notify();

    private void Notify()
    {
        OnPropertyChanged(nameof(ConditionsSummary));
        OnPropertyChanged(nameof(EnabledDisplay));
        onChanged?.Invoke(this);
    }
}
