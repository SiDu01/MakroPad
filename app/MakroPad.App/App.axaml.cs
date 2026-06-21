using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using MakroPad.App.ViewModels;
using MakroPad.App.Views;
using MakroPad.Core.Configuration;

namespace MakroPad.App;

public partial class App : Application
{
    private Shell? mainWindow;
    private TrayIcon? trayIcon;
    private bool isExitRequested;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Tray app: the window can be hidden without exiting, so the process lifetime is driven
            // by the explicit "Exit" tray command rather than by the window closing.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            mainWindow = new Shell
            {
                DataContext = new ShellViewModel(),
            };

            mainWindow.Closing += (_, args) =>
            {
                if (isExitRequested)
                {
                    return;
                }

                args.Cancel = true;
                mainWindow.Hide();
            };

            desktop.MainWindow = mainWindow;
            trayIcon = CreateTrayIcon(desktop);

            // Honour the "start in tray" setting: skip the initial window show when enabled.
            if (!ShouldStartMinimizedToTray())
            {
                mainWindow.Show();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static bool ShouldStartMinimizedToTray()
    {
        var store = new MakroPadConfigStore(ShellViewModel.GetDefaultConfigPath());
        return store.TryLoadExisting()?.AppSettings.StartMinimizedToTray ?? false;
    }

    private TrayIcon CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var openItem = new NativeMenuItem("Open MakroPad");
        openItem.Click += (_, _) => ShowMainWindow();

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            isExitRequested = true;
            trayIcon?.Dispose();
            desktop.Shutdown();
        };

        var menu = new NativeMenu();
        menu.Items.Add(openItem);
        menu.Items.Add(exitItem);

        var icon = new TrayIcon
        {
            Icon = mainWindow?.Icon,
            ToolTipText = "MakroPad",
            IsVisible = true,
            Menu = menu
        };

        icon.Clicked += (_, _) => ShowMainWindow();
        return icon;
    }

    private void ShowMainWindow()
    {
        if (mainWindow is null)
        {
            return;
        }

        if (!mainWindow.IsVisible)
        {
            mainWindow.Show();
        }

        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Activate();
    }
}
