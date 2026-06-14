using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.App.Services;
using MakroPad.Core.Configuration;
using MakroPad.Core.DisplayTriggers;

namespace MakroPad.App.ViewModels;

public partial class DisplayTriggersEditorViewModel : ViewModelBase
{
    private static readonly IReadOnlyList<DisplayTriggerPreset> DisplayTriggerPresets =
    [
        new("Slider value", "input", "slider:1", "any", "any", "any", 0, 100, 5, 1500, "layout", "Volume\nSlider {slider.id}"),
        new("Button pressed", "input", "button:1", "any", "any", "any", 0, 100, 0, 1500, "normal", "Input\n{input}"),
        new("Action completed", "actionResult", "*", "ok", "any", "any", 0, 100, 0, 3000, "layout", "Action\n{action.message}"),
        new("Action failed", "actionResult", "*", "failed", "any", "any", 0, 100, 0, 3000, "layout", "Action failed\n{action.message}"),
        new("Now playing", "mediaChanged", "*", "any", "any", "any", 0, 100, 0, 0, "layout", "Media\n{media.artist}\n{media.title}"),
        new("Connection status", "connectionChanged", "*", "any", "any", "any", 0, 100, 0, 2000, "layout", "MakroPad\n{connection.status}"),
        new("Profile changed", "profileChanged", "*", "any", "any", "any", 0, 100, 0, 2000, "layout", "Profile\n{profile}"),
        new("HTML list", "input", "button:1", "any", "any", "any", 0, 100, 0, 2500, "html", "<h1>{profile}</h1><p>{input}</p><ul><li>{status}</li></ul>"),
        new("Markdown", "input", "button:1", "any", "any", "any", 0, 100, 0, 2500, "markdown", "# {profile}\n{input}\n- {status}")
    ];

    private static readonly HashSet<string> KnownPlaceholders = new(
    [
        "{input}", "{slider.id}", "{slider.value}", "{slider.percent}",
        "{action.status}", "{action.name}", "{action.message}",
        "{media.artist}", "{media.title}", "{media.track}",
        "{connection.status}", "{profile}", "{status}"
    ], StringComparer.OrdinalIgnoreCase);

    private bool isRefreshingEditor;
    private string activeProfile = "Default";
    private string runtimeConnectionStatus = "Disconnected";

    public event EventHandler? Changed;

    public event EventHandler<DisplayTriggerTestRequestedEventArgs>? TestRequested;

    public ObservableCollection<DisplayTriggerViewModel> DisplayTriggers { get; } = [];

    public IReadOnlyList<string> AvailableDisplayTriggerInputs { get; } =
    [
        "*",
        ..Enumerable.Range(1, 20).Select(id => $"button:{id}"),
        ..Enumerable.Range(1, 3).Select(id => $"slider:{id}")
    ];

    public IReadOnlyList<string> AvailableDisplayTemplateModes { get; } = ["normal", "markdown", "html", "layout"];

    public IReadOnlyList<string> AvailableDisplayTriggerEventTypes { get; } =
        ["input", "actionResult", "mediaChanged", "connectionChanged", "profileChanged"];

    public IReadOnlyList<string> AvailableDisplayTriggerActionStatuses { get; } = ["any", "ok", "failed"];

    public IReadOnlyList<string> AvailableDisplayTriggerConnectionStatuses { get; } =
        ["any", "connected", "connecting", "unresponsive", "disconnected"];

    public IReadOnlyList<string> AvailableDisplayTriggerConditions { get; } =
        ["any", "inRange", "increasing", "decreasing"];

    public IReadOnlyList<DisplayTriggerPreset> AvailableDisplayTriggerPresets => DisplayTriggerPresets;

    [ObservableProperty]
    private DisplayTriggerViewModel? selectedDisplayTrigger;

    partial void OnSelectedDisplayTriggerChanged(DisplayTriggerViewModel? value)
    {
        if (isRefreshingEditor)
        {
            return;
        }

        LoadEditor(value);
    }

    [ObservableProperty]
    private bool displayTriggerEditorEnabled = true;

    partial void OnDisplayTriggerEditorEnabledChanged(bool value) => RefreshPreview();

    [ObservableProperty]
    private string displayTriggerEditorEventType = "input";

    partial void OnDisplayTriggerEditorEventTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsInputDisplayTrigger));
        OnPropertyChanged(nameof(IsSliderInputDisplayTrigger));
        OnPropertyChanged(nameof(IsActionResultDisplayTrigger));
        OnPropertyChanged(nameof(IsMediaChangedDisplayTrigger));
        OnPropertyChanged(nameof(IsConnectionChangedDisplayTrigger));
        OnPropertyChanged(nameof(ShowsDisplayTriggerInputMatch));
        OnPropertyChanged(nameof(ShowsDisplayTriggerPreviewValues));
        OnPropertyChanged(nameof(DisplayTriggerPlaceholderHint));
        RefreshPreview();
    }

    [ObservableProperty]
    private string displayTriggerEditorInput = "slider:1";

    partial void OnDisplayTriggerEditorInputChanged(string value)
    {
        OnPropertyChanged(nameof(IsSliderInputDisplayTrigger));
        OnPropertyChanged(nameof(ShowsDisplayTriggerPreviewValues));
        RefreshPreview();
    }

    [ObservableProperty]
    private string displayTriggerEditorActionStatus = "any";

    partial void OnDisplayTriggerEditorActionStatusChanged(string value) => RefreshPreview();

    [ObservableProperty]
    private string displayTriggerEditorConnectionStatus = "any";

    partial void OnDisplayTriggerEditorConnectionStatusChanged(string value) => RefreshPreview();

    [ObservableProperty]
    private string displayTriggerEditorCondition = "any";

    partial void OnDisplayTriggerEditorConditionChanged(string value) => RefreshPreview();

    [ObservableProperty]
    private int displayTriggerEditorMinPercent;

    partial void OnDisplayTriggerEditorMinPercentChanged(int value) => RefreshPreview();

    [ObservableProperty]
    private int displayTriggerEditorMaxPercent = 100;

    partial void OnDisplayTriggerEditorMaxPercentChanged(int value) => RefreshPreview();

    [ObservableProperty]
    private int displayTriggerEditorChangePercent = 5;

    partial void OnDisplayTriggerEditorChangePercentChanged(int value) => RefreshPreview();

    [ObservableProperty]
    private int displayTriggerEditorDurationMs = 3000;

    partial void OnDisplayTriggerEditorDurationMsChanged(int value) => RefreshPreview();

    [ObservableProperty]
    private string displayTriggerEditorTemplateMode = "normal";

    partial void OnDisplayTriggerEditorTemplateModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsMarkdownDisplayTrigger));
        OnPropertyChanged(nameof(ShowsBlockEditor));
        OnPropertyChanged(nameof(ShowsRawTemplate));
        if (!IsMarkdownDisplayTrigger)
        {
            UseBlockEditor = false;
        }

        RefreshPreview();
    }

    [ObservableProperty]
    private string displayTriggerEditorTemplate = DisplayTriggerNormalizer.DefaultTemplate;

    partial void OnDisplayTriggerEditorTemplateChanged(string value) => RefreshPreview();

    // ----- Visual block ("Baukasten") editor for the Markdown content mode -----

    [ObservableProperty]
    private bool useBlockEditor;

    partial void OnUseBlockEditorChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowsBlockEditor));
        OnPropertyChanged(nameof(ShowsRawTemplate));
    }

    public bool IsMarkdownDisplayTrigger =>
        DisplayTriggerNormalizer.NormalizeTemplateMode(DisplayTriggerEditorTemplateMode) == "markdown";

    public bool ShowsBlockEditor => IsMarkdownDisplayTrigger && UseBlockEditor;

    public bool ShowsRawTemplate => !ShowsBlockEditor;

    public ObservableCollection<DisplayBlockViewModel> DisplayBlocks { get; } = [];

    public IReadOnlyList<string> AvailableDisplayPlaceholders { get; } =
    [
        "{profile}", "{status}", "{input}", "{slider.percent}", "{slider.id}",
        "{media.artist}", "{media.title}", "{media.track}", "{action.message}", "{connection.status}"
    ];

    private DisplayBlockViewModel? activeBlock;
    private bool isSyncingBlocks;

    [RelayCommand]
    private void ToggleBlockEditor()
    {
        if (!UseBlockEditor)
        {
            ParseBlocksFromTemplate();
        }

        UseBlockEditor = !UseBlockEditor;
    }

    [RelayCommand]
    private void AddHeadingBlock() => AddBlock(DisplayBlockKind.Heading, "Titel");

    [RelayCommand]
    private void AddTextBlock() => AddBlock(DisplayBlockKind.Text, "Text");

    [RelayCommand]
    private void AddListBlock() => AddBlock(DisplayBlockKind.ListItem, "Punkt");

    [RelayCommand]
    private void AddDividerBlock() => AddBlock(DisplayBlockKind.Divider, "");

    [RelayCommand]
    private void RemoveBlock(DisplayBlockViewModel? block)
    {
        if (block is not null && DisplayBlocks.Remove(block))
        {
            if (ReferenceEquals(activeBlock, block))
            {
                activeBlock = null;
            }

            SerializeBlocksToTemplate();
        }
    }

    [RelayCommand]
    private void MoveBlockUp(DisplayBlockViewModel? block) => MoveBlock(block, -1);

    [RelayCommand]
    private void MoveBlockDown(DisplayBlockViewModel? block) => MoveBlock(block, 1);

    [RelayCommand]
    private void InsertPlaceholder(string? placeholder)
    {
        if (string.IsNullOrEmpty(placeholder))
        {
            return;
        }

        var target = activeBlock?.IsEditable == true
            ? activeBlock
            : DisplayBlocks.LastOrDefault(b => b.IsEditable);
        if (target is not null)
        {
            target.Text += placeholder;
        }
    }

    private void AddBlock(DisplayBlockKind kind, string text)
    {
        var block = new DisplayBlockViewModel(kind, text, OnBlockChanged);
        DisplayBlocks.Add(block);
        if (block.IsEditable)
        {
            activeBlock = block;
        }

        SerializeBlocksToTemplate();
    }

    private void MoveBlock(DisplayBlockViewModel? block, int direction)
    {
        if (block is null)
        {
            return;
        }

        var index = DisplayBlocks.IndexOf(block);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= DisplayBlocks.Count)
        {
            return;
        }

        DisplayBlocks.Move(index, target);
        SerializeBlocksToTemplate();
    }

    private void OnBlockChanged(DisplayBlockViewModel block)
    {
        activeBlock = block;
        SerializeBlocksToTemplate();
    }

    public void ParseBlocksFromTemplate()
    {
        isSyncingBlocks = true;
        DisplayBlocks.Clear();
        var lines = (DisplayTriggerEditorTemplate ?? "")
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            DisplayBlockKind kind;
            var text = "";
            if (line.Length >= 3 && line.All(c => c == '-'))
            {
                kind = DisplayBlockKind.Divider;
            }
            else if (line.StartsWith('#'))
            {
                kind = DisplayBlockKind.Heading;
                text = line.TrimStart('#').Trim();
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                kind = DisplayBlockKind.ListItem;
                text = line[2..].Trim();
            }
            else
            {
                kind = DisplayBlockKind.Text;
                text = line;
            }

            DisplayBlocks.Add(new DisplayBlockViewModel(kind, text, OnBlockChanged));
        }

        activeBlock = DisplayBlocks.LastOrDefault(b => b.IsEditable);
        isSyncingBlocks = false;
    }

    public void SerializeBlocksToTemplate()
    {
        if (isSyncingBlocks)
        {
            return;
        }

        DisplayTriggerEditorTemplate = string.Join("\n", DisplayBlocks.Select(SerializeBlock));
    }

    private static string SerializeBlock(DisplayBlockViewModel block) => block.Kind switch
    {
        DisplayBlockKind.Heading => "# " + block.Text.Trim(),
        DisplayBlockKind.ListItem => "- " + block.Text.Trim(),
        DisplayBlockKind.Divider => "---",
        _ => block.Text.Trim()
    };

    [ObservableProperty]
    private string displayTriggerEditorValidationMessage = "";

    [ObservableProperty]
    private DisplayTriggerPreset? selectedDisplayTriggerPreset;

    [ObservableProperty]
    private int displayTriggerPreviewPercent = 50;

    partial void OnDisplayTriggerPreviewPercentChanged(int value) => RefreshPreview();

    [ObservableProperty]
    private int displayTriggerPreviewPreviousPercent = 40;

    partial void OnDisplayTriggerPreviewPreviousPercentChanged(int value) => RefreshPreview();

    [ObservableProperty]
    private string displayTriggerPreviewActionStatus = "ok";

    partial void OnDisplayTriggerPreviewActionStatusChanged(string value) => RefreshPreview();

    [ObservableProperty]
    private string displayTriggerPreviewActionName = "media.playPause";

    partial void OnDisplayTriggerPreviewActionNameChanged(string value) => RefreshPreview();

    [ObservableProperty]
    private string displayTriggerPreviewActionMessage = "Action completed";

    partial void OnDisplayTriggerPreviewActionMessageChanged(string value) => RefreshPreview();

    [ObservableProperty]
    private string displayTriggerPreviewMediaArtist = "Artist";

    partial void OnDisplayTriggerPreviewMediaArtistChanged(string value) => RefreshPreview();

    [ObservableProperty]
    private string displayTriggerPreviewMediaTitle = "Track title";

    partial void OnDisplayTriggerPreviewMediaTitleChanged(string value) => RefreshPreview();

    [ObservableProperty]
    private string displayTriggerPreviewConnectionStatus = "connected";

    partial void OnDisplayTriggerPreviewConnectionStatusChanged(string value) => RefreshPreview();

    [ObservableProperty]
    private string displayTriggerPreviewLine1 = "";

    [ObservableProperty]
    private string displayTriggerPreviewLine2 = "";

    [ObservableProperty]
    private string displayTriggerPreviewLine3 = "";

    [ObservableProperty]
    private string displayTriggerPreviewLine4 = "";

    [ObservableProperty]
    private string displayTriggerPreviewLine5 = "";

    [ObservableProperty]
    private bool usesLayoutDisplayPreview;

    public bool UsesTextDisplayPreview => !UsesLayoutDisplayPreview;

    [ObservableProperty]
    private string displayTriggerLayoutPreviewTitle = "";

    [ObservableProperty]
    private string displayTriggerLayoutPreviewLine1 = "";

    [ObservableProperty]
    private string displayTriggerLayoutPreviewLine2 = "";

    [ObservableProperty]
    private string displayTriggerLayoutPreviewIcon = "";

    public bool HasDisplayTriggerLayoutPreviewIcon => !string.IsNullOrWhiteSpace(DisplayTriggerLayoutPreviewIcon);

    [ObservableProperty]
    private int displayTriggerLayoutPreviewValue;

    [ObservableProperty]
    private string displayTriggerLayoutPreviewValueText = "";

    [ObservableProperty]
    private bool showsDisplayTriggerLayoutPreviewValue;

    public string DisplayTriggerPlaceholderHint => NormalizeEventType(DisplayTriggerEditorEventType) switch
    {
        "actionResult" => "Placeholders: {action.status}, {action.name}, {action.message}, {input}, {profile}, {status}.",
        "mediaChanged" => "Placeholders: {media.artist}, {media.title}, {media.track}, {profile}, {status}.",
        "connectionChanged" => "Placeholders: {connection.status}, {profile}, {status}.",
        "profileChanged" => "Placeholders: {profile}, {status}.",
        _ => "Placeholders: {input}, {slider.id}, {slider.value}, {slider.percent}, {profile}, {status}."
    };

    public bool IsInputDisplayTrigger => NormalizeEventType(DisplayTriggerEditorEventType) == "input";

    public bool IsSliderInputDisplayTrigger =>
        IsInputDisplayTrigger
        && (DisplayTriggerEditorInput ?? "").StartsWith("slider:", StringComparison.OrdinalIgnoreCase);

    public bool IsActionResultDisplayTrigger => NormalizeEventType(DisplayTriggerEditorEventType) == "actionResult";

    public bool IsMediaChangedDisplayTrigger => NormalizeEventType(DisplayTriggerEditorEventType) == "mediaChanged";

    public bool IsConnectionChangedDisplayTrigger => NormalizeEventType(DisplayTriggerEditorEventType) == "connectionChanged";

    public bool ShowsDisplayTriggerInputMatch => IsInputDisplayTrigger || IsActionResultDisplayTrigger;

    public bool ShowsDisplayTriggerPreviewValues =>
        IsSliderInputDisplayTrigger
        || IsActionResultDisplayTrigger
        || IsMediaChangedDisplayTrigger
        || IsConnectionChangedDisplayTrigger;

    public string DisplayTriggerPreviewMatchStatus
    {
        get
        {
            var trigger = CreateEditorTriggerForPreview();
            var preview = CreatePreviewContext(trigger);
            var matches = NormalizeEventType(trigger.EventType) switch
            {
                "actionResult" => NormalizeActionStatus(trigger.ActionStatus) == "any"
                    || NormalizeActionStatus(trigger.ActionStatus) == NormalizeActionStatus(preview.ActionStatus),
                "connectionChanged" => NormalizeConnectionStatus(trigger.ConnectionStatus) == "any"
                    || NormalizeConnectionStatus(trigger.ConnectionStatus) == NormalizeConnectionStatus(preview.ConnectionStatus),
                "mediaChanged" or "profileChanged" => true,
                _ when preview.Percent is not null =>
                    DisplayTriggerMatcher.ThresholdMatches(
                        trigger.ChangePercent,
                        preview.Percent.Value,
                        NormalizePercent(DisplayTriggerPreviewPreviousPercent))
                    && DisplayTriggerMatcher.ConditionMatches(
                        trigger,
                        preview.Percent.Value,
                        NormalizePercent(DisplayTriggerPreviewPreviousPercent)),
                _ => true
            };

            return matches ? "Trigger would fire" : "Trigger would not fire";
        }
    }

    public string DisplayTriggerEditorContentWarning
    {
        get
        {
            var placeholders = Regex.Matches(DisplayTriggerEditorTemplate ?? "", "\\{[^{}]+\\}")
                .Select(match => match.Value.ToLowerInvariant())
                .Distinct()
                .ToList();
            var unknown = placeholders.Where(placeholder => !KnownPlaceholders.Contains(placeholder)).ToList();
            if (unknown.Count > 0)
            {
                return $"Unknown placeholder: {string.Join(", ", unknown)}";
            }

            var foreignPrefixes = NormalizeEventType(DisplayTriggerEditorEventType) switch
            {
                "actionResult" => new[] { "{slider.", "{media.", "{connection." },
                "mediaChanged" => new[] { "{slider.", "{action.", "{connection.", "{input}" },
                "connectionChanged" => new[] { "{slider.", "{action.", "{media.", "{input}" },
                "profileChanged" => new[] { "{slider.", "{action.", "{media.", "{connection.", "{input}" },
                _ => new[] { "{action.", "{media.", "{connection." }
            };
            if (placeholders.Any(placeholder =>
                foreignPrefixes.Any(prefix => placeholder.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
            {
                return "Content uses placeholders that are not populated by this event type.";
            }

            var renderedLines = (DisplayTriggerEditorTemplate ?? "")
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (renderedLines.Length > 5)
            {
                return "Only the first five non-empty display lines are shown.";
            }

            if (DisplayTriggerNormalizer.NormalizeTemplateMode(DisplayTriggerEditorTemplateMode) == "normal"
                && renderedLines.Any(line => line.Length > DisplayTriggerRenderer.MaxPreviewCharacters))
            {
                return $"Long OLED lines scroll on the device; preview shows the first {DisplayTriggerRenderer.MaxPreviewCharacters} characters.";
            }

            return "";
        }
    }

    public bool IsDisplayTriggerEditorDirty
    {
        get
        {
            if (SelectedDisplayTrigger is null)
            {
                return false;
            }

            return CreateEditorTriggerForPreview() != ToConfig(SelectedDisplayTrigger);
        }
    }

    public string DisplayTriggerSequenceInfo
    {
        get
        {
            if (SelectedDisplayTrigger is null)
            {
                return "";
            }

            var matching = DisplayTriggers
                .Where(trigger => trigger.Enabled
                    && trigger.EventType == SelectedDisplayTrigger.EventType
                    && trigger.MatchDisplay == SelectedDisplayTrigger.MatchDisplay)
                .ToList();
            if (matching.Count <= 1)
            {
                return SelectedDisplayTrigger.DurationMs == 0
                    ? "Duration 0 ms keeps this screen visible until another display message arrives."
                    : "";
            }

            var totalDuration = matching.Sum(trigger => trigger.DurationMs);
            return $"{matching.Count} triggers share this match and run in list order. Total configured duration: {totalDuration} ms.";
        }
    }

    public void Load(IReadOnlyList<DisplayTriggerConfig> triggers, int selectedIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(triggers);

        DisplayTriggers.Clear();
        for (var index = 0; index < triggers.Count; index++)
        {
            DisplayTriggers.Add(ToViewModel(index, Normalize(triggers[index])));
        }

        SetSelection(selectedIndex);
    }

    public IReadOnlyList<DisplayTriggerConfig> Export()
    {
        return DisplayTriggers.Select(ToConfig).ToList();
    }

    public bool TryCommitPendingEdit()
    {
        if (SelectedDisplayTrigger is null)
        {
            return true;
        }

        if (!TryCreateValidatedEditorTrigger(out var trigger))
        {
            return false;
        }

        var selectedIndex = SelectedDisplayTrigger.Index;
        if (selectedIndex < 0 || selectedIndex >= DisplayTriggers.Count)
        {
            DisplayTriggerEditorValidationMessage = "Selected display trigger no longer exists.";
            return false;
        }

        var triggers = Export().ToList();
        triggers[selectedIndex] = trigger;
        UpdateTriggers(triggers, selectedIndex);
        return true;
    }

    public void SetPreviewRuntime(
        string activeProfile,
        string connectionStatus,
        int lastSliderValue,
        NowPlayingTrack? mediaTrack)
    {
        this.activeProfile = activeProfile ?? "";
        runtimeConnectionStatus = connectionStatus ?? "";
        DisplayTriggerPreviewPercent = DisplayTriggerRenderer.MapSliderValueToPercent(lastSliderValue);
        DisplayTriggerPreviewConnectionStatus = NormalizeConnectionStatus(connectionStatus);
        DisplayTriggerPreviewMediaArtist = mediaTrack?.Artist ?? "";
        DisplayTriggerPreviewMediaTitle = mediaTrack?.Title ?? "";
        RefreshPreview();
    }

    [RelayCommand]
    private void ApplyDisplayTrigger()
    {
        TryCommitPendingEdit();
    }

    [RelayCommand]
    private void ResetDisplayTriggerEditor()
    {
        LoadEditor(SelectedDisplayTrigger);
    }

    [RelayCommand]
    private void AddDisplayTrigger()
    {
        if (!TryCreateValidatedEditorTrigger(out var trigger))
        {
            return;
        }

        var triggers = Export().ToList();
        triggers.Add(trigger);
        UpdateTriggers(triggers, triggers.Count - 1);
    }

    [RelayCommand]
    private void DuplicateDisplayTrigger()
    {
        if (!TryGetSelectedIndex(out var selectedIndex))
        {
            return;
        }

        var triggers = Export().ToList();
        var insertIndex = selectedIndex + 1;
        triggers.Insert(insertIndex, triggers[selectedIndex] with { });
        UpdateTriggers(triggers, insertIndex);
    }

    [RelayCommand]
    private void ApplyDisplayTriggerPreset()
    {
        if (SelectedDisplayTriggerPreset is null)
        {
            DisplayTriggerEditorValidationMessage = "No display trigger preset selected.";
            return;
        }

        var preset = SelectedDisplayTriggerPreset;
        isRefreshingEditor = true;
        try
        {
            DisplayTriggerEditorEnabled = true;
            DisplayTriggerEditorEventType = preset.EventType;
            DisplayTriggerEditorInput = preset.Input;
            DisplayTriggerEditorActionStatus = preset.ActionStatus;
            DisplayTriggerEditorConnectionStatus = preset.ConnectionStatus;
            DisplayTriggerEditorCondition = preset.Condition;
            DisplayTriggerEditorMinPercent = preset.MinPercent;
            DisplayTriggerEditorMaxPercent = preset.MaxPercent;
            DisplayTriggerEditorChangePercent = preset.ChangePercent;
            DisplayTriggerEditorDurationMs = preset.DurationMs;
            DisplayTriggerEditorTemplateMode = preset.TemplateMode;
            DisplayTriggerEditorTemplate = preset.Template;
            DisplayTriggerEditorValidationMessage = "";
        }
        finally
        {
            isRefreshingEditor = false;
        }

        RefreshPreview();
    }

    [RelayCommand]
    private void RemoveDisplayTrigger()
    {
        if (!TryGetSelectedIndex(out var selectedIndex))
        {
            return;
        }

        var triggers = Export().ToList();
        triggers.RemoveAt(selectedIndex);
        var nextIndex = triggers.Count == 0 ? -1 : Math.Min(selectedIndex, triggers.Count - 1);
        UpdateTriggers(triggers, nextIndex);
    }

    [RelayCommand]
    private void MoveDisplayTriggerUp()
    {
        MoveSelectedDisplayTrigger(-1);
    }

    [RelayCommand]
    private void MoveDisplayTriggerDown()
    {
        MoveSelectedDisplayTrigger(1);
    }

    [RelayCommand]
    private void TestDisplayTrigger()
    {
        if (!TryCreateValidatedEditorTrigger(out var trigger))
        {
            return;
        }

        TestRequested?.Invoke(this, new DisplayTriggerTestRequestedEventArgs(trigger));
    }

    private void MoveSelectedDisplayTrigger(int direction)
    {
        if (!TryGetSelectedIndex(out var sourceIndex))
        {
            return;
        }

        var targetIndex = sourceIndex + direction;
        if (targetIndex < 0 || targetIndex >= DisplayTriggers.Count)
        {
            return;
        }

        var triggers = Export().ToList();
        (triggers[sourceIndex], triggers[targetIndex]) = (triggers[targetIndex], triggers[sourceIndex]);
        UpdateTriggers(triggers, targetIndex);
    }

    private void UpdateTriggers(IReadOnlyList<DisplayTriggerConfig> triggers, int selectedIndex)
    {
        Load(triggers, selectedIndex);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SetSelection(int selectedIndex)
    {
        isRefreshingEditor = true;
        SelectedDisplayTrigger = selectedIndex >= 0 && selectedIndex < DisplayTriggers.Count
            ? DisplayTriggers[selectedIndex]
            : null;
        isRefreshingEditor = false;
        LoadEditor(SelectedDisplayTrigger);
    }

    private void LoadEditor(DisplayTriggerViewModel? value)
    {
        isRefreshingEditor = true;
        try
        {
            DisplayTriggerEditorEnabled = value?.Enabled ?? true;
            DisplayTriggerEditorEventType = value?.EventType ?? "input";
            DisplayTriggerEditorInput = value?.Input ?? "slider:1";
            DisplayTriggerEditorActionStatus = value?.ActionStatus ?? "any";
            DisplayTriggerEditorConnectionStatus = value?.ConnectionStatus ?? "any";
            DisplayTriggerEditorCondition = value?.Condition ?? "any";
            DisplayTriggerEditorMinPercent = value?.MinPercent ?? 0;
            DisplayTriggerEditorMaxPercent = value?.MaxPercent ?? 100;
            DisplayTriggerEditorChangePercent = value?.ChangePercent ?? 5;
            DisplayTriggerEditorDurationMs = value?.DurationMs ?? 3000;
            DisplayTriggerEditorTemplateMode = value?.TemplateMode ?? "normal";
            DisplayTriggerEditorTemplate = value?.Template ?? DisplayTriggerNormalizer.DefaultTemplate;
            DisplayTriggerEditorValidationMessage = "";
        }
        finally
        {
            isRefreshingEditor = false;
        }

        UseBlockEditor = false;
        RefreshPreview();
    }

    private bool TryGetSelectedIndex(out int selectedIndex)
    {
        selectedIndex = SelectedDisplayTrigger?.Index ?? -1;
        if (selectedIndex >= 0 && selectedIndex < DisplayTriggers.Count)
        {
            return true;
        }

        DisplayTriggerEditorValidationMessage = "No display trigger selected.";
        return false;
    }

    private bool TryCreateValidatedEditorTrigger(out DisplayTriggerConfig trigger)
    {
        trigger = new DisplayTriggerConfig();
        DisplayTriggerEditorValidationMessage = "";

        var eventType = NormalizeEventType(DisplayTriggerEditorEventType);
        var input = eventType is "mediaChanged" or "connectionChanged" or "profileChanged"
            ? "*"
            : (DisplayTriggerEditorInput ?? "").Trim();
        if (input != "*" && !BindingEditorService.IsSupportedInput(input))
        {
            DisplayTriggerEditorValidationMessage = "Input must be * or look like button:1 or slider:1.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(DisplayTriggerEditorTemplate))
        {
            DisplayTriggerEditorValidationMessage = "Display content is required.";
            return false;
        }

        if (eventType == "input"
            && input.StartsWith("slider:", StringComparison.OrdinalIgnoreCase)
            && DisplayTriggerEditorMinPercent > DisplayTriggerEditorMaxPercent)
        {
            DisplayTriggerEditorValidationMessage = "Min % must not be greater than Max %.";
            return false;
        }

        if (eventType == "input"
            && !input.StartsWith("slider:", StringComparison.OrdinalIgnoreCase)
            && DisplayTriggerNormalizer.NormalizeCondition(DisplayTriggerEditorCondition) != "any")
        {
            DisplayTriggerEditorValidationMessage = "Slider conditions require a slider input.";
            return false;
        }

        trigger = Normalize(new DisplayTriggerConfig
        {
            Enabled = DisplayTriggerEditorEnabled,
            EventType = eventType,
            Input = input,
            ActionStatus = DisplayTriggerEditorActionStatus,
            ConnectionStatus = DisplayTriggerEditorConnectionStatus,
            Condition = DisplayTriggerEditorCondition,
            MinPercent = DisplayTriggerEditorMinPercent,
            MaxPercent = DisplayTriggerEditorMaxPercent,
            ChangePercent = DisplayTriggerEditorChangePercent,
            DurationMs = DisplayTriggerEditorDurationMs,
            TemplateMode = DisplayTriggerEditorTemplateMode,
            Template = DisplayTriggerEditorTemplate
        });

        return true;
    }

    private DisplayTriggerConfig CreateEditorTriggerForPreview()
    {
        return Normalize(new DisplayTriggerConfig
        {
            Enabled = DisplayTriggerEditorEnabled,
            EventType = DisplayTriggerEditorEventType,
            Input = string.IsNullOrWhiteSpace(DisplayTriggerEditorInput) ? "slider:1" : DisplayTriggerEditorInput,
            ActionStatus = DisplayTriggerEditorActionStatus,
            ConnectionStatus = DisplayTriggerEditorConnectionStatus,
            Condition = DisplayTriggerEditorCondition,
            MinPercent = DisplayTriggerEditorMinPercent,
            MaxPercent = DisplayTriggerEditorMaxPercent,
            ChangePercent = DisplayTriggerEditorChangePercent,
            DurationMs = DisplayTriggerEditorDurationMs,
            TemplateMode = DisplayTriggerEditorTemplateMode,
            Template = string.IsNullOrWhiteSpace(DisplayTriggerEditorTemplate) ? " " : DisplayTriggerEditorTemplate
        });
    }

    private void RefreshPreview()
    {
        if (isRefreshingEditor)
        {
            return;
        }

        OnPropertyChanged(nameof(DisplayTriggerPreviewMatchStatus));
        OnPropertyChanged(nameof(DisplayTriggerEditorContentWarning));
        OnPropertyChanged(nameof(IsDisplayTriggerEditorDirty));
        OnPropertyChanged(nameof(DisplayTriggerSequenceInfo));

        var trigger = CreateEditorTriggerForPreview();
        var preview = CreatePreviewContext(trigger);
        var renderContext = CreateRenderContext(preview);
        if (DisplayTriggerNormalizer.NormalizeTemplateMode(trigger.TemplateMode) == "layout")
        {
            SetLayoutPreview(DisplayTriggerRenderer.RenderLayoutScreen(trigger, renderContext));
            return;
        }

        var lines = DisplayTriggerRenderer.FitPreviewLines(
            DisplayTriggerRenderer.RenderTemplateLines(trigger, renderContext));
        SetPreviewLines(lines);
    }

    private DisplayTriggerPreviewContext CreatePreviewContext(DisplayTriggerConfig trigger)
    {
        var eventType = NormalizeEventType(trigger.EventType);
        var input = trigger.Input == "*" && eventType == "input" ? "slider:1" : trigger.Input;
        var percent = input.StartsWith("slider:", StringComparison.OrdinalIgnoreCase)
            ? NormalizePercent(DisplayTriggerPreviewPercent)
            : (int?)null;
        var sliderValue = percent is null
            ? (int?)null
            : (int)Math.Round(percent.Value / 100.0 * 4095);
        var actionStatus = eventType == "actionResult"
            ? NormalizeActionStatus(DisplayTriggerPreviewActionStatus) == "any"
                ? "ok"
                : NormalizeActionStatus(DisplayTriggerPreviewActionStatus)
            : "";
        var connectionStatus = NormalizeConnectionStatus(DisplayTriggerPreviewConnectionStatus) == "any"
            ? "connected"
            : NormalizeConnectionStatus(DisplayTriggerPreviewConnectionStatus);

        return new DisplayTriggerPreviewContext(
            input,
            sliderValue,
            percent,
            actionStatus,
            DisplayTriggerPreviewActionName,
            DisplayTriggerPreviewActionMessage,
            DisplayTriggerPreviewMediaArtist,
            DisplayTriggerPreviewMediaTitle,
            connectionStatus);
    }

    private DisplayTriggerRenderContext CreateRenderContext(DisplayTriggerPreviewContext preview)
    {
        return new DisplayTriggerRenderContext
        {
            Input = preview.Input,
            SliderValue = preview.SliderValue,
            SliderPercent = preview.Percent,
            ActionStatus = preview.ActionStatus,
            ActionName = preview.ActionName,
            ActionMessage = preview.ActionMessage,
            MediaArtist = preview.MediaArtist,
            MediaTitle = preview.MediaTitle,
            ConnectionStatus = preview.ConnectionStatus,
            Profile = activeProfile,
            Status = runtimeConnectionStatus
        };
    }

    private void SetPreviewLines(IReadOnlyList<string> lines)
    {
        UsesLayoutDisplayPreview = false;
        OnPropertyChanged(nameof(UsesTextDisplayPreview));
        DisplayTriggerPreviewLine1 = lines.Count > 0 ? lines[0] : "";
        DisplayTriggerPreviewLine2 = lines.Count > 1 ? lines[1] : "";
        DisplayTriggerPreviewLine3 = lines.Count > 2 ? lines[2] : "";
        DisplayTriggerPreviewLine4 = lines.Count > 3 ? lines[3] : "";
        DisplayTriggerPreviewLine5 = lines.Count > 4 ? lines[4] : "";
        DisplayTriggerLayoutPreviewTitle = "";
        DisplayTriggerLayoutPreviewLine1 = "";
        DisplayTriggerLayoutPreviewLine2 = "";
        DisplayTriggerLayoutPreviewIcon = "";
        OnPropertyChanged(nameof(HasDisplayTriggerLayoutPreviewIcon));
        DisplayTriggerLayoutPreviewValue = 0;
        DisplayTriggerLayoutPreviewValueText = "";
        ShowsDisplayTriggerLayoutPreviewValue = false;
    }

    private void SetLayoutPreview(DisplayLayoutScreen screen)
    {
        UsesLayoutDisplayPreview = true;
        OnPropertyChanged(nameof(UsesTextDisplayPreview));
        DisplayTriggerPreviewLine1 = "";
        DisplayTriggerPreviewLine2 = "";
        DisplayTriggerPreviewLine3 = "";
        DisplayTriggerPreviewLine4 = "";
        DisplayTriggerPreviewLine5 = "";
        DisplayTriggerLayoutPreviewTitle = FitLayoutPreviewText(screen.Title, 21);
        DisplayTriggerLayoutPreviewLine1 = FitLayoutPreviewText(screen.Line1, screen.Icon.Length > 0 ? 17 : 21);
        DisplayTriggerLayoutPreviewLine2 = FitLayoutPreviewText(screen.Line2, screen.Icon.Length > 0 ? 17 : 21);
        DisplayTriggerLayoutPreviewIcon = IconPreviewLabel(screen.Icon);
        OnPropertyChanged(nameof(HasDisplayTriggerLayoutPreviewIcon));
        DisplayTriggerLayoutPreviewValue = screen.NormalizedValuePercent ?? 0;
        DisplayTriggerLayoutPreviewValueText = screen.NormalizedValuePercent is null
            ? ""
            : $"{screen.NormalizedValuePercent}%";
        ShowsDisplayTriggerLayoutPreviewValue = screen.NormalizedValuePercent is not null;
    }

    private static string FitLayoutPreviewText(string value, int maxCharacters)
    {
        var trimmed = (value ?? "").Trim();
        return trimmed.Length <= maxCharacters
            ? trimmed
            : trimmed[..Math.Max(1, maxCharacters - 2)] + "..";
    }

    private static string IconPreviewLabel(string icon)
    {
        return icon switch
        {
            "check" => "OK",
            "warn" => "!",
            "x" => "X",
            "sync" => "~",
            _ => ""
        };
    }

    private static DisplayTriggerViewModel ToViewModel(int index, DisplayTriggerConfig trigger)
    {
        return new DisplayTriggerViewModel(
            index,
            trigger.Enabled,
            trigger.EventType,
            trigger.Input,
            trigger.ActionStatus,
            trigger.ConnectionStatus,
            trigger.Condition,
            trigger.MinPercent,
            trigger.MaxPercent,
            trigger.ChangePercent,
            trigger.DurationMs,
            trigger.TemplateMode,
            trigger.Template);
    }

    private static DisplayTriggerConfig ToConfig(DisplayTriggerViewModel trigger)
    {
        return Normalize(new DisplayTriggerConfig
        {
            Enabled = trigger.Enabled,
            EventType = trigger.EventType,
            Input = trigger.Input,
            ActionStatus = trigger.ActionStatus,
            ConnectionStatus = trigger.ConnectionStatus,
            Condition = trigger.Condition,
            MinPercent = trigger.MinPercent,
            MaxPercent = trigger.MaxPercent,
            ChangePercent = trigger.ChangePercent,
            DurationMs = trigger.DurationMs,
            TemplateMode = trigger.TemplateMode,
            Template = trigger.Template
        });
    }

    private static DisplayTriggerConfig Normalize(DisplayTriggerConfig trigger) =>
        DisplayTriggerNormalizer.Normalize(trigger);

    private static string NormalizeEventType(string? value) =>
        DisplayTriggerNormalizer.NormalizeEventType(value);

    private static string NormalizeActionStatus(string? value) =>
        DisplayTriggerNormalizer.NormalizeActionStatus(value);

    private static string NormalizeConnectionStatus(string? value) =>
        DisplayTriggerNormalizer.NormalizeConnectionStatus(value);

    private static int NormalizePercent(int value) =>
        DisplayTriggerNormalizer.NormalizePercent(value);
}

public sealed class DisplayTriggerTestRequestedEventArgs(DisplayTriggerConfig trigger) : EventArgs
{
    public DisplayTriggerConfig Trigger { get; } = trigger;
}
