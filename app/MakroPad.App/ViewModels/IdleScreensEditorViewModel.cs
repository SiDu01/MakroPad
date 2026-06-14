using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.App.Services;
using MakroPad.Core.Configuration;
using MakroPad.Core.DisplayTriggers;

namespace MakroPad.App.ViewModels;

/// <summary>
/// Manages the list of idle screens and the editor for the selected one (name, activation,
/// content via template/mode/blocks, and a live OLED preview). Edits apply live to the list.
/// </summary>
public partial class IdleScreensEditorViewModel : ViewModelBase
{
    private readonly OledDefaultScreenRenderer previewRenderer = new();
    private bool isRefreshing;
    private bool isSyncingBlocks;
    private DisplayBlockViewModel? activeBlock;
    private string profileName = "Default";
    private string connectionStatus = "Disconnected";
    private NowPlayingTrack? nowPlayingTrack;
    private int? systemVolumePercent;

    public event EventHandler? Changed;

    public ObservableCollection<IdleScreenItemViewModel> IdleScreens { get; } = [];

    public IReadOnlyList<string> AvailableModes { get; } = ["normal", "markdown", "html"];

    public IReadOnlyList<string> AvailableActivations { get; } = ["default", "process"];

    public IReadOnlyList<string> AvailablePlaceholders { get; } =
    [
        "{profile}", "{status}", "{time}", "{date}", "{volume.system}",
        "{media.artist}", "{media.title}", "{media.track}"
    ];

    [ObservableProperty]
    private int idleRotationSeconds = 8;

    partial void OnIdleRotationSecondsChanged(int value)
    {
        if (isRefreshing)
        {
            return;
        }

        RaiseChanged();
    }

    [ObservableProperty]
    private IdleScreenItemViewModel? selectedIdleScreen;

    partial void OnSelectedIdleScreenChanged(IdleScreenItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsProcessActivation));
        OnPropertyChanged(nameof(IsMarkdown));
        OnPropertyChanged(nameof(ShowsBlockEditor));
        OnPropertyChanged(nameof(ShowsRawTemplate));
        UseBlockEditor = false;
        if (value is not null)
        {
            ParseBlocks();
        }

        RefreshPreview();
    }

    public bool HasSelection => SelectedIdleScreen is not null;

    public bool IsProcessActivation =>
        string.Equals(SelectedIdleScreen?.Activation, "process", StringComparison.OrdinalIgnoreCase);

    public bool IsMarkdown =>
        string.Equals(SelectedIdleScreen?.TemplateMode, "markdown", StringComparison.OrdinalIgnoreCase);

    // ----- Block ("Baukasten") editor -----

    [ObservableProperty]
    private bool useBlockEditor;

    partial void OnUseBlockEditorChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowsBlockEditor));
        OnPropertyChanged(nameof(ShowsRawTemplate));
    }

    public bool ShowsBlockEditor => HasSelection && IsMarkdown && UseBlockEditor;

    public bool ShowsRawTemplate => HasSelection && !ShowsBlockEditor;

    public ObservableCollection<DisplayBlockViewModel> Blocks { get; } = [];

    // ----- Preview -----

    [ObservableProperty]
    private string previewLine1 = "";

    [ObservableProperty]
    private string previewLine2 = "";

    [ObservableProperty]
    private string previewLine3 = "";

    [ObservableProperty]
    private string previewLine4 = "";

    [ObservableProperty]
    private string previewLine5 = "";

    public void Load(MakroPadConfig config)
    {
        isRefreshing = true;
        IdleScreens.Clear();
        foreach (var screen in config.AppSettings.IdleScreens)
        {
            IdleScreens.Add(new IdleScreenItemViewModel(screen, OnItemChanged));
        }

        IdleRotationSeconds = Math.Clamp(config.AppSettings.IdleRotationSeconds, 1, 3600);
        isRefreshing = false;
        SelectedIdleScreen = IdleScreens.FirstOrDefault();
    }

    public IReadOnlyList<IdleScreenConfig> Export() =>
        IdleScreens.Select(item => item.ToConfig()).ToList();

    public void SetRuntime(string profileName, string connectionStatus, NowPlayingTrack? nowPlayingTrack, int? systemVolumePercent)
    {
        this.profileName = profileName;
        this.connectionStatus = connectionStatus;
        this.nowPlayingTrack = nowPlayingTrack;
        this.systemVolumePercent = systemVolumePercent;
        RefreshPreview();
    }

    [RelayCommand]
    private void AddIdleScreen()
    {
        var item = new IdleScreenItemViewModel(
            new IdleScreenConfig { Name = "Neuer Screen", Template = "# {profile}\n{status}", TemplateMode = "markdown" },
            OnItemChanged);
        IdleScreens.Add(item);
        SelectedIdleScreen = item;
        RaiseChanged();
    }

    [RelayCommand]
    private void DuplicateIdleScreen()
    {
        if (SelectedIdleScreen is null)
        {
            return;
        }

        var clone = new IdleScreenItemViewModel(
            SelectedIdleScreen.ToConfig() with { Name = SelectedIdleScreen.Name + " Kopie" },
            OnItemChanged);
        var index = IdleScreens.IndexOf(SelectedIdleScreen);
        IdleScreens.Insert(index + 1, clone);
        SelectedIdleScreen = clone;
        RaiseChanged();
    }

    [RelayCommand]
    private void RemoveIdleScreen()
    {
        if (SelectedIdleScreen is null)
        {
            return;
        }

        var index = IdleScreens.IndexOf(SelectedIdleScreen);
        IdleScreens.Remove(SelectedIdleScreen);
        SelectedIdleScreen = IdleScreens.Count == 0
            ? null
            : IdleScreens[Math.Min(index, IdleScreens.Count - 1)];
        RaiseChanged();
    }

    [RelayCommand]
    private void MoveIdleScreenUp() => MoveSelected(-1);

    [RelayCommand]
    private void MoveIdleScreenDown() => MoveSelected(1);

    private void MoveSelected(int direction)
    {
        if (SelectedIdleScreen is null)
        {
            return;
        }

        var index = IdleScreens.IndexOf(SelectedIdleScreen);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= IdleScreens.Count)
        {
            return;
        }

        IdleScreens.Move(index, target);
        RaiseChanged();
    }

    [RelayCommand]
    private void ToggleBlockEditor()
    {
        if (!UseBlockEditor)
        {
            ParseBlocks();
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
        if (block is not null && Blocks.Remove(block))
        {
            if (ReferenceEquals(activeBlock, block))
            {
                activeBlock = null;
            }

            SerializeBlocks();
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
            : Blocks.LastOrDefault(b => b.IsEditable);
        if (target is not null)
        {
            target.Text += placeholder;
        }
    }

    private void AddBlock(DisplayBlockKind kind, string text)
    {
        var block = new DisplayBlockViewModel(kind, text, OnBlockChanged);
        Blocks.Add(block);
        if (block.IsEditable)
        {
            activeBlock = block;
        }

        SerializeBlocks();
    }

    private void MoveBlock(DisplayBlockViewModel? block, int direction)
    {
        if (block is null)
        {
            return;
        }

        var index = Blocks.IndexOf(block);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= Blocks.Count)
        {
            return;
        }

        Blocks.Move(index, target);
        SerializeBlocks();
    }

    private void OnBlockChanged(DisplayBlockViewModel block)
    {
        activeBlock = block;
        SerializeBlocks();
    }

    private void ParseBlocks()
    {
        isSyncingBlocks = true;
        Blocks.Clear();
        foreach (var block in DisplayBlockMarkup.Parse(SelectedIdleScreen?.Template))
        {
            Blocks.Add(new DisplayBlockViewModel(block.Kind, block.Text, OnBlockChanged));
        }

        activeBlock = Blocks.LastOrDefault(b => b.IsEditable);
        isSyncingBlocks = false;
    }

    private void SerializeBlocks()
    {
        if (isSyncingBlocks || SelectedIdleScreen is null)
        {
            return;
        }

        SelectedIdleScreen.Template = DisplayBlockMarkup.Serialize(
            Blocks.Select(block => new DisplayBlock(block.Kind, block.Text)));
    }

    private void OnItemChanged(IdleScreenItemViewModel item)
    {
        if (ReferenceEquals(item, SelectedIdleScreen))
        {
            OnPropertyChanged(nameof(IsProcessActivation));
            OnPropertyChanged(nameof(IsMarkdown));
            OnPropertyChanged(nameof(ShowsBlockEditor));
            OnPropertyChanged(nameof(ShowsRawTemplate));
            RefreshPreview();
        }

        if (!isRefreshing)
        {
            RaiseChanged();
        }
    }

    private void RefreshPreview()
    {
        var screen = SelectedIdleScreen;
        if (screen is null)
        {
            PreviewLine1 = PreviewLine2 = PreviewLine3 = PreviewLine4 = PreviewLine5 = "";
            return;
        }

        var resolved = previewRenderer.RenderTemplate(
            screen.Template,
            new OledDefaultScreenRenderContext(profileName, connectionStatus, nowPlayingTrack, systemVolumePercent, DateTime.Now));
        var lines = DisplayTriggerRenderer.FitPreviewLines(
            DisplayTriggerRenderer.RenderContentLines(resolved, screen.TemplateMode));

        PreviewLine1 = Line(lines, 0);
        PreviewLine2 = Line(lines, 1);
        PreviewLine3 = Line(lines, 2);
        PreviewLine4 = Line(lines, 3);
        PreviewLine5 = Line(lines, 4);
    }

    private static string Line(IReadOnlyList<string> lines, int index) =>
        index < lines.Count ? lines[index] : "";

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
