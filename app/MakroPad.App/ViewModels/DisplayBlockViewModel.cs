using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MakroPad.App.ViewModels;

public enum DisplayBlockKind
{
    Heading,
    Text,
    ListItem,
    Divider
}

/// <summary>
/// One block in the visual ("Baukasten") display editor. Blocks are a 1:1 view of the
/// Markdown-like template lines: heading, text, list item or divider.
/// </summary>
public partial class DisplayBlockViewModel : ObservableObject
{
    private readonly Action<DisplayBlockViewModel>? onChanged;

    public DisplayBlockViewModel(DisplayBlockKind kind, string text, Action<DisplayBlockViewModel>? onChanged = null)
    {
        Kind = kind;
        this.text = text;
        this.onChanged = onChanged;
    }

    public DisplayBlockKind Kind { get; }

    [ObservableProperty]
    private string text;

    partial void OnTextChanged(string value) => onChanged?.Invoke(this);

    public bool IsEditable => Kind != DisplayBlockKind.Divider;

    public string KindLabel => Kind switch
    {
        DisplayBlockKind.Heading => "Ueberschrift",
        DisplayBlockKind.Text => "Text",
        DisplayBlockKind.ListItem => "Listenpunkt",
        DisplayBlockKind.Divider => "Trennlinie",
        _ => ""
    };

    public string Watermark => Kind switch
    {
        DisplayBlockKind.Heading => "Ueberschrift (groß)",
        DisplayBlockKind.ListItem => "Listenpunkt",
        _ => "Text"
    };
}
