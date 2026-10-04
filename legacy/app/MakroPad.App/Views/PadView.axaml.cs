using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MakroPad.App.ViewModels;

namespace MakroPad.App.Views;

public partial class PadView : UserControl
{
    public PadView()
    {
        InitializeComponent();
    }

    private ShellViewModel? ViewModel => DataContext as ShellViewModel;

    private async void BrowseOpenApp_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { CanOpen: true } storageProvider)
        {
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "App oder Datei auswaehlen",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Applications and scripts")
                {
                    Patterns = ["*.exe", "*.bat", "*.cmd", "*.ps1", "*.lnk"]
                },
                FilePickerFileTypes.All
            ]
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            ViewModel.ProfilesEditor.ActionEditor.SetOpenAppPath(path);
        }
    }
}
