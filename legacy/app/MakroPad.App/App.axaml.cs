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

            trayIcon = CreateTrayIcon(desktop);

            // Assigning desktop.MainWindow makes Avalonia auto-show the window. To honour the
            // "start in tray" setting we must NOT assign/show it when starting hidden; the tray
            // "Open" command shows it on demand instead.
            if (!ShouldStartMinimizedToTray())
            {
                ShowMainWindow();
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

        // Assign the main window lazily on first show so a tray start stays hidden, while a normal
        // start (and later "Open") still gets proper main-window semantics.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: null } desktop)
        {
            desktop.MainWindow = mainWindow;
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
