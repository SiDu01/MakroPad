using Avalonia.Controls;
using MakroPad.App.ViewModels;

namespace MakroPad.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) => ViewModel?.Shutdown();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;
}
