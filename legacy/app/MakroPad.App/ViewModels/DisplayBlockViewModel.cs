using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MakroPad.App.ViewModels;

public enum DisplayBlockKind
{
    Heading,
    Text,
    ListItem,
    Divider,
    Blank
}

/// <summary>
/// One block in the visual ("Baukasten") display editor. Blocks are a 1:1 view of the
/// Markdown-like template lines: heading, text, list item, divider or a blank row.
/// </summary>
public partial class DisplayBlockViewModel : ObservableObject
{
    private readonly Action<DisplayBlockViewModel>? onChanged;

    public DisplayBlockViewModel(
        DisplayBlockKind kind,
        string text,
        Action<DisplayBlockViewModel>? onChanged = null,
        string alignment = "left")
    {
        Kind = kind;
        this.text = text;
        this.alignment = NormalizeAlignment(alignment);
        this.onChanged = onChanged;
    }

    public DisplayBlockKind Kind { get; }

    [ObservableProperty]
    private string text;

    partial void OnTextChanged(string value) => onChanged?.Invoke(this);

    [ObservableProperty]
    private string alignment;

    partial void OnAlignmentChanged(string value) => onChanged?.Invoke(this);

    public bool IsEditable => Kind is DisplayBlockKind.Heading or DisplayBlockKind.Text or DisplayBlockKind.ListItem;

    public bool SupportsAlignment => IsEditable;

    public string KindLabel => Kind switch
    {
        DisplayBlockKind.Heading => "Ueberschrift",
        DisplayBlockKind.Text => "Text",
        DisplayBlockKind.ListItem => "Listenpunkt",
        DisplayBlockKind.Divider => "Trennlinie",
        DisplayBlockKind.Blank => "Leerzeile",
        _ => ""
    };

    public string Watermark => Kind switch
    {
        DisplayBlockKind.Heading => "Ueberschrift (groß)",
        DisplayBlockKind.ListItem => "Listenpunkt",
        _ => "Text"
    };

    public string NonEditablePreview => Kind switch
    {
        DisplayBlockKind.Divider => "--------------------",
        DisplayBlockKind.Blank => "(Leerzeile)",
        _ => ""
    };

    private static string NormalizeAlignment(string value) => value?.ToLowerInvariant() switch
    {
        "center" => "center",
        "right" => "right",
        _ => "left"
    };
}
