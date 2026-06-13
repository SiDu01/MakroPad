using Avalonia.Controls;
using MakroPad.App.ViewModels;

namespace MakroPad.App.Views;

public partial class Shell : Window
{
    public Shell()
    {
        InitializeComponent();
        Closed += (_, _) => ViewModel?.Shutdown();
    }

    private ShellViewModel? ViewModel => DataContext as ShellViewModel;
}
