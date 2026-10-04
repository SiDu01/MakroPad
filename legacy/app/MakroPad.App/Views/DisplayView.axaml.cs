using Avalonia.Controls;
using Avalonia.Input;
using MakroPad.App.ViewModels;

namespace MakroPad.App.Views;

public partial class DisplayView : UserControl
{
    public DisplayView()
    {
        InitializeComponent();
    }

    private void RuleList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ShellViewModel shell && shell.EditDisplayRuleCommand.CanExecute(null))
        {
            shell.EditDisplayRuleCommand.Execute(null);
        }
    }

    private void IdleList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ShellViewModel shell && shell.EditIdleScreenCommand.CanExecute(null))
        {
            shell.EditIdleScreenCommand.Execute(null);
        }
    }
}
