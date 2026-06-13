using Avalonia;
using Avalonia.Controls;

namespace MakroPad.App.Controls;

public partial class PageHeader : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PageHeader, string>(nameof(Title));

    public static readonly StyledProperty<string> SubtitleProperty =
        AvaloniaProperty.Register<PageHeader, string>(nameof(Subtitle));

    public static readonly StyledProperty<bool> ShowConfigActionsProperty =
        AvaloniaProperty.Register<PageHeader, bool>(nameof(ShowConfigActions));

    public static readonly StyledProperty<bool> ShowProfileBadgeProperty =
        AvaloniaProperty.Register<PageHeader, bool>(nameof(ShowProfileBadge));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public bool ShowConfigActions
    {
        get => GetValue(ShowConfigActionsProperty);
        set => SetValue(ShowConfigActionsProperty, value);
    }

    public bool ShowProfileBadge
    {
        get => GetValue(ShowProfileBadgeProperty);
        set => SetValue(ShowProfileBadgeProperty, value);
    }

    public PageHeader()
    {
        InitializeComponent();
    }
}
